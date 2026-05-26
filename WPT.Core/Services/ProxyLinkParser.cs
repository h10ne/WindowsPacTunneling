using System.Text;
using System.Text.Json;
using WPT.Core.Models;

namespace WPT.Core.Services;

public static class ProxyLinkParser
{
    public static bool TryParse(string input, out ProxyProfile profile, out string error)
    {
        profile = null!;
        error = string.Empty;

        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Укажите ссылку на прокси";
            return false;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            error = "Ссылка должна содержать протокол, например vless://";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "Некорректный формат ссылки";
            return false;
        }

        return uri.Scheme.ToLowerInvariant() switch
        {
            "vless" => TryParseVless(uri, out profile, out error),
            "vmess" => TryParseVmess(trimmed, out profile, out error),
            "trojan" => TryParseTrojan(uri, out profile, out error),
            "ss" => TryParseShadowsocks(trimmed, uri, out profile, out error),
            _ => Fail($"Протокол «{uri.Scheme}» не поддерживается", out profile, out error)
        };
    }

    private static bool TryParseVless(Uri uri, out ProxyProfile profile, out string error)
    {
        profile = null!;
        error = string.Empty;

        var uuid = uri.UserInfo;
        if (string.IsNullOrWhiteSpace(uuid))
        {
            error = "VLESS: отсутствует UUID";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "VLESS: не указан сервер";
            return false;
        }

        if (uri.Port is <= 0 or > 65535)
        {
            error = "VLESS: некорректный порт сервера";
            return false;
        }

        var query = ParseQuery(uri.Query);
        profile = new ProxyProfile
        {
            Protocol = "vless",
            Server = uri.Host,
            ServerPort = uri.Port,
            Remark = DecodeFragment(uri.Fragment),
            Uuid = uuid,
            Transport = GetQueryValue(query, "type") ?? "tcp",
            Security = GetQueryValue(query, "security") ?? "none",
            Flow = GetQueryValue(query, "flow"),
            Sni = GetQueryValue(query, "sni"),
            Fingerprint = GetQueryValue(query, "fp"),
            PublicKey = GetQueryValue(query, "pbk"),
            ShortId = GetQueryValue(query, "sid"),
            Host = GetQueryValue(query, "host"),
            Path = GetQueryValue(query, "path"),
            ServiceName = GetQueryValue(query, "serviceName") ?? GetQueryValue(query, "servicename"),
            Alpn = GetQueryValue(query, "alpn"),
            AllowInsecure = GetQueryValue(query, "allowInsecure") is "1" or "true"
        };

        return true;
    }

    private static bool TryParseVmess(string input, out ProxyProfile profile, out string error)
    {
        profile = null!;
        error = string.Empty;

        var payload = input["vmess://".Length..];
        var fragmentIndex = payload.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            payload = payload[..fragmentIndex];
        }

        payload = payload.Trim();
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2:
                payload += "==";
                break;
            case 3:
                payload += "=";
                break;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var server = GetJsonString(root, "add", "address");
            if (string.IsNullOrWhiteSpace(server))
            {
                error = "VMess: не указан сервер";
                return false;
            }

            if (!TryGetJsonInt(root, out var port, "port") || port is <= 0 or > 65535)
            {
                error = "VMess: некорректный порт сервера";
                return false;
            }

            var uuid = GetJsonString(root, "id");
            if (string.IsNullOrWhiteSpace(uuid))
            {
                error = "VMess: отсутствует UUID";
                return false;
            }

            var net = GetJsonString(root, "net", "type") ?? "tcp";
            var tls = GetJsonString(root, "tls");
            var security = tls is "tls" or "xtls" ? "tls" : "none";

            profile = new ProxyProfile
            {
                Protocol = "vmess",
                Server = server,
                ServerPort = port,
                Remark = GetJsonString(root, "ps", "remarks"),
                Uuid = uuid,
                Transport = net,
                Security = security,
                Host = GetJsonString(root, "host"),
                Path = GetJsonString(root, "path"),
                Sni = GetJsonString(root, "sni", "host"),
                AllowInsecure = GetJsonString(root, "verify_cert") is "0" or "false"
            };

            return true;
        }
        catch (Exception ex)
        {
            error = $"VMess: не удалось разобрать ссылку ({ex.Message})";
            return false;
        }
    }

    private static bool TryParseTrojan(Uri uri, out ProxyProfile profile, out string error)
    {
        profile = null!;
        error = string.Empty;

        var password = uri.UserInfo;
        if (string.IsNullOrWhiteSpace(password))
        {
            error = "Trojan: отсутствует пароль";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "Trojan: не указан сервер";
            return false;
        }

        if (uri.Port is <= 0 or > 65535)
        {
            error = "Trojan: некорректный порт сервера";
            return false;
        }

        var query = ParseQuery(uri.Query);
        profile = new ProxyProfile
        {
            Protocol = "trojan",
            Server = uri.Host,
            ServerPort = uri.Port,
            Remark = DecodeFragment(uri.Fragment),
            Password = password,
            Transport = GetQueryValue(query, "type") ?? "tcp",
            Security = "tls",
            Sni = GetQueryValue(query, "sni") ?? uri.Host,
            Host = GetQueryValue(query, "host"),
            Path = GetQueryValue(query, "path"),
            ServiceName = GetQueryValue(query, "serviceName"),
            AllowInsecure = GetQueryValue(query, "allowInsecure") is "1" or "true"
        };

        return true;
    }

    private static bool TryParseShadowsocks(string input, Uri uri, out ProxyProfile profile, out string error)
    {
        profile = null!;
        error = string.Empty;

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            if (uri.UserInfo.Contains(':', StringComparison.Ordinal))
            {
                var parts = uri.UserInfo.Split(':', 2);
                return TryBuildShadowsocksProfile(
                    parts[0],
                    parts[1],
                    uri.Host,
                    uri.Port,
                    DecodeFragment(uri.Fragment),
                    out profile,
                    out error);
            }

            if (TryDecodeBase64(Uri.UnescapeDataString(uri.UserInfo), out var sip002Credentials)
                && TrySplitShadowsocksCredentials(sip002Credentials, out var sip002Method, out var sip002Password)
                && TryParseShadowsocksEndpoint(uri.Host, uri.Port, out var sip002Server, out var sip002Port, out error))
            {
                return TryBuildShadowsocksProfile(
                    sip002Method,
                    sip002Password,
                    sip002Server,
                    sip002Port,
                    DecodeFragment(uri.Fragment),
                    out profile,
                    out error);
            }
        }

        var payload = input["ss://".Length..];
        var fragmentIndex = payload.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            payload = payload[..fragmentIndex];
        }

        var queryIndex = payload.IndexOf('?');
        if (queryIndex >= 0)
        {
            payload = payload[..queryIndex];
        }

        payload = payload.Trim();
        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "Shadowsocks: пустая ссылка";
            return false;
        }

        var atIndex = payload.IndexOf('@');
        if (atIndex > 0)
        {
            var encodedCredentials = payload[..atIndex];
            var endpoint = payload[(atIndex + 1)..];

            if (TryDecodeBase64(encodedCredentials, out var credentials)
                && TrySplitShadowsocksCredentials(credentials, out var sipMethod, out var sipPassword)
                && TryParseShadowsocksEndpoint(endpoint, 0, out var sipServer, out var sipPort, out error))
            {
                return TryBuildShadowsocksProfile(
                    sipMethod,
                    sipPassword,
                    sipServer,
                    sipPort,
                    DecodeFragment(uri.Fragment),
                    out profile,
                    out error);
            }
        }

        if (!TryDecodeBase64(payload, out var decoded))
        {
            error = "Shadowsocks: некорректная Base64-часть ссылки";
            return false;
        }

        return TryParseLegacyShadowsocksPayload(decoded, DecodeFragment(uri.Fragment), out profile, out error);
    }

    private static bool TryParseLegacyShadowsocksPayload(string decoded, string? remark, out ProxyProfile profile, out string error)
    {
        profile = null!;
        error = string.Empty;

        var atIndex = decoded.IndexOf('@');
        if (atIndex > 0)
        {
            var credentials = decoded[..atIndex];
            var endpoint = decoded[(atIndex + 1)..];

            if (TrySplitShadowsocksCredentials(credentials, out var legacyMethod, out var legacyPassword)
                && TryParseShadowsocksEndpoint(endpoint, 0, out var legacyServer, out var legacyPort, out error))
            {
                return TryBuildShadowsocksProfile(
                    legacyMethod,
                    legacyPassword,
                    legacyServer,
                    legacyPort,
                    remark,
                    out profile,
                    out error);
            }
        }

        var colonIndex = decoded.IndexOf(':');
        if (colonIndex <= 0)
        {
            error = "Shadowsocks: некорректный формат ссылки";
            return false;
        }

        var method = decoded[..colonIndex];
        var rest = decoded[(colonIndex + 1)..];
        atIndex = rest.IndexOf('@');
        if (atIndex <= 0)
        {
            error = "Shadowsocks: некорректный формат ссылки";
            return false;
        }

        var password = rest[..atIndex];
        var endpointPart = rest[(atIndex + 1)..];

        if (!TryParseShadowsocksEndpoint(endpointPart, 0, out var altServer, out var altPort, out error))
        {
            return false;
        }

        return TryBuildShadowsocksProfile(method, password, altServer, altPort, remark, out profile, out error);
    }

    private static bool TrySplitShadowsocksCredentials(string credentials, out string method, out string password)
    {
        method = string.Empty;
        password = string.Empty;

        var colonIndex = credentials.IndexOf(':');
        if (colonIndex <= 0 || colonIndex >= credentials.Length - 1)
        {
            return false;
        }

        method = credentials[..colonIndex];
        password = credentials[(colonIndex + 1)..];
        return !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(password);
    }

    private static bool TryParseShadowsocksEndpoint(string endpoint, int fallbackPort, out string server, out int port, out string error)
    {
        server = string.Empty;
        port = fallbackPort;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            error = "Shadowsocks: не указан сервер";
            return false;
        }

        if (endpoint.StartsWith('['))
        {
            var closingBracket = endpoint.IndexOf(']');
            if (closingBracket <= 1)
            {
                error = "Shadowsocks: некорректный IPv6-адрес";
                return false;
            }

            server = endpoint[1..closingBracket];
            if (closingBracket + 1 >= endpoint.Length || endpoint[closingBracket + 1] != ':')
            {
                error = "Shadowsocks: некорректный порт сервера";
                return false;
            }

            if (!int.TryParse(endpoint[(closingBracket + 2)..], out port) || port is <= 0 or > 65535)
            {
                error = "Shadowsocks: некорректный порт сервера";
                return false;
            }

            return true;
        }

        var lastColon = endpoint.LastIndexOf(':');
        if (lastColon <= 0 || lastColon >= endpoint.Length - 1)
        {
            error = "Shadowsocks: некорректный адрес сервера";
            return false;
        }

        server = endpoint[..lastColon];
        if (!int.TryParse(endpoint[(lastColon + 1)..], out port) || port is <= 0 or > 65535)
        {
            error = "Shadowsocks: некорректный порт сервера";
            return false;
        }

        return !string.IsNullOrWhiteSpace(server);
    }

    private static bool TryBuildShadowsocksProfile(
        string method,
        string password,
        string server,
        int port,
        string? remark,
        out ProxyProfile profile,
        out string error)
    {
        profile = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(password))
        {
            error = "Shadowsocks: отсутствует метод или пароль";
            return false;
        }

        if (string.IsNullOrWhiteSpace(server))
        {
            error = "Shadowsocks: не указан сервер";
            return false;
        }

        if (port is <= 0 or > 65535)
        {
            error = "Shadowsocks: некорректный порт сервера";
            return false;
        }

        profile = new ProxyProfile
        {
            Protocol = "ss",
            Server = server,
            ServerPort = port,
            Remark = remark,
            Method = method,
            Password = password
        };

        return true;
    }

    private static bool TryDecodeBase64(string input, out string decoded)
    {
        decoded = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(NormalizeBase64(input.Trim())));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var trimmed = query.TrimStart('?');
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string? GetQueryValue(IReadOnlyDictionary<string, string> query, string key, string? defaultValue = null) =>
        query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;

    private static string? DecodeFragment(string fragment) =>
        string.IsNullOrWhiteSpace(fragment) ? null : Uri.UnescapeDataString(fragment.TrimStart('#'));

    private static string? GetJsonString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool TryGetJsonInt(JsonElement root, out int value, params string[] names)
    {
        value = 0;
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String
                && int.TryParse(property.GetString(), out value))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeBase64(string payload)
    {
        payload = payload.Replace('-', '+').Replace('_', '/');
        return (payload.Length % 4) switch
        {
            2 => payload + "==",
            3 => payload + "=",
            _ => payload
        };
    }

    private static bool Fail(string message, out ProxyProfile profile, out string error)
    {
        profile = null!;
        error = message;
        return false;
    }
}
