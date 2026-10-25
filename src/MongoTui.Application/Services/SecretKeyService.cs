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
    private readonly ICryptoService _cryptoService;
    private byte[]? _kek;
    private readonly SemaphoreSlim _operation = new(1, 1);

    public SecretKeyService(ICryptoService cryptoService) => _cryptoService = cryptoService;

    public bool IsUnlocked => _kek is not null;

    /// <summary>Serializes secret storage operations with master-key rotation.</summary>
    public async Task RunExclusiveAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await action(cancellationToken).ConfigureAwait(false); }
        finally { _operation.Release(); }
    }

    /// <summary>Serializes an operation returning a value with master-key rotation.</summary>
    public async Task<T> RunExclusiveAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await action(cancellationToken).ConfigureAwait(false); }
        finally { _operation.Release(); }
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
    /// Starts a KEK rotation. The caller re-encrypts stored secrets with the returned
    /// rotation and then calls <see cref="KekRotation.Commit"/>.
    /// </summary>
    public KekRotation BeginRotation(byte[] newKek)
    {
        ArgumentNullException.ThrowIfNull(newKek);
        if (newKek.Length == 0)
        {
            throw new ArgumentException("The new KEK must not be empty.", nameof(newKek));
        }

        var oldKek = RequiredKek();
        return new KekRotation(_cryptoService, this, oldKek.ToArray(), newKek.ToArray());
    }

    internal void SetKek(byte[] kek) => _kek = kek;

    internal void ValidateKek(EncryptedSecret secret, byte[] kek) =>
        _cryptoService.Decrypt(secret.CipherText, secret.Nonce, kek);

    private byte[] RequiredKek() =>
        _kek ?? throw new SecretsLockedException();
}
