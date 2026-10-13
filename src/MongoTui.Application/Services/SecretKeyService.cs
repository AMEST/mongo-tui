using System.Text;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

/// <summary>
/// Owns the Key Encryption Key (KEK) and protects application secrets with it. Shared by every
/// feature that stores encrypted values (connection passwords, AI API key, ...).
/// </summary>
public sealed class SecretKeyService
{
    private readonly IKeyProvider _keyProvider;
    private readonly ICryptoService _cryptoService;
    private byte[]? _kek;

    public SecretKeyService(IKeyProvider keyProvider, ICryptoService cryptoService)
    {
        _keyProvider = keyProvider;
        _cryptoService = cryptoService;
    }

    public bool IsUnlocked => _kek is not null;

    public async Task UnlockAsync(string masterPassword, CancellationToken cancellationToken) =>
        _kek = await _keyProvider.UnlockWithMasterPasswordAsync(masterPassword, cancellationToken);

    /// <summary>
    /// Tries to unlock without user input. Returns true when already unlocked or when an
    /// OS keyring supplied the KEK; false when a master password is required.
    /// </summary>
    public async Task<bool> TryAutoUnlockAsync(CancellationToken cancellationToken)
    {
        if (_kek is not null)
        {
            return true;
        }

        if (!_keyProvider.IsKeyringAvailable)
        {
            return false;
        }

        _kek = await _keyProvider.GetOrCreateKekAsync(cancellationToken);
        return true;
    }

    public void Lock() => _kek = null;

    /// <summary>Encrypts <paramref name="plaintext"/> with the current KEK.</summary>
    public EncryptedSecret Protect(string plaintext)
    {
        var ciphertext = _cryptoService.Encrypt(Encoding.UTF8.GetBytes(plaintext), RequiredKek(), out var nonce);
        return new EncryptedSecret(nonce, ciphertext);
    }

    /// <summary>Decrypts a value previously produced by <see cref="Protect"/>.</summary>
    public string Unprotect(EncryptedSecret secret) =>
        Encoding.UTF8.GetString(_cryptoService.Decrypt(secret.CipherText, secret.Nonce, RequiredKek()));

    /// <summary>
    /// Starts a master-password change. The caller re-encrypts stored secrets with the returned
    /// rotation and then calls <see cref="MasterPasswordRotation.Commit"/>.
    /// </summary>
    public async Task<MasterPasswordRotation> ChangeMasterPasswordAsync(
        string newPassword,
        CancellationToken cancellationToken)
    {
        var oldKek = RequiredKek();
        var newKek = await _keyProvider.ChangeMasterPasswordAsync(newPassword, cancellationToken);

        return new MasterPasswordRotation(_cryptoService, this, oldKek, newKek);
    }

    internal void SetKek(byte[] kek) => _kek = kek;

    private byte[] RequiredKek() =>
        _kek ?? throw new DomainException("Secrets are locked; unlock with the master password first.");
}
