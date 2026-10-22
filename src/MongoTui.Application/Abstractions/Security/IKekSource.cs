using MongoTui.Application.Models;

namespace MongoTui.Application.Abstractions.Security;

/// <summary>Supplies and manages the Key Encryption Key (KEK) for one security mode.</summary>
public interface IKekSource
{
    SecurityMode Mode { get; }

    bool IsAvailable { get; }

    Task<bool> HasMaterialAsync(CancellationToken cancellationToken);

    Task<byte[]?> TryGetKekAsync(CancellationToken cancellationToken);

    Task<byte[]> UnlockAsync(string? password, CancellationToken cancellationToken);

    Task<byte[]> CreateAsync(string? password, CancellationToken cancellationToken);

    Task DeleteAsync(CancellationToken cancellationToken);
}
