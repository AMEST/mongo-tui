using System.Globalization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

public sealed class ConnectionTunnelService : IAsyncDisposable
{
    private readonly ISshTunnelFactory _factory;
    private readonly ITwoFactorPrompt _prompt;
    private readonly IMongoGateway _gateway;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HashSet<ISshTunnelSession> _sessions = [];
    private readonly HashSet<Task<TunnelConnection>> _pending = [];
    private readonly object _sync = new();
    private int _disposed;

    public ConnectionTunnelService(ISshTunnelFactory factory, ITwoFactorPrompt prompt, IMongoGateway gateway)
    {
        _factory = factory;
        _prompt = prompt;
        _gateway = gateway;
    }

    public Task<TunnelConnection> OpenAsync(
        ConnectionProfile profile,
        MongoConnectionTarget target,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (!profile.Ssh.Enabled)
            {
                return Task.FromResult(new TunnelConnection(target, null));
            }

            var task = OpenCoreAsync(profile, target, cancellationToken);
            _pending.Add(task);
            return ObserveAsync(task);
        }
    }

    private async Task<TunnelConnection> OpenCoreAsync(
        ConnectionProfile profile,
        MongoConnectionTarget target,
        CancellationToken cancellationToken)
    {
        profile.Ssh.Validate();
        if (!target.ConnectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase)
            && !target.ConnectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("SSH SOCKS proxy requires a mongodb:// or mongodb+srv:// URI.");
        }

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        var tunnel = await _factory.StartAsync(profile.Ssh, _prompt, lifetime.Token).ConfigureAwait(false);
        try
        {
            lifetime.Token.ThrowIfCancellationRequested();
            var targetWithProxy = new MongoConnectionTarget(WithProxy(target.ConnectionString, tunnel.LocalPort));
            var tracked = new TrackedSshTunnelSession(tunnel, session => Release(session, targetWithProxy));
            lock (_sync) { _sessions.Add(tracked); }
            return new TunnelConnection(targetWithProxy, tracked);
        }
        catch
        {
            await tunnel.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Cancels pending authentication and closes every active SSH process on application exit.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _shutdown.CancelAsync().ConfigureAwait(false);
        Task<TunnelConnection>[] pending;
        lock (_sync) { pending = _pending.ToArray(); }
        foreach (var task in pending)
        {
            try { await task.ConfigureAwait(false); }
            catch (Exception) when (task.IsCanceled || task.IsFaulted) { }
        }

        ISshTunnelSession[] sessions;
        lock (_sync) { sessions = _sessions.ToArray(); }
        try
        {
            await Task.WhenAll(sessions.Select(session => session.DisposeAsync().AsTask())).ConfigureAwait(false);
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    private async Task<TunnelConnection> ObserveAsync(Task<TunnelConnection> task)
    {
        try { return await task.ConfigureAwait(false); }
        finally { lock (_sync) { _pending.Remove(task); } }
    }

    private void Release(ISshTunnelSession session, MongoConnectionTarget target)
    {
        try { _gateway.ReleaseClient(target); }
        finally { lock (_sync) { _sessions.Remove(session); } }
    }

    /// <summary>Replaces proxy options without changing hosts, credentials, or other URI options.</summary>
    public static string WithProxy(string connectionString, int localPort)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(localPort, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(localPort, 65535);
        var queryStart = connectionString.IndexOf('?');
        var address = queryStart < 0 ? connectionString : connectionString[..queryStart];
        var query = queryStart < 0 ? string.Empty : connectionString[(queryStart + 1)..];
        var parameters = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(parameter => !IsProxyParameter(parameter)).ToList();
        parameters.Add("proxyHost=127.0.0.1");
        parameters.Add("proxyPort=" + localPort.ToString(CultureInfo.InvariantCulture));
        var authorityStart = address.IndexOf("://", StringComparison.Ordinal) + 3;
        if (!address[authorityStart..].Contains('/'))
        {
            address += '/';
        }

        return address + '?' + string.Join('&', parameters);
    }

    private static bool IsProxyParameter(string parameter)
    {
        var name = Uri.UnescapeDataString(parameter.Split('=', 2)[0]);
        return name.Equals("proxyHost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("proxyPort", StringComparison.OrdinalIgnoreCase)
            || name.Equals("proxyUsername", StringComparison.OrdinalIgnoreCase)
            || name.Equals("proxyPassword", StringComparison.OrdinalIgnoreCase);
    }
}
