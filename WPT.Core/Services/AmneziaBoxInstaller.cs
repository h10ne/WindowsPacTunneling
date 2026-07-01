namespace WPT.Core.Services;

public static class AmneziaBoxInstaller
{
    private const string CapabilityMarker = "socks-udp-awg15";

    public static string ExecutablePath => Path.Combine(AppPaths.BinDirectory, "amnezia-box.exe");

    private static string CapabilityMarkerPath => Path.Combine(AppPaths.BinDirectory, "amnezia-box.capability");

    public static bool IsInstalled() =>
        File.Exists(ExecutablePath) &&
        File.Exists(CapabilityMarkerPath) &&
        string.Equals(File.ReadAllText(CapabilityMarkerPath).Trim(), CapabilityMarker, StringComparison.Ordinal);

    public static Task EnsureInstalledAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (IsInstalled())
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(
            "AmneziaBox пока не поставляется с приложением. Компонент будет доступен в следующих версиях.");
    }

}
