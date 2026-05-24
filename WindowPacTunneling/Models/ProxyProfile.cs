namespace WindowPacTunneling.Models;

public sealed class ProxyProfile
{
    public required string Protocol { get; init; }

    public required string Server { get; init; }

    public required int ServerPort { get; init; }

    public string? Remark { get; init; }

    public string? Uuid { get; init; }

    public string? Password { get; init; }

    public string? Method { get; init; }

    public string Transport { get; init; } = "tcp";

    public string Security { get; init; } = "none";

    public string? Flow { get; init; }

    public string? Sni { get; init; }

    public string? Fingerprint { get; init; }

    public string? PublicKey { get; init; }

    public string? ShortId { get; init; }

    public string? Host { get; init; }

    public string? Path { get; init; }

    public string? ServiceName { get; init; }

    public string? Alpn { get; init; }

    public bool AllowInsecure { get; init; }
}
