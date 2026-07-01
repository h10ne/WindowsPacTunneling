namespace WPT.Core.Services;

public sealed record AppUpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersionLabel,
    string LatestVersionLabel,
    string Message);
