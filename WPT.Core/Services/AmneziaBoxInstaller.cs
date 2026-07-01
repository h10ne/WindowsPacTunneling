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

    public static async Task EnsureInstalledAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (IsInstalled())
        {
            return;
        }

        if (File.Exists(ExecutablePath))
        {
            progress?.Report("Обновление amnezia-box...");
            File.Delete(ExecutablePath);
        }

        AppPaths.EnsureRoot();
        Directory.CreateDirectory(AppPaths.BinDirectory);

        var bundled = Path.Combine(AppContext.BaseDirectory, "ThirdParty", "AmneziaBox", "amnezia-box.exe");
        if (File.Exists(bundled))
        {
            progress?.Report("Копирование amnezia-box...");
            File.Copy(bundled, ExecutablePath, overwrite: true);
            await File.WriteAllTextAsync(CapabilityMarkerPath, CapabilityMarker, cancellationToken);
            return;
        }

        throw new InvalidOperationException(
            "Не найден amnezia-box.exe. Пересоберите WPT или положите amnezia-box.exe в ThirdParty/AmneziaBox.");
    }

}
