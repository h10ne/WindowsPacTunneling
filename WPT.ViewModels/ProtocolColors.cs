namespace WPT.ViewModels;

public static class ProtocolColors
{
    public static string GetHex(string? protocol) =>
        (protocol ?? string.Empty).ToLowerInvariant() switch
        {
            "vless" => "#2E7D32",
            "vmess" => "#1565C0",
            "trojan" => "#E65100",
            "ss" => "#6A1B9A",
            "awg" => "#009688",
            _ => "#555558"
        };
}
