using System.Reflection;

namespace WPT.Core;

public static class AppVersion
{
    public static Version Current { get; } = ResolveCurrentVersion();

    public static string CurrentLabel { get; } = ToLabel(Current);

    public static Version ParseTag(string tagName)
    {
        var normalized = tagName.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        if (!Version.TryParse(normalized, out var version))
        {
            throw new FormatException($"Некорректный формат версии: {tagName}.");
        }

        return Normalize(version);
    }

    public static bool IsUpdateAvailable(Version latest, Version current) =>
        Normalize(latest) > Normalize(current);

    public static string ToLabel(Version version)
    {
        var normalized = Normalize(version);
        return $"v{normalized.Major}.{normalized.Minor}.{normalized.Build}";
    }

    private static Version ResolveCurrentVersion()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version
            ?? Assembly.GetExecutingAssembly().GetName().Version;

        return assemblyVersion == null
            ? new Version(0, 0, 0)
            : Normalize(assemblyVersion);
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

}
