using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace WPT.Core.Services;

public readonly record struct ProxyHealthResult(bool IsReachable, int? LatencyMs);

public static class ProxyHealthChecker
{
    private static readonly Uri ProbeUri = new("http://connectivitycheck.gstatic.com/generate_204");

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
}
