using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WPT.Core.Services;

public static class ComponentDownloadScriptLauncher
{
    public static bool TryLaunch(ComponentDownloadScriptKind kind)
    {
        try
        {
            AppPaths.EnsureRoot();
            Directory.CreateDirectory(AppPaths.ScriptsDirectory);

            var scriptName = GetScriptBaseName(kind);
            var ps1Path = Path.Combine(AppPaths.ScriptsDirectory, scriptName + ".ps1");
            var cmdPath = Path.Combine(AppPaths.ScriptsDirectory, scriptName + ".cmd");

            File.WriteAllText(ps1Path, BuildPowerShellScript(kind), ScriptEncoding);
            File.WriteAllText(cmdPath, BuildCmdLauncher(ps1Path), ScriptEncoding);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"\"{cmdPath}\"\"",
                WorkingDirectory = AppPaths.ScriptsDirectory,
                UseShellExecute = true
            });

            AppLog.Info($"Download helper opened: {cmdPath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, $"Failed to launch download helper for {kind}");
            return false;
        }
    }

    public static InvalidOperationException CreateFailureException(
        ComponentDownloadScriptKind kind,
        string componentLabel,
        Exception inner)
    {
        var launched = TryLaunch(kind);
        var hint = launched
            ? " Открыто окно CMD для установки вручную."
            : $" Скрипт сохранён в {AppPaths.ScriptsDirectory}.";

        return new InvalidOperationException(
            $"Не удалось скачать {componentLabel} из приложения.{hint}",
            inner);
    }

    public static bool IsDownloadRelated(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is HttpRequestException or IOException)
            {
                return true;
            }

            if (current.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("соединен", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly UTF8Encoding ScriptEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private static string GetScriptBaseName(ComponentDownloadScriptKind kind) => kind switch
    {
        ComponentDownloadScriptKind.SingBox => "install-sing-box",
        ComponentDownloadScriptKind.SingBoxProcessMode => "install-sing-box-pm",
        ComponentDownloadScriptKind.Zapret => "install-zapret",
        ComponentDownloadScriptKind.Wireproxy => "install-wireproxy",
        ComponentDownloadScriptKind.AppUpdate => "download-wpt-update",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string BuildCmdLauncher(string ps1Path) =>
        $"""
        @echo off
        title WPT component install
        powershell -NoProfile -ExecutionPolicy Bypass -File "{ps1Path}"
        echo.
        echo Done or failed. Close this window and retry WPT.
        pause

        """;

    private static string BuildPowerShellScript(ComponentDownloadScriptKind kind) => kind switch
    {
        ComponentDownloadScriptKind.SingBox => BuildSingBoxScript(fixedTag: null, "sing-box.exe", "sing-box-version.txt"),
        ComponentDownloadScriptKind.SingBoxProcessMode => BuildSingBoxScript("v1.12.12", "sing-box-pm.exe", "sing-box-pm-version.txt"),
        ComponentDownloadScriptKind.Zapret => BuildZapretScript(),
        ComponentDownloadScriptKind.Wireproxy => BuildWireproxyScript(),
        ComponentDownloadScriptKind.AppUpdate => BuildAppUpdateScript(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string BuildSingBoxScript(string? fixedTag, string exeName, string versionFileName)
    {
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "windows-arm64"
            : "windows-amd64";
        var assetPattern = $"sing-box-*-{arch}.zip";
        var root = EscapePs(AppPaths.Root.Replace('\\', '/'));
        var bin = EscapePs(AppPaths.BinDirectory.Replace('\\', '/'));
        var apiUrl = string.IsNullOrWhiteSpace(fixedTag)
            ? "https://api.github.com/repos/SagerNet/sing-box/releases/latest"
            : $"https://api.github.com/repos/SagerNet/sing-box/releases/tags/{fixedTag}";

        return $$"""
            $ErrorActionPreference = "Stop"
            $root = "{{root}}"
            $bin = "{{bin}}"
            $exeName = "{{exeName}}"
            $versionFile = Join-Path $bin "{{versionFileName}}"
            New-Item -ItemType Directory -Force -Path $bin | Out-Null

            Write-Host "Fetching sing-box release..."
            $releaseJson = curl.exe -sSL --ssl-no-revoke -H "Accept: application/vnd.github+json" -H "User-Agent: WPT" "{{apiUrl}}"
            if ($LASTEXITCODE -ne 0) { throw "GitHub API failed" }
            $release = $releaseJson | ConvertFrom-Json
            $tag = $release.tag_name
            $asset = $release.assets | Where-Object { $_.name -like "{{assetPattern}}" } | Select-Object -First 1
            if (-not $asset) { throw "Asset {{assetPattern}} not found in $tag" }

            $zip = Join-Path $bin $asset.name
            $url = "https://github.com/SagerNet/sing-box/releases/download/$tag/$($asset.name)"
            $extract = Join-Path $bin "extract-manual"

            Write-Host "Downloading $tag ..."
            curl.exe -sSL --ssl-no-revoke -L -o $zip $url
            if ($LASTEXITCODE -ne 0) { throw "Download failed" }

            Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
            Expand-Archive -Path $zip -DestinationPath $extract -Force
            $exe = Get-ChildItem $extract -Recurse -Filter "sing-box.exe" | Select-Object -First 1
            if (-not $exe) { throw "sing-box.exe not found in archive" }

            Copy-Item $exe.FullName (Join-Path $bin $exeName) -Force
            Set-Content -Path $versionFile -Value $tag -Encoding UTF8
            Remove-Item $zip -Force
            Remove-Item $extract -Recurse -Force
            Write-Host "OK: $(Join-Path $bin $exeName) ($tag)" -ForegroundColor Green

            """;
    }

    private static string BuildZapretScript()
    {
        var root = EscapePs(AppPaths.Root.Replace('\\', '/'));
        var zapret = EscapePs(AppPaths.ZapretDirectory.Replace('\\', '/'));

        return $$"""
            $ErrorActionPreference = "Stop"
            $root = "{{root}}"
            $zapret = "{{zapret}}"
            New-Item -ItemType Directory -Force -Path $root | Out-Null

            Write-Host "Stopping winws if running..."
            taskkill /IM winws.exe /F 2>$null | Out-Null

            Write-Host "Fetching zapret release..."
            $releaseJson = curl.exe -sSL --ssl-no-revoke -H "Accept: application/vnd.github+json" -H "User-Agent: WPT" "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest"
            if ($LASTEXITCODE -ne 0) { throw "GitHub API failed" }
            $release = $releaseJson | ConvertFrom-Json
            $tag = $release.tag_name
            $asset = $release.assets | Where-Object { $_.name -like "*zapret-discord-youtube*.zip" } | Select-Object -First 1
            if (-not $asset) { throw "zapret zip not found in $tag" }

            $zip = Join-Path $root $asset.name
            $url = "https://github.com/Flowseal/zapret-discord-youtube/releases/download/$tag/$($asset.name)"
            $extract = Join-Path $root "zapret-extract-manual"

            Write-Host "Downloading $tag ..."
            curl.exe -sSL --ssl-no-revoke -L -o $zip $url
            if ($LASTEXITCODE -ne 0) { throw "Download failed" }

            Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
            Expand-Archive -Path $zip -DestinationPath $extract -Force
            $general = Get-ChildItem $extract -Recurse -Filter "general.bat" | Select-Object -First 1
            if (-not $general) { throw "general.bat not found in archive" }
            $sourceRoot = $general.Directory.FullName

            if (Test-Path $zapret) {
                $backup = "$zapret.old"
                Remove-Item $backup -Recurse -Force -ErrorAction SilentlyContinue
                Move-Item $zapret $backup -Force
            }
            New-Item -ItemType Directory -Force -Path $zapret | Out-Null
            Copy-Item -Path (Join-Path $sourceRoot "*") -Destination $zapret -Recurse -Force
            Set-Content -Path (Join-Path $zapret "version.txt") -Value $tag -Encoding UTF8

            Remove-Item $zip -Force
            Remove-Item $extract -Recurse -Force
            Write-Host "OK: $zapret ($tag)" -ForegroundColor Green

            """;
    }

    private static string BuildWireproxyScript()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "windows_386",
            Architecture.Arm64 => "windows_arm64",
            _ => "windows_amd64"
        };
        var root = EscapePs(AppPaths.Root.Replace('\\', '/'));
        var bin = EscapePs(AppPaths.BinDirectory.Replace('\\', '/'));

        return $$"""
            $ErrorActionPreference = "Stop"
            $bin = "{{bin}}"
            New-Item -ItemType Directory -Force -Path $bin | Out-Null

            Write-Host "Fetching wireproxy release..."
            $releaseJson = curl.exe -sSL --ssl-no-revoke -H "Accept: application/vnd.github+json" -H "User-Agent: WPT" "https://api.github.com/repos/bropines/awg-wireproxy/releases/latest"
            if ($LASTEXITCODE -ne 0) { throw "GitHub API failed" }
            $release = $releaseJson | ConvertFrom-Json
            $tag = $release.tag_name
            $asset = $release.assets | Where-Object { $_.name -like "*{{arch}}*.tar.gz" } | Select-Object -First 1
            if (-not $asset) { throw "wireproxy {{arch}} tar.gz not found in $tag" }

            $archive = Join-Path $bin $asset.name
            $url = "https://github.com/bropines/awg-wireproxy/releases/download/$tag/$($asset.name)"
            $extract = Join-Path $bin "awg-wireproxy-extract-manual"

            Write-Host "Downloading $tag ..."
            curl.exe -sSL --ssl-no-revoke -L -o $archive $url
            if ($LASTEXITCODE -ne 0) { throw "Download failed" }

            Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
            New-Item -ItemType Directory -Force -Path $extract | Out-Null
            tar -xzf $archive -C $extract
            if ($LASTEXITCODE -ne 0) { throw "tar extract failed" }

            $exe = Get-ChildItem $extract -Recurse -Filter "wireproxy.exe" | Select-Object -First 1
            if (-not $exe) { throw "wireproxy.exe not found in archive" }

            Copy-Item $exe.FullName (Join-Path $bin "wireproxy.exe") -Force
            Set-Content -Path (Join-Path $bin "awg-wireproxy-version.txt") -Value $tag -Encoding UTF8
            Set-Content -Path (Join-Path $bin "awg-wireproxy.capability") -Value "awg-wireproxy-v1" -Encoding UTF8

            Remove-Item $archive -Force
            Remove-Item $extract -Recurse -Force
            Write-Host "OK: $(Join-Path $bin 'wireproxy.exe') ($tag)" -ForegroundColor Green

            """;
    }

    private static string BuildAppUpdateScript()
    {
        var downloadDir = EscapePs(Path.Combine(Path.GetTempPath(), "wpt-update").Replace('\\', '/'));

        return $$"""
            $ErrorActionPreference = "Stop"
            $dir = "{{downloadDir}}"
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            $out = Join-Path $dir "WPT.exe.download"

            Write-Host "Fetching WPT release..."
            $releaseJson = curl.exe -sSL --ssl-no-revoke -H "Accept: application/vnd.github+json" -H "User-Agent: WPT" "https://api.github.com/repos/h10ne/WindowsPacTunneling/releases/latest"
            if ($LASTEXITCODE -ne 0) { throw "GitHub API failed" }
            $release = $releaseJson | ConvertFrom-Json
            $tag = $release.tag_name
            $asset = $release.assets | Where-Object { $_.name -eq "WPT.exe" } | Select-Object -First 1
            if (-not $asset) { throw "WPT.exe not found in $tag" }

            $url = "https://github.com/h10ne/WindowsPacTunneling/releases/download/$tag/WPT.exe"
            Write-Host "Downloading $tag ..."
            curl.exe -sSL --ssl-no-revoke -L -o $out $url
            if ($LASTEXITCODE -ne 0) { throw "Download failed" }

            Write-Host "OK: $out" -ForegroundColor Green
            Write-Host "Close WPT and replace the exe manually, or run the downloaded file."

            """;
    }

    private static string EscapePs(string value) => value.Replace("'", "''");

}
