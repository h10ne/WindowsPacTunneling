using System.Diagnostics;
using System.Net.Sockets;

namespace WPT.Core.Services;

public static class ProxyServerPinger
{
    public static async Task<int?> PingAsync(
        string host,
        int port,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);
            await tcpClient.ConnectAsync(host, port, timeoutCts.Token);
            stopwatch.Stop();
            return (int)stopwatch.ElapsedMilliseconds;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}
