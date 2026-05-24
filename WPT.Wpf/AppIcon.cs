using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;

namespace WPT.Wpf;

internal static class AppIcon
{
    private const string PackUri = "pack://application:,,,/Resources/app.ico";

    public static void ApplyTo(Window window)
    {
        try
        {
            window.Icon = BitmapFrame.Create(new Uri(PackUri, UriKind.Absolute));
        }
        catch
        {
        }
    }

    public static Icon? CreateTrayIcon()
    {
        try
        {
            var stream = Application.GetResourceStream(new Uri(PackUri, UriKind.Absolute))?.Stream;
            return stream != null ? new Icon(stream) : null;
        }
        catch
        {
            return null;
        }
    }

}
