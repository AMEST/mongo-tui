namespace MongoTui.Application.Abstractions.Security;

/// <summary>Supplies the Key Encryption Key (KEK) used to protect connection secrets.</summary>
public interface IKeyProvider
{
    /// <summary>Whether an OS keyring is available to store the KEK.</summary>
    bool IsKeyringAvailable { get; }

    /// <summary>Returns the KEK from the OS keyring, creating it on first use.</summary>
    Task<byte[]> GetOrCreateKekAsync(CancellationToken cancellationToken);

    /// <summary>Derives the KEK from a master password (used when no keyring is available).</summary>
    Task<byte[]> UnlockWithMasterPasswordAsync(string password, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the master password: rotates the salt and returns a new KEK derived from
    /// <paramref name="newPassword"/>. The caller is responsible for re-encrypting secrets.
    /// </summary>
    Task<byte[]> ChangeMasterPasswordAsync(string newPassword, CancellationToken cancellationToken);
}
