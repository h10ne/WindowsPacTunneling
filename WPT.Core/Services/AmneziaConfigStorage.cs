using WPT.Core.Models;

namespace WPT.Core.Services;

public static class AmneziaConfigStorage
{
    public static string StoredConfigPath => AppPaths.ProcessModeAmneziaConfigFile;

    public static bool HasStoredConfig => File.Exists(StoredConfigPath);

    public static bool TryImportFromFile(string sourcePath, out AmneziaConfigSummary summary, out string error)
    {
        summary = new AmneziaConfigSummary();
        error = string.Empty;

        if (!AmneziaConfigParser.TryParseFile(sourcePath, out var wireGuardConfig, out error))
        {
            return false;
        }

        AppPaths.EnsureRoot();
        File.WriteAllText(StoredConfigPath, wireGuardConfig);

        AmneziaConfigParser.TryGetSummary(wireGuardConfig, Path.GetFileName(sourcePath), out summary);
        return true;
    }

    public static bool TryReadStored(out string wireGuardConfig, out AmneziaConfigSummary summary, out string error)
    {
        wireGuardConfig = string.Empty;
        summary = new AmneziaConfigSummary();
        error = string.Empty;

        if (!HasStoredConfig)
        {
            error = "Конфигурация Amnezia не выбрана";
            return false;
        }

        try
        {
            wireGuardConfig = File.ReadAllText(StoredConfigPath);
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

        AmneziaConfigParser.TryGetSummary(wireGuardConfig, string.Empty, out summary);
        return true;
    }

    public static void ClearStoredConfig()
    {
        if (File.Exists(StoredConfigPath))
        {
            File.Delete(StoredConfigPath);
        }
    }

}
