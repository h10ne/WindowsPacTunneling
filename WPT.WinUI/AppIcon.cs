using System.Drawing;
using System.Reflection;
using Microsoft.UI.Xaml;

namespace WPT.WinUI;

internal static class AppIcon
{
    private const string EmbeddedResourceName = "WPT.WinUI.Assets.app.ico";

    public static void ApplyTo(Window window)
    {
        try
        {
            var path = ResolveIconPath();
            if (path != null)
            {
                window.AppWindow.SetIcon(path);
            }
        }
        catch
        {
        }
    }

    public static Icon? CreateTrayIcon()
    {
        try
        {
            using var stream = OpenIconStream();
            return stream != null ? new Icon(stream) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveIconPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(path))
        {
            return path;
        }

        path = Path.Combine(Path.GetTempPath(), "WPT.WinUI", "app.ico");
        if (File.Exists(path))
        {
            return path;
        }

        using var stream = OpenIconStream();
        if (stream == null)
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = File.OpenWrite(path))
        {
            stream.CopyTo(file);
        }

        return path;
    }

    private static Stream? OpenIconStream()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(path))
        {
            return File.OpenRead(path);
        }

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName);
    }

}
