using System.Diagnostics;

namespace WPT.Core.Services.Bypass;

public sealed class ZapretProcessService
{
    private readonly object _sync = new();

    private bool _externalRunning;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _externalRunning;
            }
        }
    }

    public string? ActiveStrategy { get; private set; }

    public bool TryAdopt(string? savedStrategy)
    {
        lock (_sync)
        {
            if (!_externalRunning)
            {
                return false;
            }

            ActiveStrategy = !string.IsNullOrWhiteSpace(savedStrategy)
                ? savedStrategy
                : ActiveStrategy ?? "запущен";
            return true;
        }
    }

    public async Task<bool> TryAdoptAsync(string? savedStrategy, CancellationToken cancellationToken = default)
    {
        if (TryAdopt(savedStrategy))
        {
            return true;
        }

        var adopted = await Task.Run(() => FindManagedWinWsProcess() is not null, cancellationToken)
            .ConfigureAwait(false);

        if (!adopted)
        {
            return false;
        }

        lock (_sync)
        {
            _externalRunning = true;
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

        await StopAsync().ConfigureAwait(false);

        var batPath = Path.Combine(AppPaths.ZapretDirectory, strategyBatFileName);
        if (!File.Exists(batPath))
        {
            throw new FileNotFoundException("Не найден bat-файл стратегии zapret.", batPath);
        }

        var winwsPath = Path.Combine(AppPaths.ZapretBinDirectory, "winws.exe");
        if (!File.Exists(winwsPath))
        {
            throw new FileNotFoundException("Не найден winws.exe.", winwsPath);
        }

        progress?.Report($"Запуск стратегии {strategyBatFileName}...");
        AppLog.Info($"Запуск winws.exe (скрытый): {strategyBatFileName}");

        await Task.Run(ZapretEnvironmentPrep.Run, cancellationToken).ConfigureAwait(false);

        var args = ZapretStrategyArgumentParser.ExtractWinWsArguments(batPath);
        AppLog.Debug($"Аргументы winws ({args.Length} символов)");

        var psi = new ProcessStartInfo
        {
            FileName = winwsPath,
            Arguments = args,
            WorkingDirectory = AppPaths.ZapretBinDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Не удалось запустить winws.exe.");

        try
        {
            if (!await WaitForWinWsAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("winws.exe не запустился после старта стратегии.");
            }
        }
        catch
        {
            TryKillProcess(process);
            await StopAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            process.Dispose();
        }

        lock (_sync)
        {
            _externalRunning = true;
            ActiveStrategy = strategyBatFileName;
        }
    }

    public async Task StopAsync()
    {
        AppLog.Info("Остановка winws.exe");

        lock (_sync)
        {
            _externalRunning = false;
            ActiveStrategy = null;
        }

        await Task.Run(() =>
        {
            KillManagedOrphanProcesses();
            ZapretLegacyServiceCleanup.TryRemoveSync();
        }).ConfigureAwait(false);
    }

    private static Process? FindManagedWinWsProcess()
    {
        foreach (var process in Process.GetProcessesByName("winws"))
        {
            try
            {
                if (ProcessPathHelper.IsExecutableFromDirectory(process, AppPaths.ZapretBinDirectory))
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

    private static void KillManagedOrphanProcesses()
    {
        foreach (var process in Process.GetProcessesByName("winws"))
        {
            try
            {
                if (!ProcessPathHelper.IsExecutableFromDirectory(process, AppPaths.ZapretBinDirectory))
                {
                    continue;
                }

                TryKillProcess(process);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
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

    private static async Task<bool> WaitForWinWsAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await Task.Run(() => FindManagedWinWsProcess() is not null, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
