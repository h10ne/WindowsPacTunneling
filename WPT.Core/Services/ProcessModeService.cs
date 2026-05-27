using WPT.Core.Models;

namespace WPT.Core.Services;

public sealed class ProcessModeService : IDisposable
{
    private readonly LocalProxyService _proxyService = new("processmode");
    private bool _redirectorRunning;

    public bool IsRunning => _redirectorRunning && _proxyService.IsRunning;

    public int LocalPort => _proxyService.LocalPort;

    public string LocalProxyAddress => _proxyService.LocalProxyAddress;

    public void Prepare(int localPort) => _proxyService.Prepare(localPort);

    public async Task StartAsync(
        ProxyProfile profile,
        int localPort,
        IReadOnlyList<string> applications,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (applications.Count == 0)
        {
            throw new InvalidOperationException("Добавьте хотя бы одно приложение в список.");
        }

        if (!string.Equals(profile.Protocol, "ss", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Process Mode с Redirector пока поддерживает только ss://.");
        }

        Stop();

        AdminHelper.EnsureAdminOrThrow();
        RedirectorInstaller.EnsureInstalled(progress);

        progress?.Report("Запуск локального прокси...");
        await _proxyService.StartAsync(profile, localPort, progress: null, cancellationToken);

        try
        {
            await StartRedirectorAsync(localPort, applications, progress, cancellationToken);
        }
        catch
        {
            _proxyService.Stop();
            throw;
        }
    }

    public async Task TryRestoreAsync(
        ProxyProfile profile,
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

        if (_proxyService.IsRunning)
        {
            AdminHelper.EnsureAdminOrThrow();
            RedirectorInstaller.EnsureInstalled(progress);
            progress?.Report("Восстановление Redirector...");
            await StartRedirectorAsync(localPort, applications, progress, cancellationToken);
            return;
        }

        await StartAsync(profile, localPort, applications, progress, cancellationToken);
    }

    private Task StartRedirectorAsync(
        int localPort,
        IReadOnlyList<string> applications,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            RedirectorNative.Start("127.0.0.1", localPort, applications, progress);
            _redirectorRunning = true;
            progress?.Report($"Redirector активен · {applications.Count} приложений · 127.0.0.1:{localPort}");
        }
        catch
        {
            throw;
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (_redirectorRunning)
        {
            RedirectorNative.Stop();
            _redirectorRunning = false;
        }

        _proxyService.Stop();
    }

    public void Dispose() => Stop();

}
