namespace MongoTui.Application.Abstractions.Security;

/// <summary>Persists the raw KEK in a local file with restrictive permissions.</summary>
public interface IKekFileStore
{
    string Path { get; }

    Task<byte[]?> TryReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(byte[] kek, CancellationToken cancellationToken);

    Task DeleteAsync(CancellationToken cancellationToken);
}
