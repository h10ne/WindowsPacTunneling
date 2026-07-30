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
        var bundledPath = GetBundledIconPath();
        if (bundledPath != null)
        {
            return bundledPath;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), "WPT.WinUI", "app.ico");
        using var stream = OpenIconStream();
        if (stream == null)
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
        using (var file = File.Create(tempPath))
        {
            stream.CopyTo(file);
        }

        return tempPath;
    }

    private static string? GetBundledIconPath()
    {
        var appDirectory = GetAppDirectory();
        if (appDirectory == null)
        {
            return null;
        }

        var bundledPath = Path.Combine(appDirectory, "Assets", "app.ico");
        return File.Exists(bundledPath) ? bundledPath : null;
    }

    private static string? GetAppDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return Path.GetDirectoryName(processPath);
        }

        return AppContext.BaseDirectory;
    }

    private static Stream? OpenIconStream()
    {
        var bundledPath = GetBundledIconPath();
        if (bundledPath != null)
        {
            return File.OpenRead(bundledPath);
        }

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName);
    }

}
