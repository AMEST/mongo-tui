using MongoTui.Application.Services;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Web.Services;

public sealed class ConnectionEditor(ConnectionService connections)
{
    private const int TestTimeoutSeconds = 3;

    public async Task SaveAsync(ConnectionDraft draft, CancellationToken cancellationToken)
    {
        var (profile, secret) = await PrepareAsync(draft, draft.Name, cancellationToken);
        await connections.SaveAsync(profile, secret, cancellationToken);
    }

    /// <summary>Tests the current draft without saving it, with a three-second deadline; the profile name is optional.</summary>
    public async Task TestAsync(ConnectionDraft draft, CancellationToken cancellationToken)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancelAfter(TimeSpan.FromSeconds(TestTimeoutSeconds));
        try
        {
            var name = string.IsNullOrWhiteSpace(draft.Name) ? "Connection test" : draft.Name;
            var (profile, secret) = await PrepareAsync(draft, name, operation.Token);
            await connections.TestConnectionAsync(profile, secret, operation.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && operation.IsCancellationRequested)
        {
            throw new TimeoutException($"Connection test timed out after {TestTimeoutSeconds} seconds.", exception);
        }
    }

    private async Task<(ConnectionProfile Profile, string? Secret)> PrepareAsync(
        ConnectionDraft draft, string name, CancellationToken cancellationToken)
    {
        var parsed = ConnectionUriParser.Parse(draft.Uri);
        var original = draft.Id is null ? null : await connections.GetProfileAsync(draft.Id, cancellationToken);
        var auth = parsed.Username is not null
            ? new AuthOptions { Username = parsed.Username, AuthDatabase = parsed.AuthDatabase, Kind = parsed.AuthKind }
            : original?.Auth ?? new AuthOptions();
        var profile = ConnectionProfile.Create(draft.Id ?? ConnectionId.New(), name, parsed.Uri,
            auth, original?.Tls, draft.Ssh, original?.CreatedAt);
        var secret = parsed.Password ?? (string.IsNullOrEmpty(draft.Password) && original is not null
            ? await connections.GetSecretAsync(original.Id, cancellationToken) : draft.Password);
        return (profile, secret);
    }
}

public sealed class ConnectionDraft
{
    public ConnectionId? Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = "mongodb://localhost:27017";
    public string Password { get; set; } = string.Empty;
    public SshTunnelOptions Ssh { get; set; } = new();
}
