using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace WindowPacTunneling.Services;

public static class SingBoxInstaller
{
    private const string Repository = "SagerNet/sing-box";

    public static string ExecutablePath => Path.Combine(AppPaths.BinDirectory, "sing-box.exe");

    public static bool IsInstalled() => File.Exists(ExecutablePath);

    public static async Task EnsureInstalledAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (IsInstalled())
        {
            return;
        }

        progress?.Report("Загрузка sing-box...");
        AppPaths.EnsureRoot();
        Directory.CreateDirectory(AppPaths.BinDirectory);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WindowPacTunneling/1.0");

        var release = await httpClient.GetFromJsonAsync<GitHubRelease>(
            $"https://api.github.com/repos/{Repository}/releases/latest",
            cancellationToken);

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "windows-arm64",
            _ => "windows-amd64"
        };

        var asset = release?.Assets?.FirstOrDefault(x =>
            x.Name.StartsWith("sing-box-", StringComparison.OrdinalIgnoreCase)
            && x.Name.Contains(arch, StringComparison.OrdinalIgnoreCase)
            && x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException($"Не найден архив sing-box ({arch}) в последнем релизе GitHub.");
        }

        var zipPath = Path.Combine(AppPaths.BinDirectory, asset.Name);
        progress?.Report($"Скачивание {asset.Name}...");

        await using (var response = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken))
        await using (var file = File.Create(zipPath))
        {
            await response.CopyToAsync(file, cancellationToken);
        }

        progress?.Report("Распаковка sing-box...");
        var extractDirectory = Path.Combine(AppPaths.BinDirectory, "extract");
        if (Directory.Exists(extractDirectory))
        {
            Directory.Delete(extractDirectory, recursive: true);
        }

        Directory.CreateDirectory(extractDirectory);
        ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

        var extractedExe = Directory
            .EnumerateFiles(extractDirectory, "sing-box.exe", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (extractedExe == null)
        {
            throw new InvalidOperationException("В архиве sing-box не найден sing-box.exe.");
        }

        File.Copy(extractedExe, ExecutablePath, overwrite: true);
        Directory.Delete(extractDirectory, recursive: true);
        File.Delete(zipPath);
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
