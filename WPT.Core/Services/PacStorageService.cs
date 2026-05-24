using System.Text;

namespace WPT.Core.Services;

public static class PacStorageService
{
    public static async Task SaveAsync(string content)
    {
        AppPaths.EnsureRoot();
        var tempPath = AppPaths.PacFile + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Move(tempPath, AppPaths.PacFile, overwrite: true);
    }

    public static bool TryRead(out string content)
    {
        content = string.Empty;

        if (!File.Exists(AppPaths.PacFile))
        {
            return false;
        }

        content = File.ReadAllText(AppPaths.PacFile);
        return true;
    }
}
