using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace WPT.Core.Services;

public static class AwgWireproxyInstaller
{
    private const string Repository = "bropines/awg-wireproxy";

    private const string CapabilityMarker = "awg-wireproxy-v1";

    public static string ExecutablePath => Path.Combine(AppPaths.BinDirectory, "wireproxy.exe");

    public static bool IsInstalled() =>
        File.Exists(ExecutablePath) &&
        File.Exists(AppPaths.AwgWireproxyCapabilityFile) &&
        string.Equals(File.ReadAllText(AppPaths.AwgWireproxyCapabilityFile).Trim(), CapabilityMarker, StringComparison.Ordinal);

    public static string? GetInstalledVersion()
    {
        if (File.Exists(AppPaths.AwgWireproxyVersionFile))
        {
            var version = File.ReadAllText(AppPaths.AwgWireproxyVersionFile).Trim();
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        return TryReadVersionFromExecutable();
    }

    public static async Task<SingBoxUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient();
        var release = await FetchLatestReleaseAsync(httpClient, cancellationToken);
        var latestVersion = release.TagName;
        var installedVersion = GetInstalledVersion();
        var isInstalled = IsInstalled();

        if (!isInstalled)
        {
            return new SingBoxUpdateCheckResult(
                false,
                true,
                null,
                latestVersion,
                $"Wireproxy (AmneziaWG) не установлен. Доступна версия {latestVersion}.");
        }

        if (!string.IsNullOrWhiteSpace(installedVersion)
            && VersionsEqual(installedVersion, latestVersion))
        {
            return new SingBoxUpdateCheckResult(
                true,
                false,
                installedVersion,
                latestVersion,
                $"Установлена актуальная версия {installedVersion}.");
        }

        var installedLabel = string.IsNullOrWhiteSpace(installedVersion) ? "неизвестна" : installedVersion;
        return new SingBoxUpdateCheckResult(
            true,
            true,
            installedVersion,
            latestVersion,
            $"Доступно обновление: {installedLabel} → {latestVersion}.");
    }

    public static Task EnsureInstalledAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (IsInstalled())
        {
            return Task.CompletedTask;
        }

        return InstallOrUpdateAsync(progress, cancellationToken);
    }

    public static async Task InstallOrUpdateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Загрузка wireproxy (AmneziaWG)...");
        AppPaths.EnsureRoot();
        Directory.CreateDirectory(AppPaths.BinDirectory);

        using var httpClient = CreateHttpClient();
        var release = await FetchLatestReleaseAsync(httpClient, cancellationToken);
        var asset = FindReleaseAsset(release);
        var archivePath = Path.Combine(AppPaths.BinDirectory, asset.Name);
        var extractDirectory = Path.Combine(AppPaths.BinDirectory, "awg-wireproxy-extract");

        try
        {
            progress?.Report($"Скачивание {asset.Name}...");

            await using (var response = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken))
            await using (var file = File.Create(archivePath))
            {
                await response.CopyToAsync(file, cancellationToken);
            }

            progress?.Report("Распаковка wireproxy...");
            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, recursive: true);
            }

            Directory.CreateDirectory(extractDirectory);
            ExtractTarGz(archivePath, extractDirectory);

            var extractedExe = Directory
                .EnumerateFiles(extractDirectory, "wireproxy.exe", SearchOption.AllDirectories)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("В архиве wireproxy не найден wireproxy.exe.");

            File.Copy(extractedExe, ExecutablePath, overwrite: true);
            File.WriteAllText(AppPaths.AwgWireproxyVersionFile, release.TagName);
            File.WriteAllText(AppPaths.AwgWireproxyCapabilityFile, CapabilityMarker);
        }
        finally
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, recursive: true);
            }
        }
    }

    public static void StopRunningProcesses()
    {
        var executable = Path.GetFullPath(ExecutablePath);

        foreach (var process in Process.GetProcessesByName("wireproxy"))
        {
            try
            {
                if (process.MainModule?.FileName.Equals(executable, StringComparison.OrdinalIgnoreCase) == true)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public static bool HasRunningProcesses()
    {
        var executable = Path.GetFullPath(ExecutablePath);
        var processes = Process.GetProcessesByName("wireproxy");

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    if (process.MainModule?.FileName.Equals(executable, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static void ExtractTarGz(string archivePath, string destinationDirectory)
    {
        using var fileStream = File.OpenRead(archivePath);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzipStream, destinationDirectory, overwriteFiles: true);
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WindowPacTunneling/1.0");
        return httpClient;
    }

    private static async Task<GitHubRelease> FetchLatestReleaseAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        var release = await httpClient.GetFromJsonAsync<GitHubRelease>(
            $"https://api.github.com/repos/{Repository}/releases/latest",
            cancellationToken);

        if (release == null || string.IsNullOrWhiteSpace(release.TagName))
        {
            throw new InvalidOperationException("Не удалось получить информацию о последнем релизе wireproxy.");
        }

        return release;
    }

    private static GitHubAsset FindReleaseAsset(GitHubRelease release)
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "windows_386",
            Architecture.Arm64 => "windows_arm64",
            _ => "windows_amd64"
        };

        var asset = release.Assets?.FirstOrDefault(x =>
            x.Name.Contains(arch, StringComparison.OrdinalIgnoreCase)
            && x.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));

        if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException($"Не найден архив wireproxy ({arch}) в последнем релизе GitHub.");
        }

        return asset;
    }

    private static string? TryReadVersionFromExecutable()
    {
        if (!File.Exists(ExecutablePath))
        {
            return null;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
            {
                return null;
            }

            var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static bool VersionsEqual(string installedVersion, string latestVersion) =>
        string.Equals(NormalizeVersion(installedVersion), NormalizeVersion(latestVersion), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeVersion(string version) =>
        version.Trim().TrimStart('v', 'V');

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

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
