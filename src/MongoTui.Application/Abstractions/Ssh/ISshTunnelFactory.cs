using MongoTui.Domain.Connections;

namespace MongoTui.Application.Abstractions.Ssh;

public interface ISshTunnelFactory
{
    /// <summary>Authenticates and waits for SOCKS5 readiness; releases all resources on failure.</summary>
    Task<ISshTunnelSession> StartAsync(
        SshTunnelOptions options,
        ITwoFactorPrompt prompt,
        CancellationToken cancellationToken);
}
