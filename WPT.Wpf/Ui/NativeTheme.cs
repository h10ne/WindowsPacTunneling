using System.Windows;
using System.Windows.Interop;

namespace WPT.Wpf.Ui;

internal static class NativeTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeOld = 19;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint attributeValue, int attributeSize);

    public static void ApplyDarkTitleBar(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return;
        }

        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            return;
        }

        var useDark = 1;
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaUseImmersiveDarkModeOld, ref useDark, sizeof(int));

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var captionColor = ToColorRef(0x18, 0x18, 0x18);
        var textColor = ToColorRef(0xD4, 0xD4, 0xD4);
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaCaptionColor, ref captionColor, sizeof(uint));
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaTextColor, ref textColor, sizeof(uint));
    }

    private static uint ToColorRef(byte r, byte g, byte b) =>
        (uint)r | ((uint)g << 8) | ((uint)b << 16);
}
