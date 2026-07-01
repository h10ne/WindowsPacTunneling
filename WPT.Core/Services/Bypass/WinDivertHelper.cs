using System.Diagnostics;

namespace WPT.Core.Services.Bypass;

public static class WinDivertHelper
{
    private const string DriverServiceName = "WinDivert";

    public static bool IsDriverRunning()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query {DriverServiceName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });

            if (process == null || !process.WaitForExit(3000))
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void TryStopDriver()
    {
        if (!AdminHelper.IsRunningAsAdmin())
        {
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"stop {DriverServiceName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            process?.WaitForExit(5000);
        }
        catch
        {
        }
    }
}
