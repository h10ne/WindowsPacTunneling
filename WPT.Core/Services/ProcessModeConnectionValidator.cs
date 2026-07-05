using WPT.Core.Models;

namespace WPT.Core.Services;

public static class ProcessModeConnectionValidator
{
    public static bool TryValidate(
        ProcessModeConnectionType connectionType,
        string link,
        out ProxyProfile? profile,
        out string error)
    {
        profile = null;
        error = string.Empty;

        return connectionType switch
        {
            ProcessModeConnectionType.Shadowsocks => TryValidateShadowsocks(link, out profile, out error),
            ProcessModeConnectionType.Vless => TryValidateVless(link, out profile, out error),
            ProcessModeConnectionType.Amnezia => Fail("Используйте импорт .conf для Amnezia", out error),
            _ => Fail("Неподдерживаемый тип подключения Process Mode", out error)
        };
    }

    private static bool TryValidateShadowsocks(string link, out ProxyProfile? profile, out string error)
    {
        profile = null;
        error = string.Empty;

        if (!ProxyLinkParser.TryParse(link, out var parsed, out error))
        {
            return false;
        }

        if (!string.Equals(parsed.Protocol, "ss", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Process Mode: ожидается ссылка ss://", out error);
        }

        profile = parsed;
        return true;
    }

    private static bool TryValidateVless(string link, out ProxyProfile? profile, out string error)
    {
        profile = null;
        error = string.Empty;

        if (!ProxyLinkParser.TryParse(link, out var parsed, out error))
        {
            return false;
        }

        if (!string.Equals(parsed.Protocol, "vless", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Process Mode: ожидается ссылка vless://", out error);
        }

        if (!string.Equals(parsed.Security, "reality", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Process Mode VLESS: нужен security=reality (Reality inbound в 3x-ui)", out error);
        }

        if (string.IsNullOrWhiteSpace(parsed.PublicKey))
        {
            return Fail("Process Mode VLESS: в ссылке отсутствует pbk (Public Key Reality)", out error);
        }

        if (string.IsNullOrWhiteSpace(parsed.ShortId))
        {
            return Fail("Process Mode VLESS: в ссылке отсутствует sid (Short ID Reality)", out error);
        }

        if (string.IsNullOrWhiteSpace(parsed.Sni))
        {
            return Fail("Process Mode VLESS: в ссылке отсутствует sni (Dest / Server Name)", out error);
        }

        if (!string.Equals(parsed.Transport, "tcp", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Process Mode VLESS: transport должен быть tcp (не ws/grpc/xhttp)", out error);
        }

        if (string.IsNullOrWhiteSpace(parsed.Flow))
        {
            return Fail("Process Mode VLESS: укажите flow=xtls-rprx-vision (или xtls-rprx-vision-udp443) у клиента в 3x-ui", out error);
        }

        profile = parsed;
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

}
