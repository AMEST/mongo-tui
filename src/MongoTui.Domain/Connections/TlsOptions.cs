namespace MongoTui.Domain.Connections;

/// <summary>TLS/SSL settings for a connection.</summary>
public sealed record TlsOptions
{
    public bool Enabled { get; init; }

    public bool AllowInvalidCertificates { get; init; }

    public string? CaFile { get; init; }

    public string? ClientCertificateFile { get; init; }
}
