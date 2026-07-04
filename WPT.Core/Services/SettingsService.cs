using System.Text.Json;
using System.Text.RegularExpressions;
using WPT.Core.Models;

namespace WPT.Core.Services;

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

        if (!root.TryGetProperty(nameof(AppSettings.UpdateListsOnStartup), out _))
        {
            settings.UpdateListsOnStartup = true;
        }

        if (root.TryGetProperty("ProcessModeAmneziaConfig", out var legacyAmneziaElement)
            && legacyAmneziaElement.ValueKind == JsonValueKind.String
            && !AmneziaConfigStorage.HasStoredConfig)
        {
            var legacyConfig = legacyAmneziaElement.GetString();
            if (!string.IsNullOrWhiteSpace(legacyConfig)
                && AmneziaConfigParser.TryParse(legacyConfig, out var wireGuardConfig, out _))
            {
                AppPaths.EnsureRoot();
                File.WriteAllText(AppPaths.ProcessModeAmneziaConfigFile, wireGuardConfig);
            }
        }

        MigrateLegacyProxyConfigs(settings);
    }

    private static void MigrateLegacyProxyConfigs(AppSettings settings)
    {
        if (settings.SavedProxyConfigs.Count > 0)
        {
            return;
        }

        var links = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.ProxyLink))
        {
            links.Add(settings.ProxyLink.Trim());
        }

        foreach (var link in settings.ProxyLinkHistory)
        {
            if (!string.IsNullOrWhiteSpace(link))
            {
                links.Add(link.Trim());
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in links)
        {
            if (!seen.Add(link))
            {
                continue;
            }

            if (!ProxyLinkParser.TryParse(link, out var profile, out _))
            {
                continue;
            }

            var config = new SavedProxyConfiguration
            {
                Name = BuildDefaultProxyConfigName(profile, settings.SavedProxyConfigs.Count + 1),
                Link = link,
                Protocol = profile.Protocol
            };
            settings.SavedProxyConfigs.Add(config);
        }

        if (settings.SavedProxyConfigs.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SelectedProxyConfigId))
        {
            var activeLink = settings.ProxyLink.Trim();
            settings.SelectedProxyConfigId = settings.SavedProxyConfigs
                .FirstOrDefault(x => x.Link.Equals(activeLink, StringComparison.OrdinalIgnoreCase))
                ?.Id
                ?? settings.SavedProxyConfigs[0].Id;
        }
    }

    private static string BuildDefaultProxyConfigName(ProxyProfile profile, int index)
    {
        if (!string.IsNullOrWhiteSpace(profile.Remark))
        {
            return profile.Remark.Trim();
        }

        return $"{profile.Server}:{profile.ServerPort} ({index})";
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
