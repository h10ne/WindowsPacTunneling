using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WPT.Core.Models;

namespace WPT.Core.Services;

public static partial class AmneziaConfigParser
{
    public static bool TryParse(string input, out string wireGuardConfig, out string error)
    {
        wireGuardConfig = string.Empty;
        error = string.Empty;

        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Выберите файл конфигурации Amnezia";
            return false;
        }

        if (trimmed.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseVpnLink(trimmed, out wireGuardConfig, out error);
        }

        if (trimmed.StartsWith('{'))
        {
            return TryParseJsonRoot(trimmed, out wireGuardConfig, out error);
        }

        if (ContainsWireGuardSection(trimmed))
        {
            wireGuardConfig = trimmed;
            return ValidateWireGuardConfig(wireGuardConfig, out error);
        }

        error = "Неподдерживаемый формат. Выберите файл .conf AmneziaWG";
        return false;
    }

    public static bool TryParseFile(string path, out string wireGuardConfig, out string error)
    {
        wireGuardConfig = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Не указан файл конфигурации";
            return false;
        }

        if (!File.Exists(path))
        {
            error = "Файл конфигурации не найден";
            return false;
        }

        try
        {
            return TryParse(File.ReadAllText(path), out wireGuardConfig, out error);
        }
        catch (Exception ex)
        {
            error = $"Не удалось прочитать файл ({ex.Message})";
            return false;
        }
    }

    public static bool TryGetEndpoint(string wireGuardConfig, out string endpoint)
    {
        endpoint = string.Empty;
        if (string.IsNullOrWhiteSpace(wireGuardConfig))
        {
            return false;
        }

        var match = EndpointRegex().Match(wireGuardConfig);
        if (!match.Success)
        {
            return false;
        }

        endpoint = match.Groups[1].Value.Trim();
        return !string.IsNullOrWhiteSpace(endpoint);
    }

    public static bool TryGetSummary(string wireGuardConfig, string sourceFileName, out AmneziaConfigSummary summary)
    {
        summary = new AmneziaConfigSummary
        {
            SourceFileName = sourceFileName,
            Endpoint = TryGetEndpoint(wireGuardConfig, out var endpoint) ? endpoint : "—"
        };

        return true;
    }

    private static bool TryParseVpnLink(string input, out string wireGuardConfig, out string error)
    {
        wireGuardConfig = string.Empty;
        error = string.Empty;

        var encoded = input["vpn://".Length..].Trim();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            error = "Пустая vpn:// ссылка";
            return false;
        }

        try
        {
            var padding = (4 - encoded.Length % 4) % 4;
            encoded += new string('=', padding);
            var compressed = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/'));

            string json;
            if (compressed.Length > 4)
            {
                try
                {
                    using var stream = new MemoryStream(compressed, 4, compressed.Length - 4);
                    using var zlib = new ZLibStream(stream, CompressionMode.Decompress);
                    using var reader = new StreamReader(zlib, Encoding.UTF8);
                    json = reader.ReadToEnd();
                }
                catch (InvalidDataException)
                {
                    json = Encoding.UTF8.GetString(compressed);
                }
            }
            else
            {
                json = Encoding.UTF8.GetString(compressed);
            }

            return TryParseJsonRoot(json, out wireGuardConfig, out error);
        }
        catch (Exception ex)
        {
            error = $"Не удалось разобрать vpn:// ссылку ({ex.Message})";
            return false;
        }
    }

    private static bool TryParseJsonRoot(string json, out string wireGuardConfig, out string error)
    {
        wireGuardConfig = string.Empty;
        error = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("api_endpoint", out _))
            {
                error = "Этот vpn:// ключ требует получения конфигурации с сервера. Экспортируйте готовый конфиг AmneziaWG из приложения Amnezia (формат «AmneziaWG native» или vpn:// с полной конфигурацией).";
                return false;
            }

            if (!root.TryGetProperty("containers", out var containers) || containers.ValueKind != JsonValueKind.Array)
            {
                error = "В конфигурации Amnezia не найден блок containers";
                return false;
            }

            foreach (var container in containers.EnumerateArray())
            {
                if (!container.TryGetProperty("awg", out var awg))
                {
                    continue;
                }

                if (!TryExtractWireGuardFromAwgContainer(root, awg, out wireGuardConfig, out error))
                {
                    continue;
                }

                return ValidateWireGuardConfig(wireGuardConfig, out error);
            }

            error = "В конфигурации Amnezia не найден контейнер AmneziaWG";
            return false;
        }
        catch (JsonException ex)
        {
            error = $"Некорректный JSON конфигурации Amnezia ({ex.Message})";
            return false;
        }
    }

    private static bool TryExtractWireGuardFromAwgContainer(
        JsonElement root,
        JsonElement awg,
        out string wireGuardConfig,
        out string error)
    {
        wireGuardConfig = string.Empty;
        error = string.Empty;

        if (!awg.TryGetProperty("last_config", out var lastConfigElement))
        {
            error = "В контейнере awg отсутствует last_config";
            return false;
        }

        var lastConfigJson = lastConfigElement.ValueKind == JsonValueKind.String
            ? lastConfigElement.GetString()
            : lastConfigElement.GetRawText();

        if (string.IsNullOrWhiteSpace(lastConfigJson))
        {
            error = "Пустой last_config в контейнере awg";
            return false;
        }

        using var lastConfigDocument = JsonDocument.Parse(lastConfigJson);
        var lastConfig = lastConfigDocument.RootElement;

        if (!lastConfig.TryGetProperty("config", out var configElement)
            || configElement.ValueKind != JsonValueKind.String)
        {
            error = "В last_config отсутствует поле config";
            return false;
        }

        var config = configElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(config))
        {
            error = "Пустой WireGuard конфиг в last_config";
            return false;
        }

        var primaryDns = GetJsonString(root, "dns1") ?? "1.1.1.1";
        var secondaryDns = GetJsonString(root, "dns2") ?? "1.0.0.1";
        config = config
            .Replace("$PRIMARY_DNS", primaryDns, StringComparison.Ordinal)
            .Replace("$SECONDARY_DNS", secondaryDns, StringComparison.Ordinal);

        var clientPrivateKey = GetJsonString(lastConfig, "client_priv_key", "clientPrivateKey");
        if (!string.IsNullOrWhiteSpace(clientPrivateKey))
        {
            config = config.Replace("$WIREGUARD_CLIENT_PRIVATE_KEY", clientPrivateKey, StringComparison.Ordinal);
        }

        if (lastConfig.TryGetProperty("mtu", out var mtuElement)
            && mtuElement.TryGetInt32(out var mtu)
            && mtu > 0)
        {
            config = UpsertIniValue(config, "Interface", "MTU", mtu.ToString());
        }

        if (lastConfig.TryGetProperty("port", out var portElement)
            && portElement.TryGetInt32(out var port)
            && port is > 0 and <= 65535)
        {
            config = UpsertIniValue(config, "Interface", "ListenPort", port.ToString());
        }

        wireGuardConfig = config.Trim();
        return true;
    }

    private static bool ValidateWireGuardConfig(string config, out string error)
    {
        error = string.Empty;

        if (!ContainsWireGuardSection(config))
        {
            error = "Конфигурация WireGuard должна содержать секцию [Interface]";
            return false;
        }

        if (!WireGuardKeyRegex().IsMatch(config))
        {
            error = "В конфигурации WireGuard не найден PrivateKey";
            return false;
        }

        if (!WireGuardPeerRegex().IsMatch(config))
        {
            error = "В конфигурации WireGuard не найдена секция [Peer] с PublicKey";
            return false;
        }

        if (config.Contains("$WIREGUARD_CLIENT_PRIVATE_KEY", StringComparison.Ordinal))
        {
            error = "В конфигурации остался незаполненный PrivateKey. Экспортируйте готовый .conf из Amnezia.";
            return false;
        }

        return true;
    }

    private static bool ContainsWireGuardSection(string text) =>
        text.Contains("[Interface]", StringComparison.OrdinalIgnoreCase);

    private static string UpsertIniValue(string config, string section, string key, string value)
    {
        var sectionPattern = $@"(\[{Regex.Escape(section)}\][^\[]*)";
        var match = Regex.Match(config, sectionPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return config;
        }

        var sectionBody = match.Groups[1].Value;
        var keyPattern = $@"(?m)^\s*{Regex.Escape(key)}\s*=.*$";
        string updatedSection;
        if (Regex.IsMatch(sectionBody, keyPattern, RegexOptions.IgnoreCase))
        {
            updatedSection = Regex.Replace(sectionBody, keyPattern, $"{key} = {value}", RegexOptions.IgnoreCase);
        }
        else
        {
            updatedSection = sectionBody.TrimEnd() + Environment.NewLine + $"{key} = {value}" + Environment.NewLine;
        }

        return config[..match.Groups[1].Index] + updatedSection + config[(match.Groups[1].Index + match.Groups[1].Length)..];
    }

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

    [GeneratedRegex(@"(?im)^\s*Endpoint\s*=\s*(\S+)\s*$", RegexOptions.None)]
    private static partial Regex EndpointRegex();

    [GeneratedRegex(@"(?im)^\s*PrivateKey\s*=", RegexOptions.None)]
    private static partial Regex WireGuardKeyRegex();

    [GeneratedRegex(@"(?im)\[Peer\][\s\S]*?^\s*PublicKey\s*=", RegexOptions.None)]
    private static partial Regex WireGuardPeerRegex();

}
