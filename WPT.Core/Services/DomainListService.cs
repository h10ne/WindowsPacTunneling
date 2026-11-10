using System.Net;
using System.Net.Http;
using System.Text;
using WPT.Core.Models;

namespace WPT.Core.Services;

public sealed class DomainListService : IDisposable
{
    private static readonly string[] ListSourceBaseUrls =
    [
        "https://raw.githubusercontent.com/itdoginfo/allow-domains/main/",
        "https://cdn.jsdelivr.net/gh/itdoginfo/allow-domains@main/"
    ];

    private static readonly string GitHubListBaseUrl = ListSourceBaseUrls[0];
    private static readonly string MirrorListBaseUrl = ListSourceBaseUrls[1];

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromDays(1);

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly string _lastUpdateFile;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private int _disposeState;
    private int _listSourceIndex;
    private HttpClient? _proxyHttpClient;
    private Uri? _proxyHttpClientUri;

    public DomainListService()
    {
        // Списки — небольшие текстовые файлы: заблокированный источник не должен держать минутами
        _httpClient = CreateHttpClient(null, TimeSpan.FromSeconds(30));
        _cacheDirectory = AppPaths.ListsDirectory;
        _lastUpdateFile = Path.Combine(_cacheDirectory, ".last-update");

        AppPaths.EnsureRoot();
    }

    /// <summary> Рабочий локальный прокси (http:// или socks5://) для загрузки списков, если GitHub и jsDelivr недоступны напрямую </summary>
    public Uri? FallbackProxy { get; set; }

    private static HttpClient CreateHttpClient(IWebProxy? proxy, TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        if (proxy != null)
        {
            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler)
        {
            Timeout = timeout,
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");

        return client;
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
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        var token = linkedCts.Token;

        await _updateLock.WaitAsync(token);

        try
        {
            ReportStatus("Обновление списков доменов...");

            var files = ServiceListDefinition.All
                .SelectMany(x => x.DomainFiles.Concat(x.SubnetFiles))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                await DownloadFileAsync(file, token);
            }

            await File.WriteAllTextAsync(_lastUpdateFile, DateTime.UtcNow.ToString("O"), token);

            ReportStatus("Списки обновлены");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Обновление списков прервано");
            ReportStatus($"Обновление списков прервано: {ex.Message}");
            throw;
        }
        finally
        {
            ReleaseUpdateLock();
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

    public bool HasCachedLists()
    {
        return ServiceListDefinition.All
            .SelectMany(x => x.DomainFiles.Concat(x.SubnetFiles))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .All(file => File.Exists(GetCachePath(file)));
    }

    // Источники: 0 — GitHub напрямую, 1 — jsDelivr напрямую, 2 — GitHub через локальный прокси
    private const int ProxySourceIndex = 2;

    private async Task DownloadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var url = relativePath.Replace('\\', '/');
        var preferred = _listSourceIndex;
        Exception? lastError = null;

        // Сначала источник, сработавший последним, затем остальные по порядку
        foreach (var index in new[] { preferred, 0, 1, ProxySourceIndex }.Distinct())
        {
            var client = index == ProxySourceIndex ? GetProxyHttpClient() : _httpClient;
            if (client == null)
            {
                continue;
            }

            var baseUrl = index == 1 ? MirrorListBaseUrl : GitHubListBaseUrl;

            try
            {
                await DownloadFileFromUrlAsync(client, baseUrl + url, relativePath, cancellationToken);
                if (index != preferred)
                {
                    _listSourceIndex = index;
                    AppLog.Info($"Список {relativePath} загружен через {DescribeSource(index)}. Дальнейшая загрузка в этом сеансе — сначала через него");
                }

                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                AppLog.Warning(ex, $"Загрузка {relativePath} через {DescribeSource(index)} не удалась");
            }
        }

        // Прямой доступ не работает — в следующий раз начинаем с прокси, если он появится
        _listSourceIndex = ProxySourceIndex;

        var message = FallbackProxy != null
            ? $"Не удалось загрузить {relativePath}: GitHub и jsDelivr недоступны ни напрямую, ни через прокси"
            : $"Не удалось загрузить {relativePath}: GitHub и jsDelivr недоступны";
        throw new InvalidOperationException(message, lastError);
    }

    private static string DescribeSource(int index) => index switch
    {
        0 => "GitHub",
        1 => "jsDelivr",
        _ => "GitHub (через локальный прокси)"
    };

    private HttpClient? GetProxyHttpClient()
    {
        var proxy = FallbackProxy;
        if (proxy == null)
        {
            return null;
        }

        if (_proxyHttpClient == null || _proxyHttpClientUri != proxy)
        {
            _proxyHttpClient?.Dispose();
            _proxyHttpClient = CreateHttpClient(new WebProxy(proxy), TimeSpan.FromMinutes(2));
            _proxyHttpClientUri = proxy;
        }

        return _proxyHttpClient;
    }

    private async Task DownloadFileFromUrlAsync(HttpClient client, string url, string relativePath, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
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
            ThrowIfDisposed();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
            var token = linkedCts.Token;

            await _updateLock.WaitAsync(token);

            try
            {
                if (!File.Exists(cachePath))
                {
                    await DownloadFileAsync(relativePath, token);
                }
            }
            finally
            {
                ReleaseUpdateLock();
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

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(DomainListService));
        }
    }

    private void ReleaseUpdateLock()
    {
        try
        {
            _updateLock.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _disposeCts.Cancel();

        try
        {
            if (!_updateLock.Wait(TimeSpan.FromSeconds(30)))
            {
                AppLog.Warning("Таймаут ожидания завершения обновления списков при выходе");
            }
            else
            {
                ReleaseUpdateLock();
            }
        }
        catch (ObjectDisposedException)
        {
        }

        _httpClient.Dispose();
        _proxyHttpClient?.Dispose();
        _updateLock.Dispose();
        _disposeCts.Dispose();
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
