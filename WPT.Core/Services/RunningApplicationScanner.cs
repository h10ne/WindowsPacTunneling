using System.Diagnostics;

namespace WPT.Core.Services;

/// <summary> Запущенное пользовательское приложение </summary>
/// <param name="FileName"> Имя exe, например Discord.exe </param>
/// <param name="Path"> Полный путь к exe </param>
public sealed record RunningApplication(string FileName, string Path);

public static class RunningApplicationScanner
{
    /// <summary> Приложения текущей сессии, уникальные по имени exe, без компонентов Windows и самого WPT </summary>
    public static IReadOnlyList<RunningApplication> GetUserApplications()
    {
        var sessionId = Process.GetCurrentProcess().SessionId;
        var applications = new Dictionary<string, RunningApplication>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                // Службы живут в сессии 0, чужие пользователи — в своих сессиях
                if (process.SessionId != sessionId)
                {
                    continue;
                }

                var path = ProcessPathHelper.TryGetExecutablePath(process);
                if (path == null || IsSystemOrOwn(path))
                {
                    continue;
                }

                var fileName = System.IO.Path.GetFileName(path);
                applications.TryAdd(fileName, new RunningApplication(fileName, path));
            }
        }

        return applications.Values
            .OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ponytail: «системность» по расположению exe. Отсекаются и Store-приложения Microsoft (Windows Terminal и т. п.) —
    // их можно вписать вручную
    private static bool IsSystemOrOwn(string path) =>
        IsUnder(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows))
        || IsUnder(path, System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows "))
        || IsUnder(path, System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows "))
        || path.Contains(@"\WindowsApps\Microsoft", StringComparison.OrdinalIgnoreCase)
        || IsUnder(path, AppPaths.Root)
        || string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string path, string prefix) =>
        !string.IsNullOrEmpty(prefix) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
