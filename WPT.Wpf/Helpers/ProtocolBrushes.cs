using System.Windows.Media;

namespace WPT.Wpf.Helpers;

public static class ProtocolBrushes
{
    private static readonly SolidColorBrush VlessBrush = CreateBrush(0x2E, 0x7D, 0x32);

    private static readonly SolidColorBrush VmessBrush = CreateBrush(0x15, 0x65, 0xC0);

    private static readonly SolidColorBrush TrojanBrush = CreateBrush(0xE6, 0x51, 0x00);

    private static readonly SolidColorBrush ShadowsocksBrush = CreateBrush(0x6A, 0x1B, 0x9A);

    private static readonly SolidColorBrush DefaultBrush = CreateBrush(0x55, 0x55, 0x58);

    public static System.Windows.Media.Brush Get(string? protocol) =>
        (protocol ?? string.Empty).ToLowerInvariant() switch
        {
            "vless" => VlessBrush,
            "vmess" => VmessBrush,
            "trojan" => TrojanBrush,
            "ss" => ShadowsocksBrush,
            _ => DefaultBrush
        };

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

}
