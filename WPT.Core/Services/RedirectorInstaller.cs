namespace WPT.Core.Services;

public static class RedirectorInstaller
{
    private static readonly string[] RequiredFiles =
    [
        "Redirector.bin",
        "nfapi.dll",
        "nfdriver.sys"
    ];

    public static bool IsInstalled() =>
        RequiredFiles.All(file => File.Exists(Path.Combine(AppPaths.BinDirectory, file)));

    public static void EnsureInstalled(IProgress<string>? progress)
    {
        if (IsInstalled())
        {
            return;
        }

        progress?.Report("Установка Redirector...");
        AppPaths.EnsureRoot();
        Directory.CreateDirectory(AppPaths.BinDirectory);

        var sourceDirectory = AppPaths.BundledRedirectorDirectory;
        if (!Directory.Exists(sourceDirectory))
        {
            throw new FileNotFoundException(
                "Не найдены файлы Redirector в составе приложения.",
                sourceDirectory);
        }

        foreach (var file in RequiredFiles)
        {
            var source = Path.Combine(sourceDirectory, file);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"Не найден файл {file}.", source);
            }

            var target = Path.Combine(AppPaths.BinDirectory, file);
            File.Copy(source, target, overwrite: true);
        }
    }

}
