using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WPT.Core.Services;

public static partial class AmneziaBoxConfigBuilder
{
    public static string BuildForProcessMode(string wireGuardConfig, int localPort)
    {
        var iface = ParseSection(wireGuardConfig, "Interface");
        var peer = ParseSection(wireGuardConfig, "Peer");

        if (!iface.TryGetValue("PrivateKey", out var privateKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException("В конфигурации AmneziaWG не найден PrivateKey.");
        }

        if (!iface.TryGetValue("Address", out var address) || string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("В конфигурации AmneziaWG не найден Address.");
        }

        if (!peer.TryGetValue("PublicKey", out var publicKey) || string.IsNullOrWhiteSpace(publicKey))
        {
            throw new InvalidOperationException("В конфигурации AmneziaWG не найден PublicKey.");
        }

        if (!peer.TryGetValue("Endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("В конфигурации AmneziaWG не найден Endpoint.");
        }

        if (!TryParseEndpoint(endpoint, out var peerHost, out var peerPort))
        {
            throw new InvalidOperationException($"Некорректный Endpoint: {endpoint}");
        }

        var addressArray = new JsonArray();
        foreach (var item in address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            addressArray.Add(item);
        }

        var awgEndpoint = new JsonObject
        {
            ["type"] = "awg",
            ["tag"] = "awg-ep",
            ["private_key"] = privateKey.Trim(),
            ["address"] = addressArray
        };

        AddIntIfPresent(awgEndpoint, iface, "Jc", "jc");
        AddIntIfPresent(awgEndpoint, iface, "Jmin", "jmin");
        AddIntIfPresent(awgEndpoint, iface, "Jmax", "jmax");
        AddIntIfPresent(awgEndpoint, iface, "S1", "s1");
        AddIntIfPresent(awgEndpoint, iface, "S2", "s2");
        AddIntIfPresent(awgEndpoint, iface, "S3", "s3");
        AddIntIfPresent(awgEndpoint, iface, "S4", "s4");
        AddAwgStringIfPresent(awgEndpoint, iface, "H1", "h1");
        AddAwgStringIfPresent(awgEndpoint, iface, "H2", "h2");
        AddAwgStringIfPresent(awgEndpoint, iface, "H3", "h3");
        AddAwgStringIfPresent(awgEndpoint, iface, "H4", "h4");
        AddAwgStringIfPresent(awgEndpoint, iface, "I1", "i1");
        AddAwgStringIfPresent(awgEndpoint, iface, "I2", "i2");
        AddAwgStringIfPresent(awgEndpoint, iface, "I3", "i3");
        AddAwgStringIfPresent(awgEndpoint, iface, "I4", "i4");
        AddAwgStringIfPresent(awgEndpoint, iface, "I5", "i5");

        var peerObject = new JsonObject
        {
            ["address"] = peerHost,
            ["port"] = peerPort,
            ["public_key"] = publicKey.Trim()
        };

        if (peer.TryGetValue("PresharedKey", out var presharedKey) && !string.IsNullOrWhiteSpace(presharedKey))
        {
            peerObject["preshared_key"] = presharedKey.Trim();
        }

        if (peer.TryGetValue("AllowedIPs", out var allowedIps) && !string.IsNullOrWhiteSpace(allowedIps))
        {
            var allowedArray = new JsonArray();
            foreach (var item in allowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                allowedArray.Add(item);
            }

            peerObject["allowed_ips"] = allowedArray;
        }

        if (peer.TryGetValue("PersistentKeepalive", out var keepalive)
            && int.TryParse(keepalive.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var keepaliveValue)
            && keepaliveValue > 0)
        {
            peerObject["persistent_keepalive_interval"] = keepaliveValue;
        }

        awgEndpoint["peers"] = new JsonArray(peerObject);

        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["level"] = "warn" },
            ["dns"] = new JsonObject
            {
                ["servers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["tag"] = "remote",
                        ["type"] = "udp",
                        ["server"] = "8.8.8.8",
                        ["detour"] = "awg-ep"
                    }
                },
                ["final"] = "remote"
            },
            ["endpoints"] = new JsonArray(awgEndpoint),
            ["inbounds"] = new JsonArray(new JsonObject
            {
                ["type"] = "socks",
                ["tag"] = "socks-in",
                ["listen"] = "127.0.0.1",
                ["listen_port"] = localPort,
                ["udp_timeout"] = "5m"
            }),
            ["route"] = new JsonObject
            {
                ["final"] = "awg-ep",
                ["auto_detect_interface"] = false
            }
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, string> ParseSection(string config, string sectionName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var match = SectionRegex().Match(config);
        while (match.Success)
        {
            if (string.Equals(match.Groups[1].Value, sectionName, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var rawLine in match.Groups[2].Value.Split('\n', '\r'))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = line[..separatorIndex].Trim();
                    var value = line[(separatorIndex + 1)..].Trim();
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    result[key] = value;
                }

                break;
            }

            match = match.NextMatch();
        }

        return result;
    }

    private static bool TryParseEndpoint(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var trimmed = endpoint.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.StartsWith('['))
        {
            var closingIndex = trimmed.IndexOf(']');
            if (closingIndex <= 1 || closingIndex + 2 >= trimmed.Length || trimmed[closingIndex + 1] != ':')
            {
                return false;
            }

            host = trimmed[1..closingIndex];
            return int.TryParse(trimmed[(closingIndex + 2)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
                && port is > 0 and <= 65535;
        }

        var lastColon = trimmed.LastIndexOf(':');
        if (lastColon <= 0 || lastColon == trimmed.Length - 1)
        {
            return false;
        }

        host = trimmed[..lastColon];
        return int.TryParse(trimmed[(lastColon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
            && port is > 0 and <= 65535;
    }

    private static void AddIntIfPresent(
        JsonObject target,
        IReadOnlyDictionary<string, string> section,
        string sourceKey,
        string targetKey)
    {
        if (!section.TryGetValue(sourceKey, out var value))
        {
            return;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed != 0)
        {
            target[targetKey] = parsed;
        }
    }

    private static void AddAwgStringIfPresent(
        JsonObject target,
        IReadOnlyDictionary<string, string> section,
        string sourceKey,
        string targetKey)
    {
        if (!section.TryGetValue(sourceKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (IniKeyRegex().IsMatch(trimmed))
        {
            return;
        }

        target[targetKey] = trimmed;
    }

    [GeneratedRegex(@"\[(Interface|Peer)\]([^\[]*)", RegexOptions.IgnoreCase)]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]+\s*=")]
    private static partial Regex IniKeyRegex();

}
