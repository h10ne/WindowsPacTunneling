using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace WPT.Core.Services;

public readonly record struct ProxyHealthResult(bool IsReachable, int? LatencyMs, bool UdpSupported = true);

public static class ProxyHealthChecker
{
    private static readonly Uri ProbeUri = new("http://connectivitycheck.gstatic.com/generate_204");

    private static readonly byte[] Socks5NoAuthMethods = [0x05, 0x01, 0x00];

    private static readonly byte[] Socks5UdpAssociateRequest =
    [
        0x05, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ];

    private static readonly byte[] GstaticProbeAddress = [142, 250, 204, 246];

    public static async Task<ProxyHealthResult> CheckAsync(int localPort, CancellationToken cancellationToken = default)
    {
        if (!LocalProxyService.IsPortListening(localPort))
        {
            return new ProxyHealthResult(false, null);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://127.0.0.1:{localPort}"),
                UseProxy = true
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
            using var response = await client.GetAsync(
                ProbeUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            stopwatch.Stop();

            var isReachable = response.IsSuccessStatusCode;
            return new ProxyHealthResult(
                isReachable,
                isReachable ? (int)stopwatch.ElapsedMilliseconds : null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            stopwatch.Stop();
            return new ProxyHealthResult(false, null);
        }
    }

    public static async Task<ProxyHealthResult> CheckProcessModeAsync(int localPort, CancellationToken cancellationToken = default)
    {
        if (!LocalProxyService.IsPortListening(localPort))
        {
            return new ProxyHealthResult(false, null, false);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, localPort, cancellationToken);
            await using var stream = tcpClient.GetStream();

            if (!await NegotiateSocks5Async(stream, cancellationToken))
            {
                return new ProxyHealthResult(false, null, false);
            }

            if (!await TrySocks5ConnectAsync(stream, GstaticProbeAddress, 80, cancellationToken))
            {
                return new ProxyHealthResult(false, null, false);
            }

            var latencyMs = (int)stopwatch.ElapsedMilliseconds;
            var udpSupported = await TrySocks5UdpRelayAsync(localPort, cancellationToken);
            return new ProxyHealthResult(udpSupported, latencyMs, udpSupported);
            // IsReachable = SOCKS5 TCP CONNECT + UDP relay с ответом; не проверяет WebRTC приложений
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new ProxyHealthResult(false, null, false);
        }
    }

    private static async Task<bool> NegotiateSocks5Async(Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Socks5NoAuthMethods, cancellationToken);
        var methodResponse = new byte[2];
        return await ReadExactAsync(stream, methodResponse, cancellationToken)
            && methodResponse[0] == 0x05
            && methodResponse[1] == 0x00;
    }

    private static async Task<bool> TrySocks5ConnectAsync(
        Stream stream,
        byte[] ipv4Address,
        int port,
        CancellationToken cancellationToken)
    {
        var request = new byte[10];
        request[0] = 0x05;
        request[1] = 0x01;
        request[2] = 0x00;
        request[3] = 0x01;
        ipv4Address.CopyTo(request, 4);
        request[8] = (byte)(port >> 8);
        request[9] = (byte)port;

        await stream.WriteAsync(request, cancellationToken);

        var response = new byte[10];
        if (!await ReadExactAsync(stream, response, cancellationToken))
        {
            return false;
        }

        return response[0] == 0x05 && response[1] == 0x00;
    }

    private static async Task<bool> TrySocks5UdpRelayAsync(int localPort, CancellationToken cancellationToken)
    {
        TcpClient? tcpClient = null;
        try
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, localPort, cancellationToken);
            await using var stream = tcpClient.GetStream();

            if (!await NegotiateSocks5Async(stream, cancellationToken))
            {
                return false;
            }

            await stream.WriteAsync(Socks5UdpAssociateRequest, cancellationToken);

            var response = new byte[10];
            if (!await ReadExactAsync(stream, response, cancellationToken))
            {
                return false;
            }

            if (response[0] != 0x05 || response[1] != 0x00 || response[3] != 0x01)
            {
                return false;
            }

            var relayAddress = new IPAddress(response.AsSpan(4, 4));
            var relayPort = (response[8] << 8) | response[9];

            using var udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));

            var packet = new byte[10 + MinimalDnsQuery.Length];
            packet[0] = 0x00;
            packet[1] = 0x00;
            packet[2] = 0x00;
            packet[3] = 0x01;
            packet[4] = 1;
            packet[5] = 1;
            packet[6] = 1;
            packet[7] = 1;
            packet[8] = 0;
            packet[9] = 53;
            MinimalDnsQuery.CopyTo(packet, 10);

            await udpClient.SendAsync(packet, packet.Length, new IPEndPoint(relayAddress, relayPort));

            using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            receiveCts.CancelAfter(TimeSpan.FromSeconds(5));
            var result = await udpClient.ReceiveAsync(receiveCts.Token);
            return result.Buffer.Length > 12;
        }
        catch
        {
            return false;
        }
        finally
        {
            tcpClient?.Dispose();
        }
    }

    private static readonly byte[] MinimalDnsQuery =
    [
        0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x07, 0x65, 0x78, 0x61, 0x6d, 0x70, 0x6c, 0x65, 0x03, 0x63, 0x6f, 0x6d,
        0x00, 0x00, 0x01, 0x00, 0x01
    ];

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
