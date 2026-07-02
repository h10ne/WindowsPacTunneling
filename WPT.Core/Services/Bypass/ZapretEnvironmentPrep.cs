using System.Diagnostics;

namespace WPT.Core.Services.Bypass;

internal static class ZapretEnvironmentPrep
{
    public static void Run()
    {
        RunServiceBatCommand("load_game_filter");
        RunServiceBatCommand("load_user_lists");
        EnableTcpTimestamps();
    }

    private static void RunServiceBatCommand(string command)
    {
        var serviceBat = Path.Combine(AppPaths.ZapretDirectory, "service.bat");
        if (!File.Exists(serviceBat))
        {
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c call \"{serviceBat}\" {command}",
                WorkingDirectory = AppPaths.ZapretDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(10_000);
        }
        catch
        {
        }
    }

    private static void EnableTcpTimestamps()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface tcp set global timestamps=enabled",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(5000);
        }
        catch
        {
        }
    }
}
