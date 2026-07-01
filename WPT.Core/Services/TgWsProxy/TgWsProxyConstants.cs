namespace WPT.Core.Services.TgWsProxy;

internal static class TgWsProxyConstants
{
    public const int HandshakeLen = 64;
    public const int SkipLen = 8;
    public const int PrekeyLen = 32;
    public const int KeyLen = 32;
    public const int IvLen = 16;
    public const int ProtoTagPos = 56;
    public const int DcIdxPos = 60;

    public static ReadOnlySpan<byte> ProtoTagAbridged => [0xEF, 0xEF, 0xEF, 0xEF];

    public static ReadOnlySpan<byte> ProtoTagIntermediate => [0xEE, 0xEE, 0xEE, 0xEE];

    public static ReadOnlySpan<byte> ProtoTagSecure => [0xDD, 0xDD, 0xDD, 0xDD];

    public const uint ProtoAbridgedInt = 0xEFEFEFEF;
    public const uint ProtoIntermediateInt = 0xEEEEEEEE;
    public const uint ProtoPaddedIntermediateInt = 0xDDDDDDDD;

    public static readonly byte[] Zero64 = new byte[64];

    public static readonly Dictionary<int, string> DcDefaultIps = new()
    {
        [1] = "149.154.175.50",
        [2] = "149.154.167.51",
        [3] = "149.154.175.100",
        [4] = "149.154.167.91",
        [5] = "149.154.171.5",
        [203] = "91.105.192.100"
    };

    public static string[] WsDomains(int dc, bool isMedia)
    {
        if (dc == 203)
        {
            dc = 2;
        }

        if (isMedia)
        {
            return [$"kws{dc}-1.web.telegram.org", $"kws{dc}.web.telegram.org"];
        }

        return [$"kws{dc}.web.telegram.org", $"kws{dc}-1.web.telegram.org"];
    }
}
