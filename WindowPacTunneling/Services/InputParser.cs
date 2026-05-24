using System.Text.RegularExpressions;

namespace WindowPacTunneling.Services;

public static partial class InputParser
{
    public static IReadOnlyList<string> ParseQuotedList(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        return QuotedItemRegex()
            .Matches(input)
            .Select(x => x.Groups[1].Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool TryParseProxyAddress(string input, out string host, out int port, out string error)
    {
        host = string.Empty;
        port = 0;
        error = string.Empty;

        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Укажите адрес прокси-сервера";
            return false;
        }

        var lastColon = trimmed.LastIndexOf(':');
        if (lastColon <= 0 || lastColon == trimmed.Length - 1)
        {
            error = "Адрес прокси должен быть в формате host:port";
            return false;
        }

        host = trimmed[..lastColon].Trim();
        var portText = trimmed[(lastColon + 1)..].Trim();

        if (!int.TryParse(portText, out port) || port is < 1 or > 65535)
        {
            error = "Некорректный порт прокси-сервера";
            return false;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Укажите хост прокси-сервера";
            return false;
        }

        return true;
    }

    public static bool TryParsePort(string input, out int port, out string error)
    {
        port = 0;
        error = string.Empty;

        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Укажите порт PAC-сервера";
            return false;
        }

        if (!int.TryParse(trimmed, out port) || port is < 1 or > 65535)
        {
            error = "Некорректный порт PAC-сервера";
            return false;
        }

        return true;
    }

    [GeneratedRegex("\"([^\"]+)\"")]
    private static partial Regex QuotedItemRegex();
}
