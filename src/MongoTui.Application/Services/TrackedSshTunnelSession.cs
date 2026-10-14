using MongoTui.Application.Abstractions.Ssh;

namespace MongoTui.Application.Services;

internal sealed class TrackedSshTunnelSession : ISshTunnelSession
{
    private readonly ISshTunnelSession _session;
    private readonly Action<ISshTunnelSession> _release;
    private readonly object _sync = new();
    private Task? _disposal;

    public TrackedSshTunnelSession(ISshTunnelSession session, Action<ISshTunnelSession> release)
    {
        _session = session;
        _release = release;
    }

    public int LocalPort => _session.LocalPort;

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            return new ValueTask(_disposal ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _release(this);
        }
    }
}
