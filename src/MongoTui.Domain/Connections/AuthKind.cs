namespace MongoTui.Domain.Connections;

/// <summary>Authentication mechanism used by a connection profile.</summary>
public enum AuthKind
{
    None,
    ScramSha1,
    ScramSha256,
    X509,
    Plain,
}
