using MongoTui.Application.Abstractions.Security;

namespace MongoTui.Infrastructure.Security;

/// <summary>Prefers the OS keyring and falls back to the master-password KEK.</summary>
public sealed class FallbackKeyProvider : IKeyProvider
{
    private readonly KeyringKeyProvider _keyring;
    private readonly MasterPasswordKeyProvider _masterPassword;

    public FallbackKeyProvider(KeyringKeyProvider keyring, MasterPasswordKeyProvider masterPassword)
    {
        _keyring = keyring;
        _masterPassword = masterPassword;
    }

    public bool IsKeyringAvailable => _keyring.IsKeyringAvailable;

    public Task<byte[]> GetOrCreateKekAsync(CancellationToken cancellationToken) =>
        _keyring.IsKeyringAvailable
            ? _keyring.GetOrCreateKekAsync(cancellationToken)
            : _masterPassword.GetOrCreateKekAsync(cancellationToken);

    public Task<byte[]> UnlockWithMasterPasswordAsync(string password, CancellationToken cancellationToken) =>
        _masterPassword.UnlockWithMasterPasswordAsync(password, cancellationToken);

    public Task<byte[]> ChangeMasterPasswordAsync(string newPassword, CancellationToken cancellationToken) =>
        _masterPassword.ChangeMasterPasswordAsync(newPassword, cancellationToken);
}
