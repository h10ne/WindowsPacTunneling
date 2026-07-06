namespace WPT.Core.Services;

public static class AppPaths
{
    public static string Root =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowPacTunneling");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string ListsDirectory => Path.Combine(Root, "lists");

    public static string PacFile => Path.Combine(Root, "proxy.pac");

    public static string BinDirectory => Path.Combine(Root, "bin");

    public static string SingBoxVersionFile => Path.Combine(BinDirectory, "sing-box-version.txt");

    public static string ProcessModeSingBoxExecutable => Path.Combine(BinDirectory, "sing-box-pm.exe");

    public static string ProcessModeSingBoxVersionFile => Path.Combine(BinDirectory, "sing-box-pm-version.txt");

    public static string AwgWireproxyVersionFile => Path.Combine(BinDirectory, "awg-wireproxy-version.txt");

    public static string AwgWireproxyCapabilityFile => Path.Combine(BinDirectory, "awg-wireproxy.capability");

    public static string VpnConfigsDirectory => Path.Combine(Root, "vpn-configs");

    public static string VpnConfigFileFor(string configId) =>
        Path.Combine(VpnConfigsDirectory, $"{configId}.conf");

    public static string SingBoxConfigFile => Path.Combine(Root, "sing-box.json");

    public static string SingBoxPidFile => Path.Combine(Root, "sing-box.pid");

    public static string SingBoxConfigFileFor(string instanceName) =>
        Path.Combine(Root, $"sing-box-{instanceName}.json");

    public static string SingBoxPidFileFor(string instanceName) =>
        Path.Combine(Root, $"sing-box-{instanceName}.pid");

    public static string WireGuardConfigFileFor(string instanceName) =>
        Path.Combine(Root, $"wg-{instanceName}.conf");

    public static string AwgProxyConfigFileFor(string instanceName) =>
        Path.Combine(Root, $"awgproxy-{instanceName}.conf");

    public static string AwgProxyPidFileFor(string instanceName) =>
        Path.Combine(Root, $"awgproxy-{instanceName}.pid");

    public static string ProcessModeAmneziaConfigFile => Path.Combine(Root, "amnezia-processmode.conf");

    public static string RedirectorBinary => Path.Combine(BinDirectory, "Redirector.bin");

    public static string RedirectorApiDll => Path.Combine(BinDirectory, "nfapi.dll");

    public static string NetFilterDriver => Path.Combine(BinDirectory, "nfdriver.sys");

    public static string BundledRedirectorDirectory =>
        Path.Combine(AppContext.BaseDirectory, "ThirdParty", "Netch");

    public static string ScriptsDirectory => Path.Combine(Root, "scripts");

    public static string ZapretDirectory => Path.Combine(Root, "zapret");

    public static string ZapretBinDirectory => Path.Combine(ZapretDirectory, "bin");

    public static string ZapretVersionFile => Path.Combine(ZapretDirectory, "version.txt");

    public static string TgWsProxySecretFile => Path.Combine(Root, "tg-ws-proxy-secret.txt");

    public static string LogsDirectory => Path.Combine(Root, "logs");

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ListsDirectory);
        Directory.CreateDirectory(BinDirectory);
        Directory.CreateDirectory(VpnConfigsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ScriptsDirectory);
    }


}
