using System.Security.Cryptography;
using MongoTui.Application.Abstractions.Security;

namespace MongoTui.Infrastructure.Security;

/// <summary>AES-256-GCM implementation of <see cref="ICryptoService"/>.</summary>
public sealed class AesGcmCryptoService : ICryptoService
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public byte[] Encrypt(byte[] plaintext, byte[] key, out byte[] nonce)
    {
        EnsureKeySize(key);

        nonce = RandomNumberGenerator.GetBytes(NonceSize);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[ciphertext.Length + TagSize];
        ciphertext.CopyTo(result.AsSpan());
        tag.CopyTo(result.AsSpan(ciphertext.Length));

        return result;
    }

    public byte[] Decrypt(byte[] ciphertext, byte[] nonce, byte[] key)
    {
        EnsureKeySize(key);

        if (nonce.Length != NonceSize)
        {
            throw new ArgumentException("Nonce must be 12 bytes.", nameof(nonce));
        }

        if (ciphertext.Length < TagSize)
        {
            throw new ArgumentException("Ciphertext is too short.", nameof(ciphertext));
        }

        var payloadLength = ciphertext.Length - TagSize;
        var payload = ciphertext.AsSpan(0, payloadLength);
        var tag = ciphertext.AsSpan(payloadLength);

        var plaintext = new byte[payloadLength];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, payload, tag, plaintext);

        return plaintext;
    }

    private static void EnsureKeySize(byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException("Key must be 32 bytes (AES-256).", nameof(key));
        }
    }
}
