using WPT.Core.Services.TgWsProxy;

namespace WPT.Core.Services.Bypass;

public sealed class BypassService : IAsyncDisposable
{
    private readonly ZapretProcessService _zapret = new();
    private readonly TgWsProxyService _telegram = new();

    public bool IsZapretRunning => _zapret.IsRunning;

    public bool IsTelegramRunning => _telegram.IsRunning;

    public string? ActiveZapretStrategy => _zapret.ActiveStrategy;

    public string TelegramProxyLink => _telegram.ProxyLink;

    public string TelegramSecret => _telegram.Secret;

    public void TryAdoptExisting(string? savedZapretStrategy) => _zapret.TryAdopt(savedZapretStrategy);

    public async Task StartAsync(
        bool enableZapret,
        bool enableTelegram,
        string? savedStrategy,
        int telegramPort,
        string? telegramSecret,
        IProgress<BypassProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (enableZapret)
        {
            AppLog.Info("Запуск обхода zapret");
            AdminHelper.EnsureZapretAdminOrThrow();
            await ZapretInstaller.EnsureInstalledAsync(
                progress == null ? null : new Progress<string>(m => progress.Report(BypassProgressReport.Status(m))),
                cancellationToken);

            var strategy = string.IsNullOrWhiteSpace(savedStrategy)
                ? await ResolveWorkingStrategyAsync(null, progress, cancellationToken)
                : savedStrategy;

            _zapret.TryAdopt(strategy);
            if (_zapret.IsRunning)
            {
                progress?.Report(BypassProgressReport.Status("Zapret уже запущен — подключено к существующему процессу"));
            }
            else
            {
                progress?.Report(BypassProgressReport.Status($"Запуск стратегии {strategy}..."));
                await _zapret.StartStrategyAsync(
                    strategy,
                    progress == null ? null : new Progress<string>(m => progress.Report(BypassProgressReport.Status(m))),
                    cancellationToken);
            }
        }

        if (enableTelegram)
        {
            await StartTelegramAsync(telegramPort, telegramSecret, progress, cancellationToken);
        }
    }

    public async Task StartTelegramAsync(
        int telegramPort,
        string? telegramSecret,
        IProgress<BypassProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (_telegram.IsRunning)
        {
            return;
        }

        progress?.Report(BypassProgressReport.Status("Запуск MTProto WS-прокси для Telegram..."));
        AppLog.Info($"Запуск Telegram WS-прокси на порту {telegramPort}");
        await _telegram.StartAsync(telegramPort, telegramSecret, cancellationToken);
        progress?.Report(BypassProgressReport.Status("Telegram-прокси запущен"));
    }

    public async Task<string> ProbeStrategyAsync(
        string? preferredStrategy,
        IProgress<BypassProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        AdminHelper.EnsureZapretAdminOrThrow();
        await ZapretInstaller.EnsureInstalledAsync(
            progress == null ? null : new Progress<string>(m => progress.Report(BypassProgressReport.Status(m))),
            cancellationToken);

        return await ResolveWorkingStrategyAsync(preferredStrategy, progress, cancellationToken);
    }

    public async Task StopAsync(bool stopZapret, bool stopTelegram)
    {
        AppLog.Info($"Остановка обхода (zapret={stopZapret}, telegram={stopTelegram})");
        if (stopZapret)
        {
            _zapret.Stop();
        }

        if (stopTelegram)
        {
            await _telegram.StopAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _zapret.Stop();
        await _telegram.DisposeAsync();
    }

    private async Task<string> ResolveWorkingStrategyAsync(
        string? savedStrategy,
        IProgress<BypassProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        var strategies = ZapretInstaller.DiscoverStrategies();
        if (strategies.Count == 0)
        {
            throw new InvalidOperationException("Не найдены bat-файлы стратегий general*.bat в каталоге zapret.");
        }

        if (_zapret.IsRunning)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            if (await BypassConnectivityChecker.CheckYoutubeAndDiscordAsync(cancellationToken))
            {
                return _zapret.ActiveStrategy ?? savedStrategy ?? strategies[0];
            }

            _zapret.Stop();
            await Task.Delay(500, cancellationToken);
        }

        var ordered = OrderStrategies(strategies, savedStrategy);
        var total = ordered.Count;

        for (var i = 0; i < ordered.Count; i++)
        {
            var strategy = ordered[i];
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(BypassProgressReport.Probe(i + 1, total));
            progress?.Report(BypassProgressReport.Status($"Проверка стратегии {strategy}..."));

            _zapret.Stop();
            await Task.Delay(500, cancellationToken);

            try
            {
                await _zapret.StartStrategyAsync(
                    strategy,
                    progress == null ? null : new Progress<string>(m => progress.Report(BypassProgressReport.Status(m))),
                    cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

                if (await BypassConnectivityChecker.CheckYoutubeAndDiscordAsync(cancellationToken))
                {
                    return strategy;
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, $"Стратегия {strategy} не подошла");
            }

            _zapret.Stop();
        }

        throw new InvalidOperationException(
            "Ни одна стратегия zapret не обеспечила доступ к YouTube и Discord. Попробуйте позже или проверьте Secure DNS.");
    }

    private static IReadOnlyList<string> OrderStrategies(IReadOnlyList<string> strategies, string? savedStrategy)
    {
        if (string.IsNullOrWhiteSpace(savedStrategy))
        {
            return strategies;
        }

        return strategies
            .OrderByDescending(x => x.Equals(savedStrategy, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
