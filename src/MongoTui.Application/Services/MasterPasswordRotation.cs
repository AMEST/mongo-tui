using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;

namespace MongoTui.Application.Services;

/// <summary>
/// Re-encrypts secrets from the old KEK to the new KEK during a master-password change.
/// Call <see cref="Commit"/> once every store has been re-encrypted.
/// </summary>
public sealed class MasterPasswordRotation
{
    private readonly ICryptoService _cryptoService;
    private readonly SecretKeyService _owner;
    private readonly byte[] _oldKek;
    private readonly byte[] _newKek;

    internal MasterPasswordRotation(
        ICryptoService cryptoService,
        SecretKeyService owner,
        byte[] oldKek,
        byte[] newKek)
    {
        _cryptoService = cryptoService;
        _owner = owner;
        _oldKek = oldKek;
        _newKek = newKek;
    }

    /// <summary>Decrypts with the old KEK and re-encrypts with the new one.</summary>
    public EncryptedSecret ReEncrypt(EncryptedSecret secret)
    {
        var plaintext = _cryptoService.Decrypt(secret.CipherText, secret.Nonce, _oldKek);
        var ciphertext = _cryptoService.Encrypt(plaintext, _newKek, out var nonce);

        return new EncryptedSecret(nonce, ciphertext);
    }

    /// <summary>Adopts the new KEK as the active one.</summary>
    public void Commit() => _owner.SetKek(_newKek);
}
