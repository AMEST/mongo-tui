namespace MongoTui.Application.Services;

/// <summary>Coordinates master-password changes and re-encryption of every stored secret.</summary>
public sealed class MasterPasswordService
{
    private readonly SecretKeyService _secrets;
    private readonly ConnectionService _connections;
    private readonly AiSettingsService _ai;

    public MasterPasswordService(SecretKeyService secrets, ConnectionService connections, AiSettingsService ai)
    {
        _secrets = secrets;
        _connections = connections;
        _ai = ai;
    }

    public async Task ChangeAsync(string newPassword, CancellationToken cancellationToken)
    {
        var rotation = await _secrets.ChangeMasterPasswordAsync(newPassword, cancellationToken);

        await _connections.ReEncryptSecretsAsync(rotation, cancellationToken);
        await _ai.ReEncryptAsync(rotation, cancellationToken);

        rotation.Commit();
    }
}
