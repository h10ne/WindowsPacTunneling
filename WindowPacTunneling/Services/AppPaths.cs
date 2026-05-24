namespace WindowPacTunneling.Services;

public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowPacTunneling");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string ListsDirectory => Path.Combine(Root, "lists");

    public static string PacFile => Path.Combine(Root, "proxy.pac");

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ListsDirectory);
    }
}
