using WPT.Core.Models;
using WPT.Core.Services;
using WPT.Wpf.Helpers;

namespace WPT.Wpf.ViewModels;

public sealed class SavedProxyConfigItem : ViewModelBase
{
    private int? _pingMs;
    private bool _isPinging;
    private bool _pingFailed;
    private CancellationTokenSource? _pingCts;

    public SavedProxyConfigItem(SavedProxyConfiguration model)
    {
        Id = model.Id;
        Name = model.Name;
        Link = model.Link;
        Protocol = model.Protocol;
        ProtocolBrush = ProtocolBrushes.Get(model.Protocol);
        Server = ResolveServer(model);
        ServerPort = ResolveServerPort(model);
    }

    public string Id { get; }

    public string Name { get; }

    public string Link { get; }

    public string Protocol { get; }

    public System.Windows.Media.Brush ProtocolBrush { get; }

    public string Server { get; }

    public int ServerPort { get; }

    public int? PingMs
    {
        get => _pingMs;
        private set
        {
            if (SetProperty(ref _pingMs, value))
            {
                OnPropertyChanged(nameof(PingDisplay));
                OnPropertyChanged(nameof(HasPingDisplay));
            }
        }
    }

    public bool IsPinging
    {
        get => _isPinging;
        private set
        {
            if (SetProperty(ref _isPinging, value))
            {
                OnPropertyChanged(nameof(PingDisplay));
                OnPropertyChanged(nameof(HasPingDisplay));
            }
        }
    }

    public bool PingFailed
    {
        get => _pingFailed;
        private set
        {
            if (SetProperty(ref _pingFailed, value))
            {
                OnPropertyChanged(nameof(PingDisplay));
                OnPropertyChanged(nameof(HasPingDisplay));
            }
        }
    }

    public string PingDisplay => IsPinging ? "..." : PingFailed ? "Ошибка" : PingMs.HasValue ? $"{PingMs}мс" : string.Empty;

    public bool HasPingDisplay => IsPinging || PingFailed || PingMs.HasValue;

    public static SavedProxyConfigItem FromModel(SavedProxyConfiguration model) => new(model);

    public async Task PingAsync()
    {
        _pingCts?.Cancel();
        _pingCts?.Dispose();
        var cts = new CancellationTokenSource();
        _pingCts = cts;
        var cancellationToken = cts.Token;

        IsPinging = true;
        PingMs = null;
        PingFailed = false;

        try
        {
            var latency = await ProxyServerPinger.PingAsync(Server, ServerPort, cancellationToken: cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                if (latency.HasValue)
                {
                    PingMs = latency;
                }
                else
                {
                    PingFailed = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_pingCts, cts))
            {
                IsPinging = false;
            }
        }
    }

    private static string ResolveServer(SavedProxyConfiguration model)
    {
        if (ProxyLinkParser.TryParse(model.Link, out var profile, out _))
        {
            return profile.Server;
        }

        return string.Empty;
    }

    private static int ResolveServerPort(SavedProxyConfiguration model)
    {
        if (ProxyLinkParser.TryParse(model.Link, out var profile, out _))
        {
            return profile.ServerPort;
        }

        return 0;
    }

}
