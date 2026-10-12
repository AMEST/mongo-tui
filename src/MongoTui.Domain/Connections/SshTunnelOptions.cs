namespace MongoTui.Domain.Connections;

/// <summary>SSH tunnel settings (model only; UI ships in v2).</summary>
public sealed record SshTunnelOptions
{
    public bool Enabled { get; init; }

    public string? Host { get; init; }

    public int Port { get; init; } = 22;

    public string? Username { get; init; }

    public string? PrivateKeyFile { get; init; }
}
