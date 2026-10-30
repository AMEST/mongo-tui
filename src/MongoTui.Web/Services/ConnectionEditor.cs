using MongoTui.Application.Services;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Web.Services;

public sealed class ConnectionEditor(ConnectionService connections)
{
    public async Task SaveAsync(ConnectionDraft draft, CancellationToken cancellationToken)
    {
        var (profile, secret) = await connections.PrepareAsync(draft.Snapshot(), cancellationToken).ConfigureAwait(false);
        await connections.SaveAsync(profile, secret, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tests the current draft without saving it, with a three-second deadline; the profile name is optional.</summary>
    public async Task TestAsync(ConnectionDraft draft, CancellationToken cancellationToken)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancelAfter(TimeSpan.FromSeconds(ConnectionService.TestTimeoutSeconds));
        try
        {
            var name = string.IsNullOrWhiteSpace(draft.Name) ? "Connection test" : draft.Name;
            var (profile, secret) = await connections.PrepareAsync(draft.Snapshot() with { Name = name }, operation.Token).ConfigureAwait(false);
            await connections.TestConnectionAsync(profile, secret, operation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && operation.IsCancellationRequested)
        {
            throw new TimeoutException($"Connection test timed out after {ConnectionService.TestTimeoutSeconds} seconds.", exception);
        }
    }

}

public sealed class ConnectionDraft
{
    public ConnectionId? Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = "mongodb://localhost:27017";
    public string Password { get; set; } = string.Empty;
    public SshTunnelOptions Ssh { get; set; } = new();

    public ConnectionProfileDraft Snapshot() => new()
    {
        Id = Id, Name = Name, Uri = Uri, Password = Password, Ssh = Ssh,
    };
}
