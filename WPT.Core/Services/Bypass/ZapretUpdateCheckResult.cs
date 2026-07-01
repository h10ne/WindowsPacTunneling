namespace WPT.Core.Services.Bypass;

public sealed record ZapretUpdateCheckResult(
    bool IsInstalled,
    bool UpdateAvailable,
    string? InstalledVersion,
    string LatestVersion,
    string Message);
