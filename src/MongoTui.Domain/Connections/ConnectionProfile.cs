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

        ssh?.Validate();

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

    private static bool IsMongoUri(string uri)
    {
        var schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0 || !(uri[..schemeEnd].Equals("mongodb", StringComparison.OrdinalIgnoreCase)
            || uri[..schemeEnd].Equals("mongodb+srv", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var remainder = uri[(schemeEnd + 3)..];
        var authorityEnd = remainder.IndexOfAny(['/', '?', '#']);
        var authority = authorityEnd < 0 ? remainder : remainder[..authorityEnd];
        var credentialsEnd = authority.LastIndexOf('@') + 1;
        var credentials = authority[..credentialsEnd];
        return authority[credentialsEnd..].Split(',').All(host =>
            host.Length > 0 && System.Uri.TryCreate(uri[..(schemeEnd + 3)] + credentials + host,
                System.UriKind.Absolute, out var parsed) && parsed.Host.Length > 0);
    }
}
