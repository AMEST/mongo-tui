using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Connections;

/// <summary>A saved connection profile.</summary>
public sealed record ConnectionProfile
{
    public required ConnectionId Id { get; init; }

    public required string Name { get; init; }

    public required string Uri { get; init; }

    public AuthOptions Auth { get; init; } = new();

    public TlsOptions Tls { get; init; } = new();

    public SshTunnelOptions Ssh { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public static ConnectionProfile Create(
        ConnectionId id,
        string name,
        string uri,
        AuthOptions? auth = null,
        TlsOptions? tls = null,
        SshTunnelOptions? ssh = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Connection name must not be empty.");
        }

        if (!IsMongoUri(uri))
        {
            throw new DomainException("Connection string must use the 'mongodb' or 'mongodb+srv' scheme.");
        }

        var now = DateTimeOffset.UtcNow;

        return new ConnectionProfile
        {
            Id = id,
            Name = name.Trim(),
            Uri = uri,
            Auth = auth ?? new AuthOptions(),
            Tls = tls ?? new TlsOptions(),
            Ssh = ssh ?? new SshTunnelOptions(),
            CreatedAt = createdAt ?? now,
            UpdatedAt = updatedAt ?? now,
        };
    }

    private static bool IsMongoUri(string uri) =>
        System.Uri.TryCreate(uri, System.UriKind.Absolute, out var parsed)
        && (parsed.Scheme == "mongodb" || parsed.Scheme == "mongodb+srv");
}
