using System.Text;

namespace WPT.Core.Services;

public static class AwgWireproxyConfigBuilder
{
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
