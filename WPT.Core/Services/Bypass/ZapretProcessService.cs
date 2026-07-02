using System.Diagnostics;

namespace WPT.Core.Services.Bypass;

public sealed class ZapretProcessService
{
    private readonly object _sync = new();

    public bool IsRunning => FindManagedWinWsProcess() is not null;

    public string? ActiveStrategy { get; private set; }

    public bool TryAdopt(string? savedStrategy)
    {
        if (!IsRunning)
        {
            return false;
        }

        lock (_sync)
        {
            ActiveStrategy = !string.IsNullOrWhiteSpace(savedStrategy)
                ? savedStrategy
                : ActiveStrategy ?? "запущен";
        }

        return true;
    }

    public async Task StartStrategyAsync(string strategyBatFileName, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (IsRunning
            && string.Equals(ActiveStrategy, strategyBatFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Stop();

        var batPath = Path.Combine(AppPaths.ZapretDirectory, strategyBatFileName);
        if (!File.Exists(batPath))
        {
            throw new FileNotFoundException("Не найден bat-файл стратегии zapret.", batPath);
        }

        progress?.Report($"Запуск стратегии {strategyBatFileName}...");
        AppLog.Info($"Запуск zapret: {strategyBatFileName}");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batPath}\"",
            WorkingDirectory = AppPaths.ZapretDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var cmd = Process.Start(psi)
            ?? throw new InvalidOperationException("Не удалось запустить bat-файл zapret.");

        await cmd.WaitForExitAsync(cancellationToken);

        if (!await WaitForWinWsAsync(TimeSpan.FromSeconds(15), cancellationToken))
        {
            throw new InvalidOperationException("winws.exe не запустился после выполнения bat-файла.");
        }

        lock (_sync)
        {
            ActiveStrategy = strategyBatFileName;
        }
    }

    public void Stop()
    {
        AppLog.Info("Остановка zapret (winws.exe)");
        foreach (var process in Process.GetProcessesByName("winws"))
        {
            try
            {
                if (!IsManagedProcess(process))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        lock (_sync)
        {
            ActiveStrategy = null;
        }
    }

    private static Process? FindManagedWinWsProcess()
    {
        foreach (var process in Process.GetProcessesByName("winws"))
        {
            try
            {
                if (IsManagedProcess(process))
                {
                    return process;
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static bool IsManagedProcess(Process process)
    {
        try
        {
            var modulePath = process.MainModule?.FileName;
            return modulePath != null
                && modulePath.StartsWith(AppPaths.ZapretBinDirectory, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitForWinWsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FindManagedWinWsProcess() != null)
            {
                return true;
            }

            await Task.Delay(300, cancellationToken);
        }

        return false;
    }
}
