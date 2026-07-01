using System.Security.Cryptography;

namespace WPT.Core.Services.TgWsProxy;

public sealed class TgWsProxyService : IAsyncDisposable
{
    private TgWsProxyServer? _server;
    private TgWsProxyConfig? _config;

    public bool IsRunning => _server?.IsRunning == true;

    public string ProxyLink => _server?.ProxyLink ?? string.Empty;

    public string Secret => _config?.Secret ?? string.Empty;

    public int Port => _config?.Port ?? 1443;

    public static string LoadOrCreateSecret(string? savedSecret)
    {
        if (!string.IsNullOrWhiteSpace(savedSecret) && savedSecret.Length == 32)
        {
            return savedSecret;
        }

        if (File.Exists(AppPaths.TgWsProxySecretFile))
        {
            var fromFile = File.ReadAllText(AppPaths.TgWsProxySecretFile).Trim();
            if (fromFile.Length == 32)
            {
                return fromFile;
            }
        }

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        AppPaths.EnsureRoot();
        File.WriteAllText(AppPaths.TgWsProxySecretFile, secret);
        return secret;
    }

    public async Task StartAsync(int port, string? savedSecret, CancellationToken cancellationToken = default)
    {
        await StopAsync();

        var secret = LoadOrCreateSecret(savedSecret);
        _config = new TgWsProxyConfig
        {
            Port = port,
            Host = "127.0.0.1",
            Secret = secret
        };

        _server = new TgWsProxyServer(_config);
        await _server.StartAsync(cancellationToken);
    }

    public async Task StopAsync()
    {
        if (_server == null)
        {
            return;
        }

        await _server.StopAsync().ConfigureAwait(false);
        _server = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
