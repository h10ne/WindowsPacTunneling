using System.Net;
using System.Net.Http;

namespace WPT.Core.Services.Bypass;

public static class BypassConnectivityChecker
{
    private static readonly Uri[] ProbeUrls =
    [
        new("https://www.youtube.com/generate_204"),
        new("https://discord.com/api/v9/experiments")
    ];

    public static async Task<bool> CheckYoutubeAndDiscordAsync(CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        foreach (var url in ProbeUrls)
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 401)
            {
                return false;
            }
        }

        return true;
    }
}
