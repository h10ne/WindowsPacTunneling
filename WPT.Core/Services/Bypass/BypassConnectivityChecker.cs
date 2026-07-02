using System.Net;
using System.Net.Http;

namespace WPT.Core.Services.Bypass;

public static class BypassConnectivityChecker
{
    private static readonly (Uri Url, string Label)[] ProbeTargets =
    [
        (new Uri("https://www.youtube.com/generate_204"), "YouTube"),
        (new Uri("https://discord.com/api/v9/experiments"), "Discord")
    ];

    public static async Task<bool> CheckYoutubeAndDiscordAsync(CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            UseProxy = false
        };

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

        foreach (var (url, label) in ProbeTargets)
        {
            if (!await ProbeTargetAsync(httpClient, url, label, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> ProbeTargetAsync(
        HttpClient httpClient,
        Uri url,
        string label,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode || (int)response.StatusCode == 401)
            {
                AppLog.Debug($"Проверка {label}: OK ({(int)response.StatusCode})");
                return true;
            }

            AppLog.Debug($"Проверка {label}: HTTP {(int)response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            AppLog.Debug(ex, $"Проверка {label}: недоступен");
            return false;
        }
    }
}
