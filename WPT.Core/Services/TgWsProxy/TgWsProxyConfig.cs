namespace WPT.Core.Services.TgWsProxy;

public sealed class TgWsProxyConfig
{
    public int Port { get; set; } = 1443;

    public string Host { get; set; } = "127.0.0.1";

    public string Secret { get; set; } = string.Empty;

    public Dictionary<int, string> DcRedirects { get; set; } = new()
    {
        [2] = "149.154.167.220",
        [4] = "149.154.167.220"
    };

    public int BufferSize { get; set; } = 256 * 1024;

    public bool FallbackCfProxy { get; set; } = true;

    public IReadOnlyList<string> CfProxyUserDomains { get; set; } = [];

    public IReadOnlyList<string> CfProxyWorkerDomains { get; set; } = [];
}
