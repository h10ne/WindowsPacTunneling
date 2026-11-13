using System.Net.Sockets;

namespace WPT.Core.Services.TgWsProxy;

internal static class TgWsBridge
{
    public static async Task BridgeWebSocketAsync(
        NetworkStream clientStream,
        RawWebSocketClient webSocket,
        TgWsCryptoContext ctx,
        MsgSplitter? splitter,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        var clientToWs = Task.Run(async () =>
        {
            var buffer = new byte[65536];
            while (!token.IsCancellationRequested)
            {
                var read = await clientStream.ReadAsync(buffer, token);
                if (read == 0)
                {
                    if (splitter != null)
                    {
                        foreach (var tail in splitter.Flush())
                        {
                            await webSocket.SendAsync(tail, token);
                        }
                    }

                    break;
                }

                var chunk = buffer.AsMemory(0, read);
                var plain = ctx.ClientDecryptor.Update(chunk.Span);
                var encrypted = ctx.TelegramEncryptor.Update(plain);

                if (splitter != null)
                {
                    var parts = splitter.Split(encrypted);
                    if (parts.Count == 0)
                    {
                        continue;
                    }

                    if (parts.Count > 1)
                    {
                        await webSocket.SendBatchAsync(parts, token);
                    }
                    else
                    {
                        await webSocket.SendAsync(parts[0], token);
                    }
                }
                else
                {
                    await webSocket.SendAsync(encrypted, token);
                }
            }
        }, token);

        var wsToClient = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var data = await webSocket.ReceiveAsync(token);
                if (data == null)
                {
                    break;
                }

                var plain = ctx.TelegramDecryptor.Update(data);
                var encrypted = ctx.ClientEncryptor.Update(plain);
                await clientStream.WriteAsync(encrypted, token);
                await clientStream.FlushAsync(token);
            }
        }, token);

        await Task.WhenAny(clientToWs, wsToClient);
        linked.Cancel();

        // Как в BridgeTcpAsync: обрыв любой стороны — штатный конец сессии
        try
        {
            await Task.WhenAll(clientToWs, wsToClient);
        }
        catch
        {
        }
    }

    public static async Task<bool> TcpFallbackAsync(
        NetworkStream clientStream,
        string destination,
        int port,
        byte[] relayInit,
        TgWsCryptoContext ctx,
        CancellationToken cancellationToken)
    {
        using var tcp = new TcpClient();
        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(10));
            await tcp.ConnectAsync(destination, port, connectCts.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // Сессия уже пошла: её обрыв — конец соединения, а не повод пробовать другой путь
        await using var remoteStream = tcp.GetStream();
        await remoteStream.WriteAsync(relayInit, cancellationToken);
        await remoteStream.FlushAsync(cancellationToken);
        await BridgeTcpAsync(clientStream, remoteStream, ctx, cancellationToken);
        return true;
    }

    public static async Task<bool> CfProxyFallbackAsync(
        NetworkStream clientStream,
        byte[] relayInit,
        TgWsCryptoContext ctx,
        int dc,
        bool isMedia,
        MsgSplitter? splitter,
        CfProxyBalancer balancer,
        TgWsProxyConfig config,
        CancellationToken cancellationToken)
    {
        foreach (var baseDomain in balancer.GetDomainsForDc(dc))
        {
            var domain = $"kws{dc}.{baseDomain}";
            RawWebSocketClient ws;
            try
            {
                ws = await RawWebSocketClient.ConnectAsync(
                    domain,
                    domain,
                    config.BufferSize,
                    cancellationToken: cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                continue;
            }

            // Перебираем домены только до подключения: после relayInit повтор испортил бы поток клиента
            await using (ws)
            {
                balancer.UpdateDomainForDc(dc, baseDomain);
                await ws.SendAsync(relayInit, cancellationToken);
                await BridgeWebSocketAsync(clientStream, ws, ctx, splitter, cancellationToken);
            }

            return true;
        }

        return false;
    }

    private static async Task BridgeTcpAsync(
        NetworkStream clientStream,
        Stream remoteStream,
        TgWsCryptoContext ctx,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        // У каждого направления свой буфер: чтения идут одновременно и затирали бы данные друг друга
        var up = Task.Run(async () =>
        {
            var buffer = new byte[65536];
            while (!token.IsCancellationRequested)
            {
                var read = await clientStream.ReadAsync(buffer, token);
                if (read == 0)
                {
                    break;
                }

                var plain = ctx.ClientDecryptor.Update(buffer.AsSpan(0, read));
                var encrypted = ctx.TelegramEncryptor.Update(plain);
                await remoteStream.WriteAsync(encrypted, token);
                await remoteStream.FlushAsync(token);
            }
        }, token);

        var down = Task.Run(async () =>
        {
            var buffer = new byte[65536];
            while (!token.IsCancellationRequested)
            {
                var read = await remoteStream.ReadAsync(buffer, token);
                if (read == 0)
                {
                    break;
                }

                var plain = ctx.TelegramDecryptor.Update(buffer.AsSpan(0, read));
                var encrypted = ctx.ClientEncryptor.Update(plain);
                await clientStream.WriteAsync(encrypted, token);
                await clientStream.FlushAsync(token);
            }
        }, token);

        await Task.WhenAny(up, down);
        linked.Cancel();

        // Вторая сторона обрывается отменой или закрытием сокета — это штатный конец сессии,
        // но ошибку нужно забрать, иначе она всплывает как необработанное исключение задачи
        try
        {
            await Task.WhenAll(up, down);
        }
        catch
        {
        }
    }
}
