using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace WPT.Core.Services.TgWsProxy;

internal sealed class WsHandshakeException : Exception
{
    public int StatusCode { get; }

    public bool IsRedirect => StatusCode is 301 or 302 or 303 or 307 or 308;

    public WsHandshakeException(int statusCode, string statusLine)
        : base($"HTTP {statusCode}: {statusLine}")
    {
        StatusCode = statusCode;
    }
}

internal sealed class RawWebSocketClient : IAsyncDisposable
{
    // Защита от мусорной длины кадра, как в upstream
    private const int MaxMessageLen = 16 * 1024 * 1024;

    private readonly TcpClient _tcpClient;
    private readonly SslStream _sslStream;

    // Пишут двое: мост и ответ на ping из цикла чтения, а SslStream не допускает параллельной записи
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Сборка сообщения, пришедшего несколькими кадрами (opcode 0x0)
    private readonly MemoryStream _frag = new();
    private bool _closed;

    private RawWebSocketClient(TcpClient tcpClient, SslStream sslStream)
    {
        _tcpClient = tcpClient;
        _sslStream = sslStream;
    }

    public static async Task<RawWebSocketClient> ConnectAsync(
        string host,
        string domain,
        int bufferSize,
        string path = "/apiws",
        string? sni = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        sni ??= domain;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout.Value);

        var tcp = new TcpClient();
        try
        {
            return await HandshakeAsync(tcp, host, domain, bufferSize, path, sni, timeout.Value, cts.Token);
        }
        catch
        {
            // Иначе при таймауте или ответе не 101 сокет остаётся висеть
            tcp.Dispose();
            throw;
        }
    }

    private static async Task<RawWebSocketClient> HandshakeAsync(
        TcpClient tcp,
        string host,
        string domain,
        int bufferSize,
        string path,
        string sni,
        TimeSpan timeout,
        CancellationToken token)
    {
        await tcp.ConnectAsync(host, 443, token);

        var networkStream = tcp.GetStream();
        networkStream.ReadTimeout = (int)timeout.TotalMilliseconds;
        networkStream.WriteTimeout = (int)timeout.TotalMilliseconds;

        var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false, (_, _, _, _) => true);
        await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = sni,
            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                | System.Security.Authentication.SslProtocols.Tls13
        }, token);

        try
        {
            tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            tcp.Client.ReceiveBufferSize = bufferSize;
            tcp.Client.SendBufferSize = bufferSize;
        }
        catch
        {
        }

        var wsKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var request = new StringBuilder()
            .Append("GET ").Append(path).Append(" HTTP/1.1\r\n")
            .Append("Host: ").Append(domain).Append("\r\n")
            .Append("Upgrade: websocket\r\n")
            .Append("Connection: Upgrade\r\n")
            .Append("Sec-WebSocket-Key: ").Append(wsKey).Append("\r\n")
            .Append("Sec-WebSocket-Version: 13\r\n")
            .Append("Sec-WebSocket-Protocol: binary\r\n")
            .Append("\r\n")
            .ToString();

        var requestBytes = Encoding.ASCII.GetBytes(request);
        await sslStream.WriteAsync(requestBytes, token);
        await sslStream.FlushAsync(token);

        var responseLines = new List<string>();
        while (true)
        {
            var line = await ReadLineAsync(sslStream, token);
            if (string.IsNullOrEmpty(line))
            {
                break;
            }

            responseLines.Add(line);
        }

        if (responseLines.Count == 0)
        {
            throw new WsHandshakeException(0, "empty response");
        }

        var parts = responseLines[0].Split(' ', 3);
        var statusCode = parts.Length >= 2 && int.TryParse(parts[1], out var code) ? code : 0;
        if (statusCode == 101)
        {
            return new RawWebSocketClient(tcp, sslStream);
        }

        throw new WsHandshakeException(statusCode, responseLines[0]);
    }

    public Task SendAsync(byte[] data, CancellationToken cancellationToken) =>
        WriteFrameAsync(0x2, data, cancellationToken);

    private async Task WriteFrameAsync(int opcode, byte[] data, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        var frame = BuildFrame(opcode, data, mask: true);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _sslStream.WriteAsync(frame, cancellationToken);
            await _sslStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SendBatchAsync(IReadOnlyList<byte[]> parts, CancellationToken cancellationToken)
    {
        foreach (var part in parts)
        {
            await SendAsync(part, cancellationToken);
        }
    }

    public async Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (!_closed)
        {
            var (fin, opcode, payload) = await ReadFrameAsync(cancellationToken);
            switch (opcode)
            {
                case 0x8:
                    _closed = true;
                    return null;
                case 0x9:
                    // Pong отдельным управляющим кадром. Раньше он уходил внутри binary-кадра и ломал поток MTProto
                    await WriteFrameAsync(0xA, payload, cancellationToken);
                    continue;
                case 0x0:
                case 0x1:
                case 0x2:
                    if (fin && _frag.Length == 0)
                    {
                        return payload;
                    }

                    _frag.Write(payload);
                    if (_frag.Length > MaxMessageLen)
                    {
                        throw new IOException($"WS message too large: {_frag.Length} bytes");
                    }

                    if (!fin)
                    {
                        continue;
                    }

                    var message = _frag.ToArray();
                    _frag.SetLength(0);
                    return message;
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        // Сокет закрываем и после close-кадра от сервера, раньше в этом случае он утекал
        if (!_closed)
        {
            _closed = true;
            try
            {
                await _sslStream.WriteAsync(BuildFrame(0x8, [], mask: true), CancellationToken.None);
                await _sslStream.FlushAsync(CancellationToken.None);
            }
            catch
            {
            }
        }

        await _sslStream.DisposeAsync();
        _tcpClient.Dispose();
    }

    private async Task<(bool Fin, int Opcode, byte[] Payload)> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var header = new byte[2];
        await ReadExactAsync(_sslStream, header, cancellationToken);
        var fin = (header[0] & 0x80) != 0;
        var opcode = header[0] & 0x0F;
        var rawLength = (ulong)(header[1] & 0x7F);

        if (rawLength == 126)
        {
            var lenBuf = new byte[2];
            await ReadExactAsync(_sslStream, lenBuf, cancellationToken);
            rawLength = BinaryPrimitives.ReadUInt16BigEndian(lenBuf);
        }
        else if (rawLength == 127)
        {
            var lenBuf = new byte[8];
            await ReadExactAsync(_sslStream, lenBuf, cancellationToken);
            rawLength = BinaryPrimitives.ReadUInt64BigEndian(lenBuf);
        }

        if (rawLength > MaxMessageLen)
        {
            throw new IOException($"WS frame too large: {rawLength} bytes");
        }

        var length = (int)rawLength;

        byte[] payload;
        if ((header[1] & 0x80) != 0)
        {
            var maskKey = new byte[4];
            await ReadExactAsync(_sslStream, maskKey, cancellationToken);
            payload = new byte[length];
            await ReadExactAsync(_sslStream, payload, cancellationToken);
            XorMask(payload, maskKey);
        }
        else
        {
            payload = new byte[length];
            await ReadExactAsync(_sslStream, payload, cancellationToken);
        }

        return (fin, opcode, payload);
    }

    private static byte[] BuildFrame(int opcode, byte[] data, bool mask)
    {
        var length = data.Length;
        using var ms = new MemoryStream();
        ms.WriteByte((byte)(0x80 | opcode));

        if (!mask)
        {
            WriteLength(ms, length, masked: false);
            ms.Write(data);
            return ms.ToArray();
        }

        var maskKey = RandomNumberGenerator.GetBytes(4);
        var masked = (byte[])data.Clone();
        XorMask(masked, maskKey);
        WriteLength(ms, length, masked: true);
        ms.Write(maskKey);
        ms.Write(masked);
        return ms.ToArray();
    }

    private static void WriteLength(Stream stream, int length, bool masked)
    {
        var maskBit = masked ? 0x80 : 0x00;
        if (length < 126)
        {
            stream.WriteByte((byte)(maskBit | length));
            return;
        }

        if (length < 65536)
        {
            stream.WriteByte((byte)(maskBit | 126));
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buf, (ushort)length);
            stream.Write(buf);
            return;
        }

        stream.WriteByte((byte)(maskBit | 127));
        Span<byte> buf64 = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buf64, (ulong)length);
        stream.Write(buf64);
    }

    private static void XorMask(byte[] data, byte[] maskKey)
    {
        for (var i = 0; i < data.Length; i++)
        {
            data[i] ^= maskKey[i % 4];
        }
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

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var buffer = new byte[1];
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer[0] == '\n')
            {
                break;
            }

            if (buffer[0] != '\r')
            {
                bytes.Add(buffer[0]);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
