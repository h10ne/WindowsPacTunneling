using System.Security.Cryptography;

namespace WPT.Core.Services.TgWsProxy;

internal sealed class TgWsCryptoContext
{
    public AesCtr ClientDecryptor { get; }

    public AesCtr ClientEncryptor { get; }

    public AesCtr TelegramEncryptor { get; }

    public AesCtr TelegramDecryptor { get; }

    private TgWsCryptoContext(AesCtr clientDecryptor, AesCtr clientEncryptor, AesCtr telegramEncryptor, AesCtr telegramDecryptor)
    {
        ClientDecryptor = clientDecryptor;
        ClientEncryptor = clientEncryptor;
        TelegramEncryptor = telegramEncryptor;
        TelegramDecryptor = telegramDecryptor;
    }

    public static TgWsCryptoContext Create(byte[] clientDecPrekeyIv, byte[] secret, byte[] relayInit)
    {
        var cltDecPrekey = clientDecPrekeyIv.AsSpan(0, TgWsProxyConstants.PrekeyLen).ToArray();
        var cltDecIv = clientDecPrekeyIv.AsSpan(TgWsProxyConstants.PrekeyLen, TgWsProxyConstants.IvLen).ToArray();
        var cltDecKey = SHA256.HashData(cltDecPrekey.Concat(secret).ToArray());

        var cltEncPrekeyIv = clientDecPrekeyIv.Reverse().ToArray();
        var cltEncKey = SHA256.HashData(cltEncPrekeyIv.AsSpan(0, TgWsProxyConstants.PrekeyLen).ToArray().Concat(secret).ToArray());
        var cltEncIv = cltEncPrekeyIv.AsSpan(TgWsProxyConstants.PrekeyLen, TgWsProxyConstants.IvLen).ToArray();

        var clientDecryptor = new AesCtr(cltDecKey, cltDecIv);
        var clientEncryptor = new AesCtr(cltEncKey, cltEncIv);
        clientDecryptor.Update(TgWsProxyConstants.Zero64);

        var relayEncKey = relayInit.AsSpan(TgWsProxyConstants.SkipLen, TgWsProxyConstants.PrekeyLen).ToArray();
        var relayEncIv = relayInit.AsSpan(
            TgWsProxyConstants.SkipLen + TgWsProxyConstants.PrekeyLen,
            TgWsProxyConstants.IvLen).ToArray();

        var relayDecPrekeyIv = relayInit.AsSpan(
            TgWsProxyConstants.SkipLen,
            TgWsProxyConstants.PrekeyLen + TgWsProxyConstants.IvLen).ToArray().Reverse().ToArray();
        var relayDecKey = relayDecPrekeyIv.AsSpan(0, TgWsProxyConstants.KeyLen).ToArray();
        var relayDecIv = relayDecPrekeyIv.AsSpan(TgWsProxyConstants.KeyLen, TgWsProxyConstants.IvLen).ToArray();

        var telegramEncryptor = new AesCtr(relayEncKey, relayEncIv);
        var telegramDecryptor = new AesCtr(relayDecKey, relayDecIv);
        telegramEncryptor.Update(TgWsProxyConstants.Zero64);

        return new TgWsCryptoContext(clientDecryptor, clientEncryptor, telegramEncryptor, telegramDecryptor);
    }
}

internal static class TgWsHandshake
{
    private static readonly HashSet<byte> ReservedFirstBytes = [0xEF];
    private static readonly HashSet<uint> ReservedStarts =
    [
        0x48454144, 0x504F5354, 0x47455420, 0xEEEEEEEE, 0xDDDDDDDD, 0x16030102
    ];

    public sealed record HandshakeResult(int DcId, bool IsMedia, uint ProtoInt, byte[] ClientDecPrekeyIv);

    public static HandshakeResult? TryParse(byte[] handshake, byte[] secret)
    {
        var decPrekeyAndIv = handshake.AsSpan(TgWsProxyConstants.SkipLen, TgWsProxyConstants.PrekeyLen + TgWsProxyConstants.IvLen).ToArray();
        var decPrekey = decPrekeyAndIv.AsSpan(0, TgWsProxyConstants.PrekeyLen).ToArray();
        var decIv = decPrekeyAndIv.AsSpan(TgWsProxyConstants.PrekeyLen, TgWsProxyConstants.IvLen).ToArray();
        var decKey = SHA256.HashData(decPrekey.Concat(secret).ToArray());

        var decryptor = new AesCtr(decKey, decIv);
        var decrypted = decryptor.Update(handshake);

        var protoTag = decrypted.AsSpan(TgWsProxyConstants.ProtoTagPos, 4);
        if (!protoTag.SequenceEqual(TgWsProxyConstants.ProtoTagAbridged)
            && !protoTag.SequenceEqual(TgWsProxyConstants.ProtoTagIntermediate)
            && !protoTag.SequenceEqual(TgWsProxyConstants.ProtoTagSecure))
        {
            return null;
        }

        var dcIdx = BitConverter.ToInt16(decrypted, TgWsProxyConstants.DcIdxPos);
        var dcId = Math.Abs(dcIdx);
        var isMedia = dcIdx < 0;

        uint protoInt = protoTag.SequenceEqual(TgWsProxyConstants.ProtoTagAbridged)
            ? TgWsProxyConstants.ProtoAbridgedInt
            : protoTag.SequenceEqual(TgWsProxyConstants.ProtoTagIntermediate)
                ? TgWsProxyConstants.ProtoIntermediateInt
                : TgWsProxyConstants.ProtoPaddedIntermediateInt;

        return new HandshakeResult(dcId, isMedia, protoInt, decPrekeyAndIv);
    }

    public static byte[] GenerateRelayInit(ReadOnlySpan<byte> protoTag, short dcIdx)
    {
        Span<byte> rnd = stackalloc byte[TgWsProxyConstants.HandshakeLen];
        while (true)
        {
            RandomNumberGenerator.Fill(rnd);
            if (ReservedFirstBytes.Contains(rnd[0]))
            {
                continue;
            }

            var start = BitConverter.ToUInt32(rnd[..4]);
            if (ReservedStarts.Contains(start))
            {
                continue;
            }

            if (BitConverter.ToUInt32(rnd.Slice(4, 4)) == 0)
            {
                continue;
            }

            break;
        }

        var rndBytes = rnd.ToArray();
        var encKey = rndBytes.AsSpan(TgWsProxyConstants.SkipLen, TgWsProxyConstants.PrekeyLen).ToArray();
        var encIv = rndBytes.AsSpan(
            TgWsProxyConstants.SkipLen + TgWsProxyConstants.PrekeyLen,
            TgWsProxyConstants.IvLen).ToArray();

        var encryptor = new AesCtr(encKey, encIv);

        var dcBytes = BitConverter.GetBytes(dcIdx);
        var tailPlain = new byte[8];
        protoTag.CopyTo(tailPlain);
        dcBytes.AsSpan(0, 2).CopyTo(tailPlain.AsSpan(4, 2));
        RandomNumberGenerator.Fill(tailPlain.AsSpan(6, 2));

        var encryptedFull = encryptor.Update(rndBytes);
        var encryptedTail = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            var keystreamTail = (byte)(encryptedFull[56 + i] ^ rndBytes[56 + i]);
            encryptedTail[i] = (byte)(tailPlain[i] ^ keystreamTail);
        }

        var result = rndBytes;
        encryptedTail.CopyTo(result, TgWsProxyConstants.ProtoTagPos);
        return result;
    }
}
