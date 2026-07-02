using System.Diagnostics;
using System.Net.Sockets;
using WPT.Core.Models;

namespace WPT.Core.Services;

public sealed class LocalProxyService : IDisposable
{
    private Process? _process;
    private readonly object _sync = new();
    private readonly string _configPath;
    private readonly string _pidPath;
    private readonly bool _forProcessMode;

    public LocalProxyService(string? instanceName = null)
    {
        _forProcessMode = string.Equals(instanceName, "processmode", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            _configPath = AppPaths.SingBoxConfigFile;
            _pidPath = AppPaths.SingBoxPidFile;
        }
        else
        {
            _configPath = AppPaths.SingBoxConfigFileFor(instanceName);
            _pidPath = AppPaths.SingBoxPidFileFor(instanceName);
        }
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

            return LocalPort > 0 && IsPortListening(LocalPort) && HasManagedSingBoxProcess();
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
        ProxyProfile profile,
        int localPort,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (localPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort), "Некорректный локальный порт.");
        }

        Stop();

        await SingBoxInstaller.EnsureInstalledAsync(progress, cancellationToken);

        var executable = ResolveExecutablePath();
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Не найден sing-box.exe.", executable);
        }

        AppPaths.EnsureRoot();
        var configPath = _configPath;
        var config = _forProcessMode
            ? SingBoxConfigBuilder.BuildForProcessMode(profile, localPort)
            : SingBoxConfigBuilder.Build(profile, localPort);
        await File.WriteAllTextAsync(configPath, config, cancellationToken);

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"run -c \"{configPath}\"",
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

            if (e.Data.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                AppLog.Warning($"sing-box: {e.Data}");
            }
            else
            {
                AppLog.Debug($"sing-box: {e.Data}");
            }

            progress?.Report(e.Data);
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Не удалось запустить sing-box.");
        }

        process.BeginErrorReadLine();

        lock (_sync)
        {
            _process = process;
            LocalPort = localPort;
        }

        WritePidFile(process.Id);

        progress?.Report("Ожидание локального прокси...");
        AppLog.Info($"Запуск sing-box на порту {localPort}, конфиг: {configPath}");
        if (!await WaitForPortAsync(localPort, process, cancellationToken))
        {
            Stop();
            throw new InvalidOperationException($"Локальный прокси не ответил на порту {localPort}.");
        }

        if (process.HasExited)
        {
            Stop();
            throw new InvalidOperationException("sing-box завершился сразу после запуска.");
        }
    }

    public void Stop()
    {
        AppLog.Info("Остановка локального прокси (sing-box)");
        KillManagedProcess();
        ClearPidFile();
    }

    public void Dispose() => Stop();

    public static bool IsPortListening(int port)
    {
        if (port is < 1 or > 65535)
        {
            return false;
        }

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            return connectTask.Wait(300) && client.Connected;
        }
        catch
        {
            return false;
        }
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

    private bool HasManagedSingBoxProcess()
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
        var bundled = Path.Combine(AppContext.BaseDirectory, "sing-box.exe");
        return File.Exists(bundled) ? bundled : SingBoxInstaller.ExecutablePath;
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

            if (IsPortListening(port))
            {
                return true;
            }

            await Task.Delay(200, cancellationToken);
        }

        return false;
    }
}
