namespace WPT.Core.Services;

public sealed record SingBoxUpdateCheckResult(
    bool IsInstalled,
    bool UpdateAvailable,
    string? InstalledVersion,
    string LatestVersion,
    string Message);
