namespace WPT.Core.Models;

public sealed class AppSettings
{
    public string ProxyAddress { get; set; } = "127.0.0.1:10808";

    public List<string> ProxyHistory { get; set; } = ["127.0.0.1:10808"];

    public string ProxyLink { get; set; } = string.Empty;

    public List<string> ProxyLinkHistory { get; set; } = [];

    public List<SavedProxyConfiguration> SavedProxyConfigs { get; set; } = [];

    public string? SelectedProxyConfigId { get; set; }

    public int LocalProxyPort { get; set; } = 10808;

    public bool IsLocalProxyActive { get; set; }

    public int PacPort { get; set; } = 1080;

    public List<int> PacPortHistory { get; set; } = [1080];

    public List<string> SelectedListIds { get; set; } = [];

    public List<string> CustomDomains { get; set; } = [];

    public List<string> CustomIps { get; set; } = [];

    public bool StartWithWindows { get; set; }

    public bool StartProxyWithApp { get; set; }

    public bool StartProcessModeWithApp { get; set; }

    public bool StartMinimizedToTray { get; set; }

    public bool NotifyOnMinimizeToTray { get; set; } = true;

    public bool UpdateListsOnStartup { get; set; } = true;

    public bool IsProxyActive { get; set; }

    public string? ActivePacHash { get; set; }

    public bool RouteAllTrafficThroughProxy { get; set; }

    public string ProcessModeLink { get; set; } = string.Empty;

    public string ProcessModeAmneziaSourceName { get; set; } = string.Empty;

    public ProcessModeConnectionType ProcessModeConnectionType { get; set; }

    public int ProcessModePort { get; set; } = 20808;

    public List<string> ProcessModeApplications { get; set; } = [];

    public bool IsProcessModeActive { get; set; }

    public bool IsBypassActive { get; set; }

    public bool BypassEnableZapret { get; set; } = true;

    public bool BypassEnableTelegram { get; set; } = true;

    public string? SavedZapretStrategy { get; set; }

    public int TgWsProxyPort { get; set; } = 1443;

    public string TgWsProxySecret { get; set; } = string.Empty;

    public bool StartBypassWithApp { get; set; }

    public bool RunAsAdministrator { get; set; }
}
