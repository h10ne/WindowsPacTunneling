using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace WPT.Core.Services;

public static class AppUpdateService
{
    private const string Repository = "h10ne/WindowsPacTunneling";

    private const string ReleaseAssetName = "WPT.exe";

    private const int UpdaterMaxRetries = 120;

    public static async Task<AppUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var release = await FetchLatestReleaseAsync(cancellationToken);
        var latestVersion = AppVersion.ParseTag(release.TagName);
        var currentVersion = AppVersion.Current;
        var currentLabel = AppVersion.CurrentLabel;
        var latestLabel = AppVersion.ToLabel(latestVersion);

        if (!AppVersion.IsUpdateAvailable(latestVersion, currentVersion))
        {
            return new AppUpdateCheckResult(
                false,
                currentLabel,
                latestLabel,
                $"Установлена актуальная версия {currentLabel}.");
        }

        return new AppUpdateCheckResult(
            true,
            currentLabel,
            latestLabel,
            $"Доступно обновление: {currentLabel} → {latestLabel}.");
    }

    public static async Task<string> DownloadLatestReleaseAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadLatestReleaseCoreAsync(progress, cancellationToken);
        }
        catch (Exception ex) when (ComponentDownloadScriptLauncher.IsDownloadRelated(ex))
        {
            throw ComponentDownloadScriptLauncher.CreateFailureException(
                ComponentDownloadScriptKind.AppUpdate,
                "обновление приложения",
                ex);
        }
    }

    private static async Task<string> DownloadLatestReleaseCoreAsync(
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Проверка последней версии...");

        var release = await FetchLatestReleaseAsync(cancellationToken);
        var asset = FindReleaseAsset(release);
        var downloadDirectory = Path.Combine(Path.GetTempPath(), "wpt-update");
        Directory.CreateDirectory(downloadDirectory);
        var downloadPath = Path.Combine(downloadDirectory, "WPT.exe.download");

        if (File.Exists(downloadPath))
        {
            File.Delete(downloadPath);
        }

        progress?.Report($"Скачивание {asset.Name}...");

        using var httpClient = CreateHttpClient();
        await using (var response = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken))
        await using (var file = File.Create(downloadPath))
        {
            await response.CopyToAsync(file, cancellationToken);
        }

        UnblockFile(downloadPath);
        progress?.Report("Загрузка завершена.");
        return downloadPath;
    }

    public static void LaunchUpdaterAndExit(string downloadedPath, string targetExePath, bool restartAsAdmin)
    {
        if (!File.Exists(downloadedPath))
        {
            throw new FileNotFoundException("Файл обновления не найден.", downloadedPath);
        }

        if (string.IsNullOrWhiteSpace(targetExePath))
        {
            throw new InvalidOperationException("Не удалось определить путь к текущему приложению.");
        }

        var scriptPath = Path.Combine(Path.GetTempPath(), $"wpt-update-{Guid.NewGuid():N}.cmd");
        var script = BuildUpdaterScript(downloadedPath, targetExePath, restartAsAdmin);
        File.WriteAllText(scriptPath, script, Encoding.ASCII);

        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    public static string? GetCurrentExecutablePath() =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName;

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"WPT/{AppVersion.CurrentLabel.TrimStart('v')}");
        return httpClient;
    }

    private static async Task<GitHubRelease> FetchLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient();
        var release = await httpClient.GetFromJsonAsync<GitHubRelease>(
            $"https://api.github.com/repos/{Repository}/releases/latest",
            cancellationToken);

        if (release == null || string.IsNullOrWhiteSpace(release.TagName))
        {
            throw new InvalidOperationException("Не удалось получить информацию о последнем релизе приложения.");
        }

        return release;
    }

    private static GitHubAsset FindReleaseAsset(GitHubRelease release)
    {
        var asset = release.Assets?.FirstOrDefault(x =>
            x.Name.Equals(ReleaseAssetName, StringComparison.OrdinalIgnoreCase));

        if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException($"В последнем релизе не найден файл {ReleaseAssetName}.");
        }

        return asset;
    }

    private static string BuildUpdaterScript(string downloadedPath, string targetExePath, bool restartAsAdmin)
    {
        var downloaded = EscapeCmdPath(downloadedPath);
        var target = EscapeCmdPath(targetExePath);
        var builder = new StringBuilder();
        builder.AppendLine("@echo off");
        builder.AppendLine("setlocal");
        builder.AppendLine($"set \"NEW={downloaded}\"");
        builder.AppendLine($"set \"TARGET={target}\"");
        builder.AppendLine("timeout /t 2 /nobreak >nul");
        builder.AppendLine("set RETRIES=0");
        builder.AppendLine(":replace");
        builder.AppendLine("move /Y \"%NEW%\" \"%TARGET%\" >nul 2>&1");
        builder.AppendLine("if not errorlevel 1 goto start");
        builder.AppendLine("set /a RETRIES+=1");
        builder.AppendLine($"if %RETRIES% GEQ {UpdaterMaxRetries} exit /b 1");
        builder.AppendLine("timeout /t 1 /nobreak >nul");
        builder.AppendLine("goto replace");
        builder.AppendLine(":start");

        if (restartAsAdmin)
        {
            builder.AppendLine("powershell -NoProfile -ExecutionPolicy Bypass -Command \"Start-Process -FilePath '%TARGET%' -ArgumentList '--elevated'\"");
        }
        else
        {
            builder.AppendLine("start \"\" \"%TARGET%\"");
        }

        builder.AppendLine("del \"%~f0\"");
        builder.AppendLine("endlocal");
        return builder.ToString();
    }

    private static string EscapeCmdPath(string path) =>
        path.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static void UnblockFile(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            File.Delete(path + ":Zone.Identifier");
        }
        catch
        {
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
