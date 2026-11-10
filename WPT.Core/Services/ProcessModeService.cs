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
        _awgProxyService.Prepare(AwgUpstreamPort(localPort));
    }

    // Amnezia: Redirector → sing-box (sniff SNI) → wireproxy. Redirector отдаёт IP из системного DNS,
    // а он бывает fake-IP (198.18.x.x) или подменён провайдером; у wireproxy нет sniffing, и такой IP в туннеле никуда не ведёт
    // ponytail: соседний порт фиксирован, искать свободный — если начнутся коллизии
    private static int AwgUpstreamPort(int localPort) => localPort + 1;

    public async Task StartAsync(
        ProcessModeConnectionType connectionType,
        ProxyProfile? proxyProfile,
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

        progress?.Report(connectionType switch
        {
            ProcessModeConnectionType.Amnezia => "Запуск локального SOCKS (AmneziaWG)...",
            ProcessModeConnectionType.Vless => "Запуск локального SOCKS (VLESS + XUDP)...",
            _ => "Запуск локального прокси..."
        });

        switch (connectionType)
        {
            case ProcessModeConnectionType.Shadowsocks:
            case ProcessModeConnectionType.Vless:
                if (proxyProfile == null)
                {
                    throw new ArgumentException("Не указан профиль прокси.", nameof(proxyProfile));
                }

                if (connectionType == ProcessModeConnectionType.Shadowsocks
                    && !string.Equals(proxyProfile.Protocol, "ss", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("Process Mode для Shadowsocks поддерживает только ss://.");
                }

                if (connectionType == ProcessModeConnectionType.Vless
                    && !string.Equals(proxyProfile.Protocol, "vless", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("Process Mode для VLESS поддерживает только vless://.");
                }

                await _proxyService.StartAsync(proxyProfile, localPort, progress: null, cancellationToken);
                _activeConnectionType = connectionType;
                break;

            case ProcessModeConnectionType.Amnezia:
                if (string.IsNullOrWhiteSpace(amneziaConfig))
                {
                    throw new ArgumentException("Не указана конфигурация Amnezia.", nameof(amneziaConfig));
                }

                var upstreamPort = AwgUpstreamPort(localPort);
                await _awgProxyService.StartAsync(amneziaConfig, upstreamPort, forPacProxy: false, progress, cancellationToken);
                try
                {
                    await _proxyService.StartAsync(
                        new ProxyProfile { Protocol = "socks", Server = "127.0.0.1", ServerPort = upstreamPort },
                        localPort,
                        progress: null,
                        cancellationToken);
                }
                catch
                {
                    _awgProxyService.Stop();
                    throw;
                }

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
        ProxyProfile? proxyProfile,
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
            ProcessModeConnectionType.Amnezia => _awgProxyService.IsRunning && _proxyService.IsRunning,
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
            proxyProfile,
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
        ProcessModeConnectionType.Amnezia => _awgProxyService.IsRunning && _proxyService.IsRunning,
        ProcessModeConnectionType.Shadowsocks => _proxyService.IsRunning,
        ProcessModeConnectionType.Vless => _proxyService.IsRunning,
        _ => false
    };

    // Во всех режимах Redirector смотрит в sing-box
    private int GetActiveLocalPort() => _proxyService.LocalPort;

}
