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

        string method;
        string password;
        string server;
        int port;

        if (!string.IsNullOrEmpty(uri.UserInfo) && uri.UserInfo.Contains(':', StringComparison.Ordinal))
        {
            var parts = uri.UserInfo.Split(':', 2);
            method = parts[0];
            password = parts[1];
            server = uri.Host;
            port = uri.Port;
        }
        else
        {
            var payload = input["ss://".Length..];
            var fragmentIndex = payload.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                payload = payload[..fragmentIndex];
            }

            payload = payload.Trim();
            if (payload.Contains('@'))
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(NormalizeBase64(payload)));
                var atIndex = decoded.LastIndexOf('@');
                if (atIndex <= 0)
                {
                    error = "Shadowsocks: некорректный формат ссылки";
                    return false;
                }

                var credentials = decoded[..atIndex];
                var endpoint = decoded[(atIndex + 1)..];
                var colonIndex = credentials.IndexOf(':');
                if (colonIndex <= 0)
                {
                    error = "Shadowsocks: некорректный формат учётных данных";
                    return false;
                }

                method = credentials[..colonIndex];
                password = credentials[(colonIndex + 1)..];
                var lastColon = endpoint.LastIndexOf(':');
                if (lastColon <= 0)
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
            }
            else
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(NormalizeBase64(payload)));
                var firstAt = decoded.IndexOf('@');
                if (firstAt > 0)
                {
                    var credentials = decoded[..firstAt];
                    var endpoint = decoded[(firstAt + 1)..];
                    var colonIndex = credentials.IndexOf(':');
                    method = credentials[..colonIndex];
                    password = credentials[(colonIndex + 1)..];
                    var lastColon = endpoint.LastIndexOf(':');
                    server = endpoint[..lastColon];
                    port = int.Parse(endpoint[(lastColon + 1)..]);
                }
                else
                {
                    var colonIndex = decoded.IndexOf(':');
                    if (colonIndex <= 0)
                    {
                        error = "Shadowsocks: некорректный формат ссылки";
                        return false;
                    }

                    method = decoded[..colonIndex];
                    var rest = decoded[(colonIndex + 1)..];
                    var atIndex = rest.IndexOf('@');
                    password = rest[..atIndex];
                    var endpoint = rest[(atIndex + 1)..];
                    var lastColon = endpoint.LastIndexOf(':');
                    server = endpoint[..lastColon];
                    port = int.Parse(endpoint[(lastColon + 1)..]);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(password))
        {
            error = "Shadowsocks: отсутствует метод или пароль";
            return false;
        }

        profile = new ProxyProfile
        {
            Protocol = "ss",
            Server = server,
            ServerPort = port,
            Remark = DecodeFragment(uri.Fragment),
            Method = method,
            Password = password
        };

        return true;
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
