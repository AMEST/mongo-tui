namespace MongoTui.Application.Models;

/// <summary>AES-GCM payload: the nonce and the authenticated ciphertext (with tag).</summary>
public sealed record EncryptedSecret(byte[] Nonce, byte[] CipherText);
