using System.Text;
using System.Text.RegularExpressions;

namespace WPT.Core.Services.Bypass;

internal static class ZapretStrategyArgumentParser
{
    public static string ExtractWinWsArguments(string strategyBatPath)
    {
        if (!File.Exists(strategyBatPath))
        {
            throw new FileNotFoundException("Не найден bat-файл стратегии zapret.", strategyBatPath);
        }

        var zapretDir = AppPaths.ZapretDirectory;
        var binPath = AppPaths.ZapretBinDirectory + Path.DirectorySeparatorChar;
        var listsPath = Path.Combine(zapretDir, "lists") + Path.DirectorySeparatorChar;
        var gameFilter = ReadGameFilter();

        var joined = JoinBatContinuations(File.ReadAllLines(strategyBatPath));
        var match = Regex.Match(
            joined,
            @"winws\.exe(?<args>.*)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
        {
            throw new InvalidOperationException($"В {Path.GetFileName(strategyBatPath)} не найден вызов winws.exe.");
        }

        var args = match.Groups["args"].Value.Trim();
        if (args.StartsWith('"'))
        {
            args = args[1..].TrimStart();
        }

        args = args.Replace("%BIN%", binPath, StringComparison.OrdinalIgnoreCase);
        args = args.Replace("%LISTS%", listsPath, StringComparison.OrdinalIgnoreCase);
        args = args.Replace("%GameFilterTCP%", gameFilter.Tcp, StringComparison.OrdinalIgnoreCase);
        args = args.Replace("%GameFilterUDP%", gameFilter.Udp, StringComparison.OrdinalIgnoreCase);
        args = args.Replace("%GameFilter%", gameFilter.Tcp, StringComparison.OrdinalIgnoreCase);
        args = Regex.Replace(args, @"\s+", " ").Trim();

        return args;
    }

    private static string JoinBatContinuations(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        var pending = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("::", StringComparison.Ordinal))
            {
                continue;
            }

            if (pending.Length > 0)
            {
                line = pending + line;
                pending = string.Empty;
            }

            if (line.EndsWith('^'))
            {
                pending = line[..^1];
                continue;
            }

            builder.Append(' ');
            builder.Append(line);
        }

        if (pending.Length > 0)
        {
            builder.Append(' ');
            builder.Append(pending);
        }

        return builder.ToString().Trim();
    }

    private static (string Tcp, string Udp) ReadGameFilter()
    {
        var flagFile = Path.Combine(AppPaths.ZapretDirectory, "utils", "game_filter.enabled");
        if (!File.Exists(flagFile))
        {
            return ("12", "12");
        }

        return File.ReadAllText(flagFile).Trim().ToLowerInvariant() switch
        {
            "all" => ("1024-65535", "1024-65535"),
            "tcp" => ("1024-65535", "12"),
            "udp" => ("12", "1024-65535"),
            _ => ("1024-65535", "1024-65535")
        };
    }
}
