using Microsoft.Extensions.Hosting;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Services;

namespace MongoTui.Infrastructure.Diagnostics;

public sealed class QueryMetricsFlushService(QueryMetricsQueue queue, IQueryMetricsRepository repository,
    DiagnosticsSettingsService settings, TimeProvider clock) : BackgroundService
{
    private DateTimeOffset _lastPrune;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            do { await FlushAsync(stoppingToken).ConfigureAwait(false); }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        await FlushAsync(deadline.Token).ConfigureAwait(false);
    }

    internal async Task FlushAsync(CancellationToken cancellationToken)
    {
        var batch = queue.Drain();
        try
        {
            if (batch.Count > 0 && settings.Current.PersistMetrics)
                await repository.AddBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            var now = clock.GetUtcNow();
            if (now - _lastPrune < TimeSpan.FromHours(1)) return;
            await repository.PruneAsync(now.AddDays(-settings.Current.PersistentRetentionDays),
                settings.Current.MaximumPersistentRows, cancellationToken).ConfigureAwait(false);
            _lastPrune = now;
        }
        catch (Exception) { queue.ReportLost(batch.Count); }
    }
}
