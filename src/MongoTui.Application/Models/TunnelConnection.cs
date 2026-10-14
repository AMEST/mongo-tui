using MongoTui.Application.Abstractions.Ssh;

namespace MongoTui.Application.Models;

/// <summary>A prepared connection whose SSH session must live for the duration of MongoDB use.</summary>
public sealed record TunnelConnection(MongoConnectionTarget Target, ISshTunnelSession? Tunnel) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Tunnel?.DisposeAsync() ?? ValueTask.CompletedTask;
}
