using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace WPT.Core.Services.TgWsProxy;

public sealed class TgWsProxyServer : IAsyncDisposable
{
    private readonly TgWsProxyConfig _config;
    private readonly CfProxyBalancer _balancer = new();
    private readonly byte[] _secretBytes;
    private readonly ConcurrentDictionary<string, DateTime> _dcFailUntil = new();
    private readonly ConcurrentDictionary<string, bool> _wsBlacklist = new(StringComparer.Ordinal);

    // Какой способ подключения сработал последним: прямой SNI или fronting. С него и начинаем
    private volatile bool _tryFrontingFirst;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public TgWsProxyServer(TgWsProxyConfig config)
    {
        _config = config;
        _secretBytes = Convert.FromHexString(config.Secret);

        if (_config.CfProxyUserDomains.Count > 0)
        {
            _balancer.UpdateDomainsList(_config.CfProxyUserDomains);
        }
        else if (_config.FallbackCfProxy)
        {
            _balancer.UpdateDomainsList(CfProxyDomains.DefaultDomains);
        }
    }

    public string ProxyLink
    {
        get
        {
            var host = _config.Host == "0.0.0.0" ? "127.0.0.1" : _config.Host;
            return $"tg://proxy?server={host}&port={_config.Port}&secret=dd{_config.Secret}";
        }
    }

    public bool IsRunning => _listener != null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener != null)
        {
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Loopback, _config.Port);
        _listener.Start();
        _acceptLoop = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_listener == null)
        {
            return;
        }

        var acceptLoop = _acceptLoop;
        _cts?.Cancel();

        try
        {
            _listener.Stop();
        }
        catch
        {
        }

        _listener = null;

        if (acceptLoop != null)
        {
            var completed = await Task.WhenAny(acceptLoop, Task.Delay(2000)).ConfigureAwait(false);
            if (completed == acceptLoop)
            {
                try
                {
                    await acceptLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        _cts?.Dispose();
        _cts = null;
        _acceptLoop = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientDisposable = client;
        try
        {
            client.NoDelay = true;
            await using var stream = client.GetStream();

            var handshakeBuffer = new byte[TgWsProxyConstants.HandshakeLen];
            await ReadExactAsync(stream, handshakeBuffer, cancellationToken);

            var parsed = TgWsHandshake.TryParse(handshakeBuffer, _secretBytes);
            if (parsed == null)
            {
                return;
            }

            var protoTag = parsed.ProtoInt switch
            {
                TgWsProxyConstants.ProtoAbridgedInt => TgWsProxyConstants.ProtoTagAbridged,
                TgWsProxyConstants.ProtoIntermediateInt => TgWsProxyConstants.ProtoTagIntermediate,
                _ => TgWsProxyConstants.ProtoTagSecure
            };

            var dcIdx = (short)(parsed.IsMedia ? -parsed.DcId : parsed.DcId);
            var relayInit = TgWsHandshake.GenerateRelayInit(protoTag, dcIdx);
            var ctx = TgWsCryptoContext.Create(parsed.ClientDecPrekeyIv, _secretBytes, relayInit);
            MsgSplitter? splitter = null;
            try
            {
                splitter = new MsgSplitter(relayInit, parsed.ProtoInt);
            }
            catch
            {
            }

            var dcKey = $"{parsed.DcId}{(parsed.IsMedia ? "m" : string.Empty)}";
            var target = _config.DcRedirects.GetValueOrDefault(parsed.DcId);
            // Без своего IP, в чёрном списке или недавно не подключался: сразу в fallback, не тратя время на WS
            if (target == null
                || _wsBlacklist.ContainsKey(dcKey)
                || (_dcFailUntil.TryGetValue(dcKey, out var dcUntil) && DateTime.UtcNow < dcUntil))
            {
                await RunFallbackAsync(stream, relayInit, ctx, parsed.DcId, parsed.IsMedia, splitter, cancellationToken);
                return;
            }

            var (ws, allRedirects) = await ConnectWsAsync(target, parsed.DcId, parsed.IsMedia, cancellationToken);
            if (ws == null)
            {
                if (allRedirects)
                {
                    _wsBlacklist[dcKey] = true;
                }
                else
                {
                    _dcFailUntil[dcKey] = DateTime.UtcNow.AddMinutes(1);
                }

                await RunFallbackAsync(stream, relayInit, ctx, parsed.DcId, parsed.IsMedia, splitter, cancellationToken);
                return;
            }

            _dcFailUntil.TryRemove(dcKey, out _);

            await using (ws)
            {
                await ws.SendAsync(relayInit, cancellationToken);
                await TgWsBridge.BridgeWebSocketAsync(stream, ws, ctx, splitter, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (EndOfStreamException)
        {
        }
        catch
        {
        }
    }

    // Как _connect_one в upstream: на каждый домен прямой SNI и fronting, начиная с последнего удачного.
    // Блокировка по SNI kws*.web.telegram.org обходится fronting'ом на тот же IP
    private async Task<(RawWebSocketClient? Ws, bool AllRedirects)> ConnectWsAsync(
        string target,
        int dc,
        bool isMedia,
        CancellationToken cancellationToken)
    {
        var allRedirects = true;
        foreach (var domain in TgWsProxyConstants.WsDomains(dc, isMedia))
        {
            var frontingFirst = _tryFrontingFirst;
            foreach (var fronted in new[] { frontingFirst, !frontingFirst })
            {
                try
                {
                    var ws = await RawWebSocketClient.ConnectAsync(
                        target,
                        domain,
                        _config.BufferSize,
                        sni: fronted ? TgWsProxyConstants.FrontingSni : null,
                        timeout: TimeSpan.FromSeconds(fronted ? 7 : 8),
                        cancellationToken: cancellationToken);
                    _tryFrontingFirst = fronted;
                    return (ws, false);
                }
                catch (WsHandshakeException ex) when (ex.IsRedirect)
                {
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    allRedirects = false;
                }
            }
        }

        return (null, allRedirects);
    }

    private async Task<bool> RunFallbackAsync(
        NetworkStream stream,
        byte[] relayInit,
        TgWsCryptoContext ctx,
        int dc,
        bool isMedia,
        MsgSplitter? splitter,
        CancellationToken cancellationToken)
    {
        if (_config.FallbackCfProxy)
        {
            if (await TgWsBridge.CfProxyFallbackAsync(
                stream, relayInit, ctx, dc, isMedia, splitter, _balancer, _config, cancellationToken))
            {
                return true;
            }
        }

        var fallbackIp = TgWsProxyConstants.DcDefaultIps.GetValueOrDefault(dc)
            ?? _config.DcRedirects.GetValueOrDefault(dc);
        if (fallbackIp != null)
        {
            return await TgWsBridge.TcpFallbackAsync(stream, fallbackIp, 443, relayInit, ctx, cancellationToken);
        }

        return false;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}
