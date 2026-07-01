namespace WPT.Core.Services.Bypass;

public static class ZapretDirectoryCleanup
{
    public static void DiscardLegacyInstallations()
    {
        foreach (var directory in EnumerateLegacyDirectories())
        {
            if (!TryDeleteDirectory(directory))
            {
                TryRenameAbandonedDirectory(directory);
            }
        }
    }

    public static IReadOnlyList<string> EnumerateLegacyDirectories()
    {
        if (!Directory.Exists(AppPaths.Root))
        {
            return [];
        }

        return Directory
            .GetDirectories(AppPaths.Root, "zapret.old*", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return true;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                }
            }

            Directory.Delete(path, recursive: true);
            return !Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static void TryRenameAbandonedDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var abandonedPath = $"{path}.abandoned-{DateTime.UtcNow.Ticks}";
        try
        {
            Directory.Move(path, abandonedPath);
        }
        catch
        {
        }
    }
}
