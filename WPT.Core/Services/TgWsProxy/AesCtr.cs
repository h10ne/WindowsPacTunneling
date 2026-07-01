using System.Security.Cryptography;

namespace WPT.Core.Services.TgWsProxy;

internal sealed class AesCtr
{
    private readonly ICryptoTransform _encryptor;
    private readonly byte[] _counter;
    private byte[] _keystream = [];
    private int _keystreamPos;

    public AesCtr(byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        _encryptor = aes.CreateEncryptor();
        _counter = new byte[16];
        iv.AsSpan(0, Math.Min(iv.Length, 16)).CopyTo(_counter);
    }

    public byte[] Update(ReadOnlySpan<byte> input)
    {
        var output = new byte[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            if (_keystreamPos >= _keystream.Length)
            {
                _keystream = new byte[16];
                _encryptor.TransformBlock(_counter, 0, 16, _keystream, 0);
                IncrementCounter();
                _keystreamPos = 0;
            }

            output[i] = (byte)(input[i] ^ _keystream[_keystreamPos++]);
        }

        return output;
    }

    private void IncrementCounter()
    {
        for (var i = 15; i >= 0; i--)
        {
            if (++_counter[i] != 0)
            {
                break;
            }
        }
    }
}
