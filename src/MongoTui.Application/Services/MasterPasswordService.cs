using MongoTui.Application.Abstractions.Persistence;

namespace MongoTui.Application.Services;

/// <summary>Coordinates master-password changes and re-encryption of every stored secret.</summary>
public sealed class MasterPasswordService
{
    private readonly SecretKeyService _secrets;
    private readonly ConnectionService _connections;
    private readonly AiSettingsService _ai;
    private readonly IStorageTransaction _transaction;

    public MasterPasswordService(SecretKeyService secrets, ConnectionService connections, AiSettingsService ai,
        IStorageTransaction transaction)
    {
        _secrets = secrets;
        _connections = connections;
        _ai = ai;
        _transaction = transaction;
    }

    public Task ChangeAsync(string newPassword, CancellationToken cancellationToken) =>
        _secrets.RunExclusiveAsync(token => ChangeCoreAsync(newPassword, token), cancellationToken);

    private async Task ChangeCoreAsync(string newPassword, CancellationToken cancellationToken)
    {
        MasterPasswordRotation? rotation = null;
        await _transaction.ExecuteAsync(async token =>
        {
            rotation = await _secrets.ChangeMasterPasswordAsync(newPassword, token);
            await _connections.ReEncryptSecretsAsync(rotation, token);
            await _ai.ReEncryptAsync(rotation, token);
        }, cancellationToken);
        rotation!.Commit();
    }
}
