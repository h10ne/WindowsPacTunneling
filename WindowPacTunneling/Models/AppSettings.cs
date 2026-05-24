namespace WindowPacTunneling.Models;

public sealed class AppSettings
{
    public string ProxyAddress { get; set; } = "127.0.0.1:10808";

    public List<string> ProxyHistory { get; set; } = ["127.0.0.1:10808"];

    public int PacPort { get; set; } = 1080;

    public List<int> PacPortHistory { get; set; } = [1080];

    public List<string> SelectedListIds { get; set; } = [];

    public List<string> CustomDomains { get; set; } = [];

    public List<string> CustomIps { get; set; } = [];

    public bool StartWithWindows { get; set; }

    public bool IsProxyActive { get; set; }

    public string? ActivePacHash { get; set; }
}
