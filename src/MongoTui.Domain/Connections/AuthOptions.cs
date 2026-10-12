namespace MongoTui.Domain.Connections;

/// <summary>Authentication settings without the secret itself.</summary>
public sealed record AuthOptions
{
    public AuthKind Kind { get; init; } = AuthKind.None;

    public string? Username { get; init; }

    public string? AuthDatabase { get; init; }
}
