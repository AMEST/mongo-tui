using MongoTui.Domain.Connections;

namespace MongoTui.Application.Models;

/// <summary>A connection string split into its stored parts and credentials.</summary>
public sealed record ParsedConnectionUri
{
    /// <summary>The connection string without userinfo (credentials removed).</summary>
    public required string Uri { get; init; }

    public required string Host { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? AuthDatabase { get; init; }

    public AuthKind AuthKind { get; init; }
}
