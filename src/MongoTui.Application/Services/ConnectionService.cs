using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Services;

/// <summary>Manages connection profiles and their encrypted secrets.</summary>
public sealed class ConnectionService
{
    private readonly IConnectionRepository _repository;
    private readonly SecretKeyService _secrets;
    private readonly IMongoGateway _gateway;

    public ConnectionService(
        IConnectionRepository repository,
        SecretKeyService secrets,
        IMongoGateway gateway)
    {
        _repository = repository;
        _secrets = secrets;
        _gateway = gateway;
    }

    public bool IsUnlocked => _secrets.IsUnlocked;

    public Task UnlockAsync(string masterPassword, CancellationToken cancellationToken) =>
        _secrets.UnlockAsync(masterPassword, cancellationToken);

    /// <summary>
    /// Tries to unlock without user input. Returns true when already unlocked or when an
    /// OS keyring supplied the KEK; false when a master password is required.
    /// </summary>
    public Task<bool> TryAutoUnlockAsync(CancellationToken cancellationToken) =>
        _secrets.TryAutoUnlockAsync(cancellationToken);

    public void Lock() => _secrets.Lock();

    /// <summary>Re-encrypts every stored connection secret with the rotated KEK.</summary>
    public async Task ReEncryptSecretsAsync(MasterPasswordRotation rotation, CancellationToken cancellationToken)
    {
        foreach (var connection in await _repository.GetAllAsync(cancellationToken))
        {
            if (connection.Secret is not { } secret)
            {
                continue;
            }

            await _repository.UpdateAsync(
                connection with { Secret = rotation.ReEncrypt(secret) },
                cancellationToken);
        }
    }

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
        return stored?.Secret is null ? null : _secrets.Unprotect(stored.Secret);
    }

    public async Task<MongoConnectionTarget> ResolveTargetAsync(ConnectionId id, CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAsync(id, cancellationToken)
            ?? throw new DomainException($"Connection '{id}' was not found.");

        var secret = stored.Secret is null ? null : _secrets.Unprotect(stored.Secret);

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

    private EncryptedSecret? EncryptSecret(string? secret) =>
        string.IsNullOrEmpty(secret) ? null : _secrets.Protect(secret);
}
