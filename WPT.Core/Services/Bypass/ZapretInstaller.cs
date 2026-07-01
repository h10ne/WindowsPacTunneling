using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace WPT.Core.Services.Bypass;

public static class ZapretInstaller
{
    private const string Repository = "Flowseal/zapret-discord-youtube";

    public static bool IsInstalled() =>
        Directory.Exists(AppPaths.ZapretBinDirectory)
        && File.Exists(Path.Combine(AppPaths.ZapretBinDirectory, "winws.exe"));

    public static IReadOnlyList<string> DiscoverStrategies()
    {
        if (!Directory.Exists(AppPaths.ZapretDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(AppPaths.ZapretDirectory, "general*.bat", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    public static async Task EnsureInstalledAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (IsInstalled())
        {
            return;
        }

        progress?.Report("Загрузка zapret...");
        AppPaths.EnsureRoot();
        Directory.CreateDirectory(AppPaths.ZapretDirectory);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WindowPacTunneling/1.0");

        var release = await httpClient.GetFromJsonAsync<GitHubRelease>(
            $"https://api.github.com/repos/{Repository}/releases/latest",
            cancellationToken);

        var asset = release?.Assets?.FirstOrDefault(x =>
            x.Name.Contains("zapret-discord-youtube", StringComparison.OrdinalIgnoreCase)
            && x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException("Не найден zip-архив zapret в последнем релизе GitHub.");
        }

        var zipPath = Path.Combine(AppPaths.ZapretDirectory, asset.Name);
        progress?.Report($"Скачивание {asset.Name}...");

        await using (var response = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken))
        await using (var file = File.Create(zipPath))
        {
            await response.CopyToAsync(file, cancellationToken);
        }

        progress?.Report("Распаковка zapret...");
        var extractDirectory = Path.Combine(AppPaths.ZapretDirectory, "extract");
        if (Directory.Exists(extractDirectory))
        {
            Directory.Delete(extractDirectory, recursive: true);
        }

        Directory.CreateDirectory(extractDirectory);
        ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

        var rootWithBat = Directory
            .EnumerateFiles(extractDirectory, "general.bat", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(rootWithBat))
        {
            throw new InvalidOperationException("В архиве zapret не найден general.bat.");
        }

        CopyDirectory(rootWithBat, AppPaths.ZapretDirectory);
        Directory.Delete(extractDirectory, recursive: true);
        File.Delete(zipPath);
        UnblockDirectory(AppPaths.ZapretDirectory);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, directory));
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void UnblockDirectory(string directory)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file + ":Zone.Identifier");
            }
            catch
            {
            }
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
