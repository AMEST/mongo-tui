using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Security;

/// <summary>
/// Derives the KEK from a master password using Argon2id. The random salt is
/// persisted so the same password always yields the same KEK. A small encrypted
/// check value lets us detect a wrong password at unlock time.
/// </summary>
public sealed class PasswordKekSource : IKekSource
{
    internal const string SaltSettingKey = "security.kek_salt";
    internal const string CheckSettingKey = "security.kek_check";

    private const int KeySize = 32;
    private const int SaltSize = 16;

    private static readonly byte[] CheckPlaintext = Encoding.UTF8.GetBytes("mongotui-kek-check");

    private readonly ISettingsStore _settings;
    private readonly ICryptoService _cryptoService;
    private readonly Argon2Parameters _parameters;

    public PasswordKekSource(
        ISettingsStore settings,
        ICryptoService cryptoService,
        Argon2Parameters parameters)
    {
        _settings = settings;
        _cryptoService = cryptoService;
        _parameters = parameters;
    }

    public SecurityMode Mode => SecurityMode.Password;

    public bool IsAvailable => true;

    public async Task<bool> HasMaterialAsync(CancellationToken cancellationToken) =>
        await _settings.GetAsync(SaltSettingKey, cancellationToken) is not null;

    public Task<byte[]?> TryGetKekAsync(CancellationToken cancellationToken) =>
        Task.FromResult<byte[]?>(null);

    public async Task<byte[]> UnlockAsync(string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new DomainException("Master password must not be empty.");
        }

        var salt = await GetOrCreateSaltAsync(cancellationToken);
        var kek = DeriveKey(password, salt);

        await VerifyOrCreateCheckAsync(kek, cancellationToken);

        return kek;
    }

    public async Task<byte[]> CreateAsync(string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new DomainException("Master password must not be empty.");
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        await _settings.SetAsync(SaltSettingKey, Convert.ToBase64String(salt), cancellationToken);

        var kek = DeriveKey(password, salt);
        await StoreCheckAsync(kek, cancellationToken);

        return kek;
    }

    public Task DeleteAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(
            _settings.DeleteAsync(SaltSettingKey, cancellationToken),
            _settings.DeleteAsync(CheckSettingKey, cancellationToken));

    private async Task<byte[]> GetOrCreateSaltAsync(CancellationToken cancellationToken)
    {
        var stored = await _settings.GetAsync(SaltSettingKey, cancellationToken);
        if (stored is not null)
        {
            return Convert.FromBase64String(stored);
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        await _settings.SetAsync(SaltSettingKey, Convert.ToBase64String(salt), cancellationToken);

        return salt;
    }

    private async Task VerifyOrCreateCheckAsync(byte[] kek, CancellationToken cancellationToken)
    {
        var stored = await _settings.GetAsync(CheckSettingKey, cancellationToken);
        if (stored is null)
        {
            await StoreCheckAsync(kek, cancellationToken);
            return;
        }

        var blob = Convert.FromBase64String(stored);
        var nonceLength = blob[0];
        var nonce = blob.AsSpan(1, nonceLength).ToArray();
        var ciphertext = blob.AsSpan(1 + nonceLength).ToArray();

        try
        {
            var plaintext = _cryptoService.Decrypt(ciphertext, nonce, kek);
            if (!plaintext.AsSpan().SequenceEqual(CheckPlaintext))
            {
                throw new DomainException("Invalid master password.");
            }
        }
        catch (CryptographicException)
        {
            throw new DomainException("Invalid master password.");
        }
    }

    private async Task StoreCheckAsync(byte[] kek, CancellationToken cancellationToken)
    {
        var ciphertext = _cryptoService.Encrypt(CheckPlaintext, kek, out var nonce);

        var blob = new byte[1 + nonce.Length + ciphertext.Length];
        blob[0] = (byte)nonce.Length;
        nonce.CopyTo(blob.AsSpan(1));
        ciphertext.CopyTo(blob.AsSpan(1 + nonce.Length));

        await _settings.SetAsync(CheckSettingKey, Convert.ToBase64String(blob), cancellationToken);
    }

    private byte[] DeriveKey(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = _parameters.DegreesOfParallelism,
            MemorySize = _parameters.MemorySizeKb,
            Iterations = _parameters.Iterations,
        };

        return argon2.GetBytes(KeySize);
    }
}
