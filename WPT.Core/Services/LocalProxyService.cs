using System.Diagnostics;
using System.Net.Sockets;
using WPT.Core.Models;

namespace WPT.Core.Services;

public sealed class LocalProxyService : IDisposable
{
    private Process? _process;
    private readonly object _sync = new();

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _process is { HasExited: false };
            }
        }
    }

    public int LocalPort { get; private set; }

    public string LocalProxyAddress => $"127.0.0.1:{LocalPort}";

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
        var configPath = AppPaths.SingBoxConfigFile;
        var config = SingBoxConfigBuilder.Build(profile, localPort);
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
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                progress?.Report(e.Data);
            }
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

        progress?.Report("Ожидание локального прокси...");
        if (!await WaitForPortAsync(localPort, process, cancellationToken))
        {
            Stop();
            throw new InvalidOperationException($"Локальный прокси не ответил на порту {localPort}.");
        }
    }

    public void Stop()
    {
        Process? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
            LocalPort = 0;
        }

        if (process == null)
        {
            return;
        }

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

    public void Dispose() => Stop();

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

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync("127.0.0.1", port, cancellationToken).AsTask();
                if (await Task.WhenAny(connectTask, Task.Delay(200, cancellationToken)) == connectTask
                    && client.Connected)
                {
                    return true;
                }
            }
            catch
            {
            }

            await Task.Delay(200, cancellationToken);
        }

        return false;
    }
}
