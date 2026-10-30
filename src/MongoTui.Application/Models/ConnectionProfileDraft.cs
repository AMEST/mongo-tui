using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Models;

/// <summary>A snapshot of connection fields; omitted settings retain their saved values.</summary>
public sealed record ConnectionProfileDraft
{
    public ConnectionId? Id { get; init; }
    public required string Name { get; init; }
    public required string Uri { get; init; }
    public AuthOptions? Auth { get; init; }
    public string? Password { get; init; }
    public TlsOptions? Tls { get; init; }
    public SshTunnelOptions? Ssh { get; init; }

    public override string ToString() => nameof(ConnectionProfileDraft);
}
