using System.Text.Json;
using System.Text.RegularExpressions;
using WindowPacTunneling.Models;

namespace WindowPacTunneling.Services;

public static partial class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            AppPaths.EnsureRoot();

            if (!File.Exists(AppPaths.SettingsFile))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(AppPaths.SettingsFile);
            using var document = JsonDocument.Parse(json);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            MigrateLegacyFields(document.RootElement, settings);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        AppPaths.EnsureRoot();

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, AppPaths.SettingsFile, overwrite: true);
    }

    private static void MigrateLegacyFields(JsonElement root, AppSettings settings)
    {
        if (root.TryGetProperty("CustomDomains", out var domainsElement)
            && domainsElement.ValueKind == JsonValueKind.String
            && settings.CustomDomains.Count == 0)
        {
            var legacyDomains = domainsElement.GetString();
            if (!string.IsNullOrWhiteSpace(legacyDomains))
            {
                settings.CustomDomains = ParseLegacyQuotedList(legacyDomains);
            }
        }

        if (root.TryGetProperty("CustomIps", out var ipsElement)
            && ipsElement.ValueKind == JsonValueKind.String
            && settings.CustomIps.Count == 0)
        {
            var legacyIps = ipsElement.GetString();
            if (!string.IsNullOrWhiteSpace(legacyIps))
            {
                settings.CustomIps = ParseLegacyQuotedList(legacyIps);
            }
        }

        if (!root.TryGetProperty(nameof(AppSettings.NotifyOnMinimizeToTray), out _))
        {
            settings.NotifyOnMinimizeToTray = true;
        }
    }

    private static List<string> ParseLegacyQuotedList(string input) =>
        QuotedItemRegex()
            .Matches(input)
            .Select(x => x.Groups[1].Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

    [GeneratedRegex("\"([^\"]+)\"")]
    private static partial Regex QuotedItemRegex();
}
