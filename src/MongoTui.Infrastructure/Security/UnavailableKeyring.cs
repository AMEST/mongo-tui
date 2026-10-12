using MongoTui.Application.Abstractions.Security;

namespace MongoTui.Infrastructure.Security;

/// <summary>Keyring placeholder used when no OS keyring integration is available.</summary>
public sealed class UnavailableKeyring : IKeyring
{
    public bool IsAvailable => false;

    public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<byte[]?>(null);

    public Task SetAsync(string key, byte[] value, CancellationToken cancellationToken) =>
        throw new NotSupportedException("No OS keyring is available.");

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
