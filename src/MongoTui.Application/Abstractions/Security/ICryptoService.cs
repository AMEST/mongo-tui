namespace MongoTui.Application.Abstractions.Security;

/// <summary>Symmetric authenticated encryption (AES-256-GCM).</summary>
public interface ICryptoService
{
    /// <summary>Encrypts <paramref name="plaintext"/> and returns ciphertext with an authentication tag appended.</summary>
    byte[] Encrypt(byte[] plaintext, byte[] key, out byte[] nonce);

    /// <summary>Decrypts ciphertext produced by <see cref="Encrypt"/>; throws when the tag does not match.</summary>
    byte[] Decrypt(byte[] ciphertext, byte[] nonce, byte[] key);
}
