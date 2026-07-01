using System.Diagnostics;
using System.Text;

namespace WPT.Core.Services;

public sealed class AwgProxyService : IDisposable
{
    private Process? _process;
    private readonly object _sync = new();
    private readonly string _instanceName;
    private readonly string _configPath;
    private readonly string _pidPath;

    public AwgProxyService(string? instanceName = null)
    {
        _instanceName = string.IsNullOrWhiteSpace(instanceName) ? "default" : instanceName;
        _configPath = AppPaths.AmneziaBoxConfigFileFor(_instanceName);
        _pidPath = AppPaths.AmneziaBoxPidFileFor(_instanceName);
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                if (_process is { HasExited: false })
                {
                    return true;
                }
            }

            return LocalPort > 0 && LocalProxyService.IsPortListening(LocalPort) && HasManagedProcess();
        }
    }

    public int LocalPort { get; private set; }

    public string LocalProxyAddress => LocalPort > 0 ? $"127.0.0.1:{LocalPort}" : "127.0.0.1:0";

    public void Prepare(int localPort)
    {
        LocalPort = localPort;
        TryAdoptFromPidFile();
    }

    public async Task StartAsync(
        string wireGuardConfig,
        int localPort,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (localPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort), "Некорректный локальный порт.");
        }

        if (string.IsNullOrWhiteSpace(wireGuardConfig))
        {
            throw new ArgumentException("Пустая конфигурация WireGuard.", nameof(wireGuardConfig));
        }

        Stop();

        await AmneziaBoxInstaller.EnsureInstalledAsync(progress, cancellationToken);

        var executable = ResolveExecutablePath();
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Не найден amnezia-box.exe.", executable);
        }

        AppPaths.EnsureRoot();
        var config = AmneziaBoxConfigBuilder.BuildForProcessMode(wireGuardConfig, localPort);
        await File.WriteAllTextAsync(_configPath, config, cancellationToken);

        progress?.Report("Проверка конфигурации AmneziaWG...");
        var configError = await RunConfigCheckAsync(executable, _configPath, cancellationToken);
        if (configError != null)
        {
            throw new InvalidOperationException(configError);
        }

        var stderrLines = new List<string>();
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"run -c \"{_configPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            WorkingDirectory = AppPaths.BinDirectory
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            lock (stderrLines)
            {
                stderrLines.Add(e.Data);
                if (stderrLines.Count > 20)
                {
                    stderrLines.RemoveAt(0);
                }
            }

            progress?.Report(e.Data);
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Не удалось запустить amnezia-box.");
        }

        process.BeginErrorReadLine();

        lock (_sync)
        {
            _process = process;
            LocalPort = localPort;
        }

        WritePidFile(process.Id);

        progress?.Report("Ожидание локального SOCKS (AmneziaWG)...");
        if (!await WaitForPortAsync(localPort, process, cancellationToken))
        {
            var details = BuildFailureDetails(process, stderrLines);
            Stop();
            throw new InvalidOperationException(
                $"Локальный SOCKS не ответил на порту {localPort}.{details}");
        }

        if (process.HasExited)
        {
            var details = BuildFailureDetails(process, stderrLines);
            Stop();
            throw new InvalidOperationException($"amnezia-box завершился сразу после запуска.{details}");
        }
    }

    public void Stop()
    {
        KillManagedProcess();
        ClearPidFile();
    }

    public void Dispose() => Stop();

    private static async Task<string?> RunConfigCheckAsync(
        string executable,
        string configPath,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"check -c \"{configPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = AppPaths.BinDirectory
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return "Не удалось запустить проверку конфигурации amnezia-box.";
        }

        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0)
        {
            return null;
        }

        var message = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
        return string.IsNullOrWhiteSpace(message)
            ? $"amnezia-box отклонил конфиг (код {process.ExitCode})."
            : $"Ошибка конфигурации AmneziaWG: {message}";
    }

    private static string BuildFailureDetails(Process process, IReadOnlyList<string> stderrLines)
    {
        var builder = new StringBuilder();
        if (process.HasExited)
        {
            builder.Append($" amnezia-box завершился (код {process.ExitCode}).");
        }

        lock (stderrLines)
        {
            if (stderrLines.Count > 0)
            {
                builder.Append(" Последняя ошибка: ");
                builder.Append(stderrLines[^1]);
            }
        }

        return builder.ToString();
    }

    private void TryAdoptFromPidFile()
    {
        if (!File.Exists(_pidPath))
        {
            return;
        }

        if (!int.TryParse(File.ReadAllText(_pidPath).Trim(), out var pid))
        {
            ClearPidFile();
            return;
        }

        try
        {
            var process = Process.GetProcessById(pid);
            if (process.HasExited || !IsManagedProcess(process))
            {
                process.Dispose();
                ClearPidFile();
                return;
            }

            lock (_sync)
            {
                _process?.Dispose();
                _process = process;
            }
        }
        catch
        {
            ClearPidFile();
        }
    }

    private bool HasManagedProcess()
    {
        if (!File.Exists(_pidPath))
        {
            return false;
        }

        if (!int.TryParse(File.ReadAllText(_pidPath).Trim(), out var pid))
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited && IsManagedProcess(process);
        }
        catch
        {
            return false;
        }
    }

    private void KillManagedProcess()
    {
        Process? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        if (process != null)
        {
            TryKillProcess(process);
            return;
        }

        if (!File.Exists(_pidPath))
        {
            return;
        }

        if (!int.TryParse(File.ReadAllText(_pidPath).Trim(), out var pid))
        {
            return;
        }

        try
        {
            var orphan = Process.GetProcessById(pid);
            if (IsManagedProcess(orphan))
            {
                TryKillProcess(orphan);
            }
            else
            {
                orphan.Dispose();
            }
        }
        catch
        {
        }
    }

    private bool IsManagedProcess(Process process)
    {
        try
        {
            var executable = ResolveExecutablePath();
            return process.MainModule?.FileName.Equals(executable, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
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

    private void WritePidFile(int pid)
    {
        AppPaths.EnsureRoot();
        File.WriteAllText(_pidPath, pid.ToString());
    }

    private void ClearPidFile()
    {
        if (File.Exists(_pidPath))
        {
            File.Delete(_pidPath);
        }
    }

    private static string ResolveExecutablePath()
    {
        return AmneziaBoxInstaller.ExecutablePath;
    }

    private static async Task<bool> WaitForPortAsync(int port, Process process, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                return false;
            }

            if (LocalProxyService.IsPortListening(port))
            {
                return true;
            }

            await Task.Delay(200, cancellationToken);
        }

        return false;
    }

}
