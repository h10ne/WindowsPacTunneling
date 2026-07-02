using System.Net;
using System.Net.Http;
using System.Text;
using WPT.Core.Models;

namespace WPT.Core.Services;

public sealed class DomainListService : IDisposable
{
    private const string BaseUrl = "https://raw.githubusercontent.com/itdoginfo/allow-domains/main/";
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromDays(1);

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly string _lastUpdateFile;
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    public DomainListService()
    {
        var handler = new HttpClientHandler { UseProxy = false };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WindowPacTunneling/1.0");

        _cacheDirectory = AppPaths.ListsDirectory;
        _lastUpdateFile = Path.Combine(_cacheDirectory, ".last-update");

        AppPaths.EnsureRoot();
    }

    public event EventHandler<string>? StatusChanged;

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        if (!NeedsUpdate())
        {
            return;
        }

        await UpdateAllListsAsync(cancellationToken);
    }

    public async Task UpdateAllListsAsync(CancellationToken cancellationToken = default)
    {
        await _updateLock.WaitAsync(cancellationToken);

        try
        {
            ReportStatus("Обновление списков доменов...");

            var files = ServiceListDefinition.All
                .SelectMany(x => x.DomainFiles.Concat(x.SubnetFiles))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var failed = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await DownloadFileAsync(file, cancellationToken);
                }
                catch (Exception ex)
                {
                    failed++;
                    AppLog.Error(ex, $"Ошибка загрузки списка {file}");
                    ReportStatus($"Ошибка загрузки {file}: {ex.Message}");
                }
            }

            await File.WriteAllTextAsync(_lastUpdateFile, DateTime.UtcNow.ToString("O"), cancellationToken);

            ReportStatus(failed == 0
                ? "Списки обновлены"
                : $"Списки обновлены с ошибками: {failed}");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task<(HashSet<string> Domains, List<CidrEntry> Subnets)> CollectEntriesAsync(
        IEnumerable<string> listIds,
        IEnumerable<string> customDomains,
        IEnumerable<string> customIps,
        CancellationToken cancellationToken = default)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var subnets = new List<CidrEntry>();

        foreach (var listId in listIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definition = ServiceListDefinition.FindById(listId);
            if (definition == null)
            {
                continue;
            }

            foreach (var file in definition.DomainFiles)
            {
                foreach (var line in await ReadCachedLinesAsync(file, cancellationToken))
                {
                    domains.Add(line);
                }
            }

            foreach (var file in definition.SubnetFiles)
            {
                foreach (var line in await ReadCachedLinesAsync(file, cancellationToken))
                {
                    if (CidrEntry.TryParse(line, out var entry))
                    {
                        subnets.Add(entry);
                    }
                }
            }
        }

        foreach (var domain in customDomains)
        {
            domains.Add(domain);
        }

        foreach (var ip in customIps)
        {
            if (CidrEntry.TryParse(ip, out var entry))
            {
                subnets.Add(entry);
            }
        }

        return (domains, subnets);
    }

    private bool NeedsUpdate()
    {
        if (!File.Exists(_lastUpdateFile))
        {
            return true;
        }

        if (!DateTime.TryParse(File.ReadAllText(_lastUpdateFile), out var lastUpdate))
        {
            return true;
        }

        return DateTime.UtcNow - lastUpdate.ToUniversalTime() >= UpdateInterval
            || !HasCachedLists();
    }

    private bool HasCachedLists()
    {
        return ServiceListDefinition.All
            .SelectMany(x => x.DomainFiles.Concat(x.SubnetFiles))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .All(file => File.Exists(GetCachePath(file)));
    }

    private async Task DownloadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var url = BaseUrl + relativePath.Replace('\\', '/');
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var cachePath = GetCachePath(relativePath);
        var tempPath = cachePath + ".tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllBytesAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, cachePath, overwrite: true);
    }

    private async Task<IReadOnlyList<string>> ReadCachedLinesAsync(string relativePath, CancellationToken cancellationToken)
    {
        var cachePath = GetCachePath(relativePath);

        if (!File.Exists(cachePath))
        {
            await _updateLock.WaitAsync(cancellationToken);

            try
            {
                if (!File.Exists(cachePath))
                {
                    await DownloadFileAsync(relativePath, cancellationToken);
                }
            }
            finally
            {
                _updateLock.Release();
            }
        }

        var lines = await ReadAllLinesWithRetryAsync(cachePath, cancellationToken);
        return lines
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith('#'))
            .ToList();
    }

    private static async Task<string[]> ReadAllLinesWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await File.ReadAllLinesAsync(path, cancellationToken);
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(100 * (attempt + 1), cancellationToken);
            }
        }

        return await File.ReadAllLinesAsync(path, cancellationToken);
    }

    private string GetCachePath(string relativePath) =>
        Path.Combine(_cacheDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private void ReportStatus(string message) => StatusChanged?.Invoke(this, message);

    public void Dispose()
    {
        _httpClient.Dispose();
        _updateLock.Dispose();
    }
}

public readonly struct CidrEntry : IEquatable<CidrEntry>
{
    public CidrEntry(string network, string mask)
    {
        Network = network;
        Mask = mask;
    }

    public string Network { get; }

    public string Mask { get; }

    public bool Equals(CidrEntry other) =>
        Network == other.Network && Mask == other.Mask;

    public override bool Equals(object? obj) => obj is CidrEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Network, Mask);

    public static bool TryParse(string value, out CidrEntry entry)
    {
        entry = default;

        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (trimmed.Contains('/'))
        {
            var parts = trimmed.Split('/', 2);
            if (!IPAddress.TryParse(parts[0], out var ip))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32)
            {
                return false;
            }

            var mask = PrefixToMask(prefix);
            var network = GetNetworkAddress(ip, mask);
            entry = new CidrEntry(network.ToString(), mask.ToString());
            return true;
        }

        if (IPAddress.TryParse(trimmed, out _))
        {
            entry = new CidrEntry(trimmed, "255.255.255.255");
            return true;
        }

        return false;
    }

    private static IPAddress PrefixToMask(int prefix)
    {
        if (prefix == 0)
        {
            return IPAddress.Parse("0.0.0.0");
        }

        var mask = uint.MaxValue << (32 - prefix);
        var bytes = BitConverter.GetBytes(mask);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return new IPAddress(bytes);
    }

    private static IPAddress GetNetworkAddress(IPAddress ip, IPAddress mask)
    {
        var ipBytes = ip.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var networkBytes = new byte[4];

        for (var i = 0; i < 4; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }

        return new IPAddress(networkBytes);
    }
}
