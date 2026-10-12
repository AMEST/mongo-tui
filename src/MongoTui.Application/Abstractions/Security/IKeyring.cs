namespace MongoTui.Application.Abstractions.Security;

/// <summary>OS keyring / secret storage abstraction.</summary>
public interface IKeyring
{
    bool IsAvailable { get; }

    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, byte[] value, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
