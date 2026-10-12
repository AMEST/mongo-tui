using System.Text;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Services;

/// <summary>Manages connection profiles and their encrypted secrets.</summary>
public sealed class ConnectionService
{
    private readonly IConnectionRepository _repository;
    private readonly ICryptoService _cryptoService;
    private readonly IKeyProvider _keyProvider;
    private readonly IMongoGateway _gateway;

    private byte[]? _kek;

    public ConnectionService(
        IConnectionRepository repository,
        ICryptoService cryptoService,
        IKeyProvider keyProvider,
        IMongoGateway gateway)
    {
        _repository = repository;
        _cryptoService = cryptoService;
        _keyProvider = keyProvider;
        _gateway = gateway;
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

    /// <summary>
    /// Changes the master password: derives a new KEK and re-encrypts all stored secrets.
    /// Requires the secrets to be currently unlocked.
    /// </summary>
    public async Task ChangeMasterPasswordAsync(string newPassword, CancellationToken cancellationToken)
    {
        var oldKek = _kek ?? throw new DomainException("Secrets are locked; unlock before changing the password.");

        var newKek = await _keyProvider.ChangeMasterPasswordAsync(newPassword, cancellationToken);

        var stored = await _repository.GetAllAsync(cancellationToken);
        foreach (var connection in stored)
        {
            if (connection.Secret is not { } secret)
            {
                continue;
            }

            var plaintext = _cryptoService.Decrypt(secret.CipherText, secret.Nonce, oldKek);
            var ciphertext = _cryptoService.Encrypt(plaintext, newKek, out var nonce);

            await _repository.UpdateAsync(
                connection with { Secret = new EncryptedSecret(nonce, ciphertext) },
                cancellationToken);
        }

        _kek = newKek;
    }

    public void Lock() => _kek = null;

    public async Task<IReadOnlyList<ConnectionProfile>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAllAsync(cancellationToken);
        return stored.Select(connection => connection.Profile).ToList();
    }

    public async Task<ConnectionProfile?> GetProfileAsync(ConnectionId id, CancellationToken cancellationToken) =>
        (await _repository.GetAsync(id, cancellationToken))?.Profile;

    public async Task SaveAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        var stored = new StoredConnection(profile, EncryptSecret(secret));
        var existing = await _repository.GetAsync(profile.Id, cancellationToken);

        if (existing is null)
        {
            await _repository.AddAsync(stored, cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(stored, cancellationToken);
        }
    }

    public Task DeleteAsync(ConnectionId id, CancellationToken cancellationToken) =>
        _repository.DeleteAsync(id, cancellationToken);

    public async Task<string?> GetSecretAsync(ConnectionId id, CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAsync(id, cancellationToken);
        return stored?.Secret is null ? null : DecryptSecret(stored.Secret);
    }

    public async Task<MongoConnectionTarget> ResolveTargetAsync(ConnectionId id, CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAsync(id, cancellationToken)
            ?? throw new DomainException($"Connection '{id}' was not found.");

        var secret = stored.Secret is null ? null : DecryptSecret(stored.Secret);

        return new MongoConnectionTarget(BuildConnectionString(stored.Profile, secret));
    }

    public async Task TestConnectionAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        var target = new MongoConnectionTarget(BuildConnectionString(profile, secret));
        await _gateway.ListDatabasesAsync(target, cancellationToken);
    }

    /// <summary>Builds an effective connection string by injecting credentials when authentication is used.</summary>
    public static string BuildConnectionString(ConnectionProfile profile, string? secret)
    {
        if (profile.Auth.Kind == AuthKind.None || string.IsNullOrEmpty(profile.Auth.Username))
        {
            return profile.Uri;
        }

        var schemeEnd = profile.Uri.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return profile.Uri;
        }

        schemeEnd += 3;
        var credentials =
            $"{Uri.EscapeDataString(profile.Auth.Username)}:{Uri.EscapeDataString(secret ?? string.Empty)}@";

        var result = profile.Uri[..schemeEnd] + credentials + profile.Uri[schemeEnd..];

        if (!string.IsNullOrWhiteSpace(profile.Auth.AuthDatabase)
            && !result.Contains("authSource=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = result.Contains('?') ? '&' : '?';
            result += $"{separator}authSource={Uri.EscapeDataString(profile.Auth.AuthDatabase)}";
        }

        return result;
    }

    private EncryptedSecret? EncryptSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return null;
        }

        var ciphertext = _cryptoService.Encrypt(Encoding.UTF8.GetBytes(secret), RequiredKek(), out var nonce);
        return new EncryptedSecret(nonce, ciphertext);
    }

    private string DecryptSecret(EncryptedSecret secret)
    {
        var plaintext = _cryptoService.Decrypt(secret.CipherText, secret.Nonce, RequiredKek());
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] RequiredKek() =>
        _kek ?? throw new DomainException("Secrets are locked; unlock with the master password first.");
}
