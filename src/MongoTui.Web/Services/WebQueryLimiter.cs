namespace MongoTui.Web.Services;

/// <summary>Bounds simultaneous shell/driver executions across browser circuits.</summary>
public sealed class WebQueryLimiter : IDisposable
{
    private readonly SemaphoreSlim _gate = new(2, 2);
    public Task EnterAsync(CancellationToken cancellationToken) => _gate.WaitAsync(cancellationToken);
    public void Exit() => _gate.Release();
    public void Dispose() => _gate.Dispose();
}
