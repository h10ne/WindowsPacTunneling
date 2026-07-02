using System.Diagnostics;

namespace WPT.Core.Services.Bypass;

internal static class ZapretLegacyServiceCleanup
{
    private const string ServiceName = "zapret";

    public static void TryRemoveInBackground()
    {
        _ = Task.Run(TryRemoveCore);
    }

    public static void TryRemoveSync()
    {
        TryRemoveCore();
    }

    private static void TryRemoveCore()
    {
        RunSc($"stop {ServiceName}");
        RunSc($"delete {ServiceName}");
    }

    private static void RunSc(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(3000);
        }
        catch
        {
        }
    }
}
