using System.Diagnostics;
using Microsoft.Win32;

namespace WPT.Core.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowPacTunneling";
    private const string TaskName = "WindowPacTunneling";
    private const string ElevatedArg = "--elevated";

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

        RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
    }

    private static bool HasScheduledTask()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            return process?.WaitForExit(5000) == true && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RunSchtasks(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Не удалось запустить schtasks.exe");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? "Не удалось настроить автозагрузку Windows"
                    : error.Trim());
        }
    }
}
