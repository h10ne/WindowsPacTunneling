using WPT.Core.Models;

namespace WPT.Core.Services;

public static class VpnConfigStorage
{
    public const string AwgProtocol = "awg";

    public static bool IsVpnProtocol(string? protocol) =>
        string.Equals(protocol, AwgProtocol, StringComparison.OrdinalIgnoreCase);

    public static bool TrySave(
        string configId,
        string wireGuardConfig,
        string sourceFileName,
        out AmneziaConfigSummary summary,
        out string error)
    {
        summary = new AmneziaConfigSummary();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(configId))
        {
            error = "Не указан идентификатор конфигурации";
            return false;
        }

        if (!AmneziaConfigParser.TryParse(wireGuardConfig, out wireGuardConfig, out error))
        {
            return false;
        }

        AppPaths.EnsureRoot();
        Directory.CreateDirectory(AppPaths.VpnConfigsDirectory);
        File.WriteAllText(AppPaths.VpnConfigFileFor(configId), wireGuardConfig);

        AmneziaConfigParser.TryGetSummary(wireGuardConfig, sourceFileName, out summary);
        return true;
    }

    public static bool TryRead(
        string configId,
        out string wireGuardConfig,
        out AmneziaConfigSummary summary,
        out string error)
    {
        wireGuardConfig = string.Empty;
        summary = new AmneziaConfigSummary();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(configId))
        {
            error = "Не указан идентификатор конфигурации";
            return false;
        }

        var path = AppPaths.VpnConfigFileFor(configId);
        if (!File.Exists(path))
        {
            error = "Файл конфигурации VPN не найден";
            return false;
        }

        try
        {
            wireGuardConfig = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            error = $"Не удалось прочитать конфигурацию ({ex.Message})";
            return false;
        }

        if (!AmneziaConfigParser.TryParse(wireGuardConfig, out wireGuardConfig, out error))
        {
            return false;
        }

        AmneziaConfigParser.TryGetSummary(wireGuardConfig, Path.GetFileName(path), out summary);
        return true;
    }

    public static bool Exists(string configId) =>
        !string.IsNullOrWhiteSpace(configId) && File.Exists(AppPaths.VpnConfigFileFor(configId));

    public static void Delete(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
        {
            return;
        }

        var path = AppPaths.VpnConfigFileFor(configId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static bool TryImportFromFile(string sourcePath, out string wireGuardConfig, out AmneziaConfigSummary summary, out string error)
    {
        wireGuardConfig = string.Empty;
        summary = new AmneziaConfigSummary();
        error = string.Empty;

        if (!AmneziaConfigParser.TryParseFile(sourcePath, out wireGuardConfig, out error))
        {
            return false;
        }

        AmneziaConfigParser.TryGetSummary(wireGuardConfig, Path.GetFileName(sourcePath), out summary);
        return true;
    }

}
