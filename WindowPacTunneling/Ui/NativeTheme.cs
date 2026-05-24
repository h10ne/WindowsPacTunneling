using System.Runtime.InteropServices;

namespace WindowPacTunneling.Ui;

internal static class NativeTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeOld = 19;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint attributeValue, int attributeSize);

    public static void ApplyDarkTitleBar(IWin32Window window)
    {
        if (window.Handle == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return;
        }

        var useDark = 1;
        _ = DwmSetWindowAttribute(window.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(window.Handle, DwmwaUseImmersiveDarkModeOld, ref useDark, sizeof(int));

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var captionColor = ToColorRef(UiTheme.FormBackground);
        var textColor = ToColorRef(UiTheme.TextPrimary);
        _ = DwmSetWindowAttribute(window.Handle, DwmwaCaptionColor, ref captionColor, sizeof(uint));
        _ = DwmSetWindowAttribute(window.Handle, DwmwaTextColor, ref textColor, sizeof(uint));
    }

    private static uint ToColorRef(Color color) =>
        (uint)color.R | ((uint)color.G << 8) | ((uint)color.B << 16);
}
