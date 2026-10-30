using MongoTui.Application.Abstractions.Mongo;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Services;

/// <summary>Manages connection profiles and their encrypted secrets.</summary>
public sealed class ConnectionService
{
    public const int TestTimeoutSeconds = 3;

    private readonly IConnectionRepository _repository;
    private readonly SecretKeyService _secrets;
    private readonly IMongoGateway _gateway;
    private readonly ConnectionTunnelService _tunnels;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(
        IConnectionRepository repository,
        SecretKeyService secrets,
        IMongoGateway gateway,
        ConnectionTunnelService tunnels,
        ILogger<ConnectionService> logger)
    {
        _repository = repository;
        _secrets = secrets;
        _gateway = gateway;
        _tunnels = tunnels;
        _logger = logger;
    }

    /// <summary>Re-encrypts every stored connection secret with the rotated KEK.</summary>
    public async Task ReEncryptSecretsAsync(KekRotation rotation, CancellationToken cancellationToken)
    {
        foreach (var connection in await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (connection.Secret is not { } secret)
            {
                continue;
            }

            await _repository.UpdateAsync(
                connection with { Secret = rotation.ReEncrypt(secret) },
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task ValidateKekAsync(byte[] kek, CancellationToken cancellationToken)
    {
        foreach (var connection in await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (connection.Secret is { } secret)
            {
                _secrets.ValidateKek(secret, kek);
            }
        }
    }

    public async Task<IReadOnlyList<ConnectionProfile>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return stored.Select(connection => connection.Profile).ToList();
    }

    public async Task<ConnectionProfile?> GetProfileAsync(ConnectionId id, CancellationToken cancellationToken) =>
        (await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false))?.Profile;

    /// <summary>Prepares submitted fields, loading saved settings and a secret only when needed.</summary>
    public async Task<(ConnectionProfile Profile, string? Secret)> PrepareAsync(
        ConnectionProfileDraft draft, CancellationToken cancellationToken)
    {
        var parsed = ConnectionUriParser.Parse(draft.Uri);
        var original = draft.Id is null ? null : await GetProfileAsync(draft.Id, cancellationToken).ConfigureAwait(false);
        var secret = original is not null && string.IsNullOrEmpty(draft.Password) && parsed.Password is null
            ? await GetSecretAsync(original.Id, cancellationToken).ConfigureAwait(false) : null;
        return ConnectionProfilePreparation.Prepare(draft, original, secret);
    }

    public async Task SaveAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        try
        {
            await _secrets.RunExclusiveAsync(token => SaveCoreAsync(profile, secret, token), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Connection profile {ConnectionId} saved", profile.Id.Value);
        }
        catch (OperationCanceledException) { _logger.LogDebug("Connection save cancelled"); throw; }
        catch (Exception exception)
        {
            _logger.LogError("Connection profile {ConnectionId} save failed: {Failure}", profile.Id.Value, ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    private async Task SaveCoreAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        (profile, secret) = ConnectionCredentials.Extract(profile, secret);
        var stored = new StoredConnection(profile, EncryptSecret(secret));
        var existing = await _repository.GetAsync(profile.Id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            await _repository.AddAsync(stored, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _repository.UpdateAsync(stored, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(ConnectionId id, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Connection profile {ConnectionId} deleted", id.Value);
        }
        catch (OperationCanceledException) { _logger.LogDebug("Connection {ConnectionId} deletion cancelled", id.Value); throw; }
        catch (Exception exception)
        {
            _logger.LogError("Connection profile {ConnectionId} deletion failed: {Failure}", id.Value, ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    /// <summary>Removes every stored connection secret without deleting the profiles.</summary>
    public async Task ClearSecretsAsync(CancellationToken cancellationToken)
    {
        foreach (var connection in await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var (profile, _) = ConnectionCredentials.Extract(connection.Profile, null);
            if (connection.Secret is not null || profile != connection.Profile)
            {
                await _repository.UpdateAsync(connection with { Profile = profile, Secret = null }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task<string?> GetSecretAsync(ConnectionId id, CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(async token =>
        {
            var stored = await _repository.GetAsync(id, token).ConfigureAwait(false);
            return stored?.Secret is null ? null : _secrets.Unprotect(stored.Secret);
        }, cancellationToken);

    public Task<MongoConnectionTarget> ResolveTargetAsync(ConnectionId id, CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(token => ResolveTargetCoreAsync(id, token), cancellationToken);

    private async Task<MongoConnectionTarget> ResolveTargetCoreAsync(ConnectionId id, CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"Connection '{id}' was not found.");

        var secret = stored.Secret is null ? null : _secrets.Unprotect(stored.Secret);

        return new MongoConnectionTarget(BuildConnectionString(stored.Profile, secret));
    }

    public async Task TestConnectionAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Testing connection {ConnectionId} (SSH: {Ssh})", profile.Id.Value, profile.Ssh.Enabled);
        try
        {
            await TestConnectionCoreAsync(profile, secret, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Connection {ConnectionId} test completed", profile.Id.Value);
        }
        catch (OperationCanceledException) { _logger.LogDebug("Connection test cancelled"); throw; }
        catch (Exception exception)
        {
            _logger.LogWarning("Connection {ConnectionId} test failed: {Failure}", profile.Id.Value, ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    private async Task TestConnectionCoreAsync(ConnectionProfile profile, string? secret, CancellationToken cancellationToken)
    {
        var target = new MongoConnectionTarget(BuildConnectionString(profile, secret));
        await using var connection = await _tunnels.OpenAsync(profile, target, cancellationToken).ConfigureAwait(false);
        await _gateway.ListDatabasesAsync(connection.Target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds an effective connection string by injecting credentials when authentication is used.</summary>
    public static string BuildConnectionString(ConnectionProfile profile, string? secret)
    {
        (profile, secret) = ConnectionCredentials.Extract(profile, secret);
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

    /// <summary>Moves credentials embedded in a URI into the encrypted secret storage.</summary>
    public async Task MigrateCredentialsAsync(CancellationToken cancellationToken)
    {
        foreach (var stored in await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var parsed = ConnectionUriParser.Parse(stored.Profile.Uri);
            if (parsed.Username is null && parsed.Password is null) continue;
            var secret = stored.Secret is null ? null : _secrets.Unprotect(stored.Secret);
            var normalized = ConnectionCredentials.Extract(stored.Profile, secret);
            await _repository.UpdateAsync(new StoredConnection(normalized.Profile,
                EncryptSecret(normalized.Secret)), cancellationToken).ConfigureAwait(false);
        }
    }

    private EncryptedSecret? EncryptSecret(string? secret) =>
        string.IsNullOrEmpty(secret) ? null : _secrets.Protect(secret);

}
