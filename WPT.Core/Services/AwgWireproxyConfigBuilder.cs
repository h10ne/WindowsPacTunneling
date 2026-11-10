using System.Text;
using System.Text.RegularExpressions;

namespace WPT.Core.Services;

public static partial class AwgWireproxyConfigBuilder
{
    /// <summary> Приводит конфиг AmneziaWG к формату, который принимает wireproxy </summary>
    /// <param name="wireGuardConfig"> Текст конфига AmneziaWG </param>
    // Клиент Amnezia экспортирует булевы параметры AWG 3.x как on/off, а wireproxy ждёт true/false
    public static string NormalizeWireGuardConfig(string wireGuardConfig) =>
        AwgBoolOptionRegex().Replace(
            wireGuardConfig,
            m => m.Groups[1].Value + (m.Groups[2].Value.Equals("on", StringComparison.OrdinalIgnoreCase) ? "true" : "false"));

    [GeneratedRegex(@"^(\s*(?:RandomTrailers|DisableCookies)\s*=\s*)(on|off)(?=\s*(?:#.*)?$)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex AwgBoolOptionRegex();

    public static string BuildForProcessMode(string wireGuardConfigPath, int localPort)
    {
        return Build(wireGuardConfigPath, localPort, useHttpProxy: false);
    }

    public static string BuildForProxy(string wireGuardConfigPath, int localPort)
    {
        return Build(wireGuardConfigPath, localPort, useHttpProxy: true);
    }

    private static string Build(string wireGuardConfigPath, int localPort, bool useHttpProxy)
    {
        if (string.IsNullOrWhiteSpace(wireGuardConfigPath))
        {
            throw new ArgumentException("Не указан путь к конфигурации WireGuard.", nameof(wireGuardConfigPath));
        }

        if (localPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort), "Некорректный локальный порт.");
        }

        var section = useHttpProxy ? "http" : "Socks5";
        var builder = new StringBuilder();
        builder.AppendLine($"WGConfig = {wireGuardConfigPath}");
        builder.AppendLine();
        builder.AppendLine($"[{section}]");
        builder.AppendLine($"BindAddress = 127.0.0.1:{localPort}");
        return builder.ToString();
    }

}
