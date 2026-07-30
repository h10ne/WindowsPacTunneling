using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;
using WinUiWindow = Microsoft.UI.Xaml.Window;

namespace WPT.WinUI.Services;

internal static class WindowTitleBarHelper
{
    public static void ApplyTheme(WinUiWindow window)
    {
        var titleBar = window.AppWindow.TitleBar;
        if (titleBar == null)
        {
            return;
        }

        if (IsDarkTheme(window))
        {
            ApplyDark(titleBar);
            return;
        }

        ApplyLight(titleBar);
    }

    public static void WatchTheme(WinUiWindow window, FrameworkElement root)
    {
        ApplyTheme(window);
        root.ActualThemeChanged += (_, _) => ApplyTheme(window);
    }

    private static void ApplyDark(Microsoft.UI.Windowing.AppWindowTitleBar titleBar)
    {
        var background = Color.FromArgb(255, 32, 32, 32);
        var foreground = Colors.White;
        var inactiveForeground = Color.FromArgb(255, 160, 160, 160);
        var hover = Color.FromArgb(255, 55, 55, 55);
        var pressed = Color.FromArgb(255, 70, 70, 70);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    private static void ApplyLight(Microsoft.UI.Windowing.AppWindowTitleBar titleBar)
    {
        var background = Color.FromArgb(255, 243, 243, 243);
        var foreground = Color.FromArgb(255, 0, 0, 0);
        var inactiveForeground = Color.FromArgb(255, 140, 140, 140);
        var hover = Color.FromArgb(255, 230, 230, 230);
        var pressed = Color.FromArgb(255, 210, 210, 210);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    private static bool IsDarkTheme(WinUiWindow window)
    {
        if (window.Content is FrameworkElement root)
        {
            if (root.ActualTheme == ElementTheme.Dark)
            {
                return true;
            }

            if (root.ActualTheme == ElementTheme.Light)
            {
                return false;
            }
        }

        return !IsSystemLightTheme();
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i != 0;
        }
        catch
        {
            return true;
        }
    }

}
