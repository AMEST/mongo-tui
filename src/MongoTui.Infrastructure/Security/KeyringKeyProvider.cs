using System.Security.Cryptography;
using MongoTui.Application.Abstractions.Security;

namespace MongoTui.Infrastructure.Security;

/// <summary>Stores the KEK in the OS keyring, generating it on first use.</summary>
public sealed class KeyringKeyProvider : IKeyProvider
{
    private const string KekKey = "kek";
    private const int KeySize = 32;

    private readonly IKeyring _keyring;

    public KeyringKeyProvider(IKeyring keyring) => _keyring = keyring;

    public bool IsKeyringAvailable => _keyring.IsAvailable;

    public async Task<byte[]> GetOrCreateKekAsync(CancellationToken cancellationToken)
    {
        var existing = await _keyring.GetAsync(KekKey, cancellationToken);
        if (existing is { Length: KeySize })
        {
            return existing;
        }

        var kek = RandomNumberGenerator.GetBytes(KeySize);
        await _keyring.SetAsync(KekKey, kek, cancellationToken);

        return kek;
    }

    public Task<byte[]> UnlockWithMasterPasswordAsync(string password, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The keyring provider does not use a master password.");

    public Task<byte[]> ChangeMasterPasswordAsync(string newPassword, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The keyring provider does not use a master password.");
}
