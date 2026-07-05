using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace WPT.Core.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowPacTunneling";
    private const string TaskName = "WindowPacTunneling";
    private const string ElevatedArg = "--elevated";

    private static readonly Encoding SchtasksOutputEncoding = Encoding.GetEncoding(866);

    public static bool IsEnabled()
    {
        if (HasScheduledTask())
        {
            return true;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static void SetEnabled(bool enabled, bool runAsAdministrator = false)
    {
        if (enabled && runAsAdministrator && !AdminHelper.IsRunningAsAdmin())
        {
            throw new InvalidOperationException(
                "Автозапуск от имени администратора можно включить только из WPT, запущенного от имени администратора.");
        }

        ClearRegistryRun();
        ClearScheduledTask();

        if (!enabled)
        {
            return;
        }

        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу");

        if (runAsAdministrator)
        {
            CreateLogonTask(exePath);
            return;
        }

        SetRegistryRun(exePath);
    }

    private static void SetRegistryRun(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Не удалось открыть ключ автозагрузки Windows");

        key.SetValue(ValueName, $"\"{exePath}\"");
    }

    private static void ClearRegistryRun()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static void CreateLogonTask(string exePath)
    {
        var taskCommand = $"\"{exePath}\" {ElevatedArg}";
        RunSchtasks($"/Create /TN \"{TaskName}\" /TR \"{taskCommand}\" /SC ONLOGON /RL HIGHEST /F");
    }

    private static void ClearScheduledTask()
    {
        if (!HasScheduledTask())
        {
            return;
        }

        TryRunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    private static bool HasScheduledTask()
    {
        try
        {
            using var process = Process.Start(CreateSchtasksStartInfo($"/Query /TN \"{TaskName}\""));

            return process?.WaitForExit(5000) == true && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RunSchtasks(string arguments)
    {
        using var process = Process.Start(CreateSchtasksStartInfo(arguments))
            ?? throw new InvalidOperationException("Не удалось запустить schtasks.exe");

        process.WaitForExit();
        if (process.ExitCode == 0)
        {
            return;
        }

        var error = process.StandardError.ReadToEnd();
        var output = process.StandardOutput.ReadToEnd();
        var message = string.IsNullOrWhiteSpace(error) ? output : error;
        throw new InvalidOperationException(NormalizeSchtasksError(message));
    }

    private static bool TryRunSchtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(CreateSchtasksStartInfo(arguments));
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(5000))
            {
                return false;
            }

            _ = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo CreateSchtasksStartInfo(string arguments) =>
        new()
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = SchtasksOutputEncoding,
            StandardErrorEncoding = SchtasksOutputEncoding
        };

    private static string NormalizeSchtasksError(string rawMessage)
    {
        var message = rawMessage.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Не удалось настроить автозагрузку Windows";
        }

        if (message.Contains("Отказано в доступе", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
        {
            return "Отказано в доступе. Для автозапуска от имени администратора сохраните настройки в WPT, запущенном от имени администратора.";
        }

        return message;
    }

}
