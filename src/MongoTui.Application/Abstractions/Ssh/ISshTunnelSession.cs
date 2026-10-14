namespace MongoTui.Application.Abstractions.Ssh;

/// <summary>Owns a running SOCKS5 proxy bound to IPv4 loopback.</summary>
public interface ISshTunnelSession : IAsyncDisposable
{
    int LocalPort { get; }
}
