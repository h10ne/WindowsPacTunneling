using System.Diagnostics;
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

    public static string? GetInstalledVersion()
    {
        if (!File.Exists(AppPaths.ZapretVersionFile))
        {
            return null;
        }

        var version = File.ReadAllText(AppPaths.ZapretVersionFile).Trim();
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

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

    public static async Task<ZapretUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient();
        var release = await FetchLatestReleaseAsync(httpClient, cancellationToken);
        var latestVersion = release.TagName;
        var installedVersion = GetInstalledVersion();
        var isInstalled = IsInstalled();

        if (!isInstalled)
        {
            return new ZapretUpdateCheckResult(
                false,
                true,
                null,
                latestVersion,
                $"Zapret не установлен. Доступна версия {latestVersion}.");
        }

        if (!string.IsNullOrWhiteSpace(installedVersion)
            && installedVersion.Equals(latestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new ZapretUpdateCheckResult(
                true,
                false,
                installedVersion,
                latestVersion,
                $"Установлена актуальная версия {installedVersion}.");
        }

        var installedLabel = string.IsNullOrWhiteSpace(installedVersion) ? "неизвестна" : installedVersion;
        return new ZapretUpdateCheckResult(
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
        progress?.Report("Загрузка zapret...");
        AppPaths.EnsureRoot();

        using var httpClient = CreateHttpClient();
        var release = await FetchLatestReleaseAsync(httpClient, cancellationToken);
        var asset = FindReleaseAsset(release);

        var zipPath = Path.Combine(AppPaths.Root, asset.Name);
        var extractDirectory = Path.Combine(AppPaths.Root, "zapret-extract");

        try
        {
            progress?.Report($"Скачивание {asset.Name}...");

            await using (var response = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken))
            await using (var file = File.Create(zipPath))
            {
                await response.CopyToAsync(file, cancellationToken);
            }

            progress?.Report("Распаковка zapret...");
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

            progress?.Report("Установка zapret...");
            await PrepareForInstallAsync(progress, cancellationToken);
            ReplaceZapretDirectory(rootWithBat);
            UnblockDirectory(AppPaths.ZapretDirectory);
            File.WriteAllText(AppPaths.ZapretVersionFile, release.TagName);
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
            throw new InvalidOperationException("Не удалось получить информацию о последнем релизе zapret.");
        }

        return release;
    }

    private static GitHubAsset FindReleaseAsset(GitHubRelease release)
    {
        var asset = release.Assets?.FirstOrDefault(x =>
            x.Name.Contains("zapret-discord-youtube", StringComparison.OrdinalIgnoreCase)
            && x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException("Не найден zip-архив zapret в последнем релизе GitHub.");
        }

        return asset;
    }

    public static void StopRunningProcesses()
    {
        foreach (var process in Process.GetProcessesByName("winws"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        ZapretLegacyServiceCleanup.TryRemoveInBackground();
    }

    public static bool HasRunningProcesses()
    {
        var processes = Process.GetProcessesByName("winws");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static async Task PrepareForInstallAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Остановка zapret...");
        StopRunningProcesses();
        WinDivertHelper.TryStopDriver();
        await Task.Delay(1500, cancellationToken);

        if (WinDivertHelper.IsDriverRunning() && !AdminHelper.IsRunningAsAdmin())
        {
            throw new InvalidOperationException(
                "Драйвер WinDivert всё ещё активен. Перезапустите WPT от имени администратора и повторите обновление.");
        }

        progress?.Report("Очистка старых файлов zapret...");
        ZapretDirectoryCleanup.DiscardLegacyInstallations();
    }

    private static void ReplaceZapretDirectory(string sourceRoot)
    {
        var backupDirectory = AppPaths.ZapretDirectory + ".old";

        try
        {
            ZapretDirectoryCleanup.DiscardLegacyInstallations();

            if (Directory.Exists(AppPaths.ZapretDirectory))
            {
                if (Directory.Exists(backupDirectory))
                {
                    TryRenameAbandonedDirectory(backupDirectory);
                }

                Directory.Move(AppPaths.ZapretDirectory, backupDirectory);
            }

            Directory.CreateDirectory(AppPaths.ZapretDirectory);
            CopyDirectory(sourceRoot, AppPaths.ZapretDirectory);
            TryDeleteDirectoryBestEffort(backupDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (TryReplaceInPlace(sourceRoot))
            {
                return;
            }

            throw new InvalidOperationException(
                "Не удалось заменить файлы zapret. Перезапустите WPT от имени администратора, затем повторите обновление.",
                ex);
        }
    }

    private static bool TryReplaceInPlace(string sourceRoot)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ZapretDirectory);
            CopyDirectory(sourceRoot, AppPaths.ZapretDirectory);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteDirectoryBestEffort(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch
            {
            }
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            TryRenameAbandonedDirectory(path);
        }
    }

    private static void TryRenameAbandonedDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var abandonedPath = $"{path}.abandoned-{DateTime.UtcNow.Ticks}";
        try
        {
            Directory.Move(path, abandonedPath);
        }
        catch
        {
        }
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
