using WPT.Core.Models;

namespace WPT.Core.Services;

public sealed class ProcessModeService : IDisposable
{
    private readonly LocalProxyService _proxyService = new("processmode");
    private readonly AwgProxyService _awgProxyService = new("processmode");
    private ProcessModeConnectionType? _activeConnectionType;
    private bool _redirectorRunning;

    public bool IsRunning => _redirectorRunning && IsLocalProxyRunning();

    public int LocalPort => GetActiveLocalPort();

    public string LocalProxyAddress
    {
        get
        {
            var port = GetActiveLocalPort();
            return port > 0 ? $"127.0.0.1:{port}" : "127.0.0.1:0";
        }
    }

    public ProcessModeConnectionType? ActiveConnectionType => _activeConnectionType;

    public void Prepare(int localPort)
    {
        _proxyService.Prepare(localPort);
        _awgProxyService.Prepare(localPort);
    }

    public async Task StartAsync(
        ProcessModeConnectionType connectionType,
        ProxyProfile? ssProfile,
        string? amneziaConfig,
        int localPort,
        IReadOnlyList<string> applications,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (applications.Count == 0)
        {
            throw new InvalidOperationException("Добавьте хотя бы одно приложение в список.");
        }

        Stop();

        AdminHelper.EnsureAdminOrThrow();
        RedirectorInstaller.EnsureInstalled(progress);

        progress?.Report(connectionType == ProcessModeConnectionType.Amnezia
            ? "Запуск локального SOCKS (AmneziaWG)..."
            : "Запуск локального прокси...");

        switch (connectionType)
        {
            case ProcessModeConnectionType.Shadowsocks:
                if (ssProfile == null)
                {
                    throw new ArgumentException("Не указан профиль Shadowsocks.", nameof(ssProfile));
                }

                if (!string.Equals(ssProfile.Protocol, "ss", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("Process Mode с Redirector для Shadowsocks поддерживает только ss://.");
                }

                await _proxyService.StartAsync(ssProfile, localPort, progress: null, cancellationToken);
                _activeConnectionType = ProcessModeConnectionType.Shadowsocks;
                break;

            case ProcessModeConnectionType.Amnezia:
                if (string.IsNullOrWhiteSpace(amneziaConfig))
                {
                    throw new ArgumentException("Не указана конфигурация Amnezia.", nameof(amneziaConfig));
                }

                await _awgProxyService.StartAsync(amneziaConfig, localPort, progress, cancellationToken);
                _activeConnectionType = ProcessModeConnectionType.Amnezia;
                break;

            default:
                throw new NotSupportedException($"Тип подключения {connectionType} не поддерживается.");
        }

        try
        {
            progress?.Report(DnsCacheHelper.TryFlush()
                ? "Сброс DNS-кэша Windows..."
                : "Запуск Redirector...");

            await StartRedirectorAsync(localPort, applications, progress, cancellationToken);
            progress?.Report("Перезапустите Discord после старта Process Mode");
        }
        catch
        {
            StopLocalProxy();
            throw;
        }
    }

    public async Task TryRestoreAsync(
        ProcessModeConnectionType connectionType,
        ProxyProfile? ssProfile,
        string? amneziaConfig,
        int localPort,
        IReadOnlyList<string> applications,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (applications.Count == 0)
        {
            throw new InvalidOperationException("Добавьте хотя бы одно приложение в список.");
        }

        Prepare(localPort);

        if (IsRunning)
        {
            return;
        }

        var proxyRunning = connectionType switch
        {
            ProcessModeConnectionType.Amnezia => _awgProxyService.IsRunning,
            _ => _proxyService.IsRunning
        };

        if (proxyRunning)
        {
            _activeConnectionType = connectionType;
            AdminHelper.EnsureAdminOrThrow();
            RedirectorInstaller.EnsureInstalled(progress);
            progress?.Report(DnsCacheHelper.TryFlush()
                ? "Сброс DNS-кэша Windows..."
                : "Восстановление Redirector...");
            await StartRedirectorAsync(localPort, applications, progress, cancellationToken);
            progress?.Report("Перезапустите Discord после старта Process Mode");
            return;
        }

        await StartAsync(
            connectionType,
            ssProfile,
            amneziaConfig,
            localPort,
            applications,
            progress,
            cancellationToken);
    }

    private Task StartRedirectorAsync(
        int localPort,
        IReadOnlyList<string> applications,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RedirectorNative.Start("127.0.0.1", localPort, applications, progress);
        _redirectorRunning = true;
        progress?.Report($"Redirector активен · {applications.Count} приложений · 127.0.0.1:{localPort}");

        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (_redirectorRunning)
        {
            RedirectorNative.Stop();
            _redirectorRunning = false;
        }

        StopLocalProxy();
    }

    private void StopLocalProxy()
    {
        _proxyService.Stop();
        _awgProxyService.Stop();
        _activeConnectionType = null;
    }

    public void Dispose() => Stop();

    private bool IsLocalProxyRunning() => _activeConnectionType switch
    {
        ProcessModeConnectionType.Amnezia => _awgProxyService.IsRunning,
        ProcessModeConnectionType.Shadowsocks => _proxyService.IsRunning,
        _ => false
    };

    private int GetActiveLocalPort() => _activeConnectionType switch
    {
        ProcessModeConnectionType.Amnezia => _awgProxyService.LocalPort,
        ProcessModeConnectionType.Shadowsocks => _proxyService.LocalPort,
        _ => _proxyService.LocalPort > 0 ? _proxyService.LocalPort : _awgProxyService.LocalPort
    };

}
