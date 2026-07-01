namespace WPT.Core.Services.TgWsProxy;

internal sealed class MsgSplitter
{
    private readonly AesCtr _decryptor;
    private readonly uint _proto;
    private readonly List<byte> _cipherBuf = [];
    private readonly List<byte> _plainBuf = [];
    private bool _disabled;

    public MsgSplitter(byte[] relayInit, uint protoInt)
    {
        var relayKey = relayInit.AsSpan(8, 32).ToArray();
        var relayIv = relayInit.AsSpan(40, 16).ToArray();
        _decryptor = new AesCtr(relayKey, relayIv);
        _decryptor.Update(TgWsProxyConstants.Zero64);
        _proto = protoInt;
    }

    public IReadOnlyList<byte[]> Split(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty)
        {
            return [];
        }

        if (_disabled)
        {
            return [chunk.ToArray()];
        }

        _cipherBuf.AddRange(chunk.ToArray());
        _plainBuf.AddRange(_decryptor.Update(chunk));

        var parts = new List<byte[]>();
        var offset = 0;
        var bufLen = _cipherBuf.Count;

        while (offset < bufLen)
        {
            var packetLen = NextPacketLen(offset, bufLen - offset);
            if (packetLen == null)
            {
                break;
            }

            if (packetLen <= 0)
            {
                parts.Add(_cipherBuf.Skip(offset).ToArray());
                offset = bufLen;
                _disabled = true;
                break;
            }

            parts.Add(_cipherBuf.Skip(offset).Take(packetLen.Value).ToArray());
            offset += packetLen.Value;
        }

        if (offset > 0)
        {
            _cipherBuf.RemoveRange(0, offset);
            _plainBuf.RemoveRange(0, offset);
        }

        return parts;
    }

    public IReadOnlyList<byte[]> Flush()
    {
        if (_cipherBuf.Count == 0)
        {
            return [];
        }

        var tail = _cipherBuf.ToArray();
        _cipherBuf.Clear();
        _plainBuf.Clear();
        return [tail];
    }

    private int? NextPacketLen(int offset, int avail)
    {
        if (avail <= 0)
        {
            return null;
        }

        if (_proto == TgWsProxyConstants.ProtoAbridgedInt)
        {
            return NextAbridgedLen(offset, avail);
        }

        if (_proto is TgWsProxyConstants.ProtoIntermediateInt or TgWsProxyConstants.ProtoPaddedIntermediateInt)
        {
            return NextIntermediateLen(offset, avail);
        }

        return 0;
    }

    private int? NextAbridgedLen(int offset, int avail)
    {
        var first = _plainBuf[offset];
        int payloadLen;
        int headerLen;

        if (first is 0x7F or 0xFF)
        {
            if (avail < 4)
            {
                return null;
            }

            payloadLen = _plainBuf[offset + 1]
                | (_plainBuf[offset + 2] << 8)
                | (_plainBuf[offset + 3] << 16);
            payloadLen *= 4;
            headerLen = 4;
        }
        else
        {
            payloadLen = (first & 0x7F) * 4;
            headerLen = 1;
        }

        if (payloadLen <= 0)
        {
            return 0;
        }

        var packetLen = headerLen + payloadLen;
        return avail < packetLen ? null : packetLen;
    }

    private int? NextIntermediateLen(int offset, int avail)
    {
        if (avail < 4)
        {
            return null;
        }

        var payloadLen = (int)(BitConverter.ToUInt32(_plainBuf.Skip(offset).Take(4).ToArray(), 0) & 0x7FFFFFFF);
        if (payloadLen <= 0)
        {
            return 0;
        }

        var packetLen = 4 + payloadLen;
        return avail < packetLen ? null : packetLen;
    }
}
