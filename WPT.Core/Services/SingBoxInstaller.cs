using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace WPT.Core.Services;

public static class SingBoxInstaller
{
    private const string Repository = "SagerNet/sing-box";

    public static string ExecutablePath => Path.Combine(AppPaths.BinDirectory, "sing-box.exe");

    public static bool IsInstalled() => File.Exists(ExecutablePath);

    public static string? GetInstalledVersion()
    {
        if (File.Exists(AppPaths.SingBoxVersionFile))
        {
            var version = File.ReadAllText(AppPaths.SingBoxVersionFile).Trim();
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
                $"Sing-box не установлен. Доступна версия {latestVersion}.");
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

    public static async Task EnsureInstalledAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (IsInstalled())
        {
            return;
        }

        await InstallOrUpdateAsync(progress, cancellationToken);
    }

    public static async Task InstallOrUpdateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Загрузка sing-box...");
        AppPaths.EnsureRoot();
        Directory.CreateDirectory(AppPaths.BinDirectory);

        using var httpClient = CreateHttpClient();
        var release = await FetchLatestReleaseAsync(httpClient, cancellationToken);
        var asset = FindReleaseAsset(release);
        var zipPath = Path.Combine(AppPaths.BinDirectory, asset.Name);
        var extractDirectory = Path.Combine(AppPaths.BinDirectory, "extract");

        try
        {
            progress?.Report($"Скачивание {asset.Name}...");

            await using (var response = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken))
            await using (var file = File.Create(zipPath))
            {
                await response.CopyToAsync(file, cancellationToken);
            }

            progress?.Report("Распаковка sing-box...");
            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, recursive: true);
            }

            Directory.CreateDirectory(extractDirectory);
            ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

            var extractedExe = Directory
                .EnumerateFiles(extractDirectory, "sing-box.exe", SearchOption.AllDirectories)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("В архиве sing-box не найден sing-box.exe.");

            File.Copy(extractedExe, ExecutablePath, overwrite: true);
            File.WriteAllText(AppPaths.SingBoxVersionFile, release.TagName);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
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

        foreach (var process in Process.GetProcessesByName("sing-box"))
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
        var processes = Process.GetProcessesByName("sing-box");

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
            throw new InvalidOperationException("Не удалось получить информацию о последнем релизе sing-box.");
        }

        return release;
    }

    private static GitHubAsset FindReleaseAsset(GitHubRelease release)
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "windows-arm64",
            _ => "windows-amd64"
        };

        var asset = release.Assets?.FirstOrDefault(x =>
            x.Name.StartsWith("sing-box-", StringComparison.OrdinalIgnoreCase)
            && x.Name.Contains(arch, StringComparison.OrdinalIgnoreCase)
            && x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException($"Не найден архив sing-box ({arch}) в последнем релизе GitHub.");
        }

        return asset;
    }

    private static string? TryReadVersionFromExecutable()
    {
        if (!IsInstalled())
        {
            return null;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = "version",
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

            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            const string prefix = "sing-box version ";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return line[prefix.Length..].Trim();
            }

            return line;
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
