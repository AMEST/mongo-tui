using Microsoft.Extensions.Hosting;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Services;

namespace MongoTui.Infrastructure.Diagnostics;

public sealed class ServerProfilerMonitor(DiagnosticsService diagnostics, DiagnosticsSettingsService settings,
    SlowQueryLogger logger, TimeProvider clock, IQueryMetricsStore metrics) : BackgroundService
{
    private readonly Guid _processId = Guid.NewGuid();
    private readonly Dictionary<ProfilerLease, DateTimeOffset> _watermarks = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await diagnostics.RefreshKnownProfilerStatesAsync(stoppingToken).ConfigureAwait(false);
                var leases = diagnostics.Leases;
                foreach (var key in _watermarks.Keys.Except(leases).ToArray()) _watermarks.Remove(key);
                foreach (var lease in leases)
                    try { await PollAsync(lease, stoppingToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception) { }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private async Task PollAsync(ProfilerLease lease, CancellationToken cancellationToken)
    {
        if (lease.ExpiresAt <= clock.GetUtcNow()) { await diagnostics.RestoreAsync(lease, cancellationToken).ConfigureAwait(false); return; }
        if (!settings.Current.MirrorServerSlowToApplicationLog || lease.Target.ConnectionId is not { } id) return;
        var since = _watermarks.GetValueOrDefault(lease, clock.GetUtcNow().AddSeconds(-2));
        var entries = await diagnostics.ReadProfilerAsync(lease.Target, lease.Database, since,
            TimeSpan.FromMilliseconds(settings.Current.SlowThresholdMs), 10000, cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries.Where(entry => entry.Timestamp > since))
        {
            var observation = new MongoTui.Domain.Diagnostics.QueryObservation
            {
                ProcessId = _processId, ConnectionId = id, Database = entry.Database, Collection = entry.Collection,
                Operation = entry.Operation, Source = MongoTui.Domain.Diagnostics.QuerySource.ServerProfiler,
                StartedAt = entry.Timestamp, Duration = entry.Duration, Success = entry.ErrorCode is null,
                ReturnedCount = entry.ReturnedCount, DocumentsExamined = entry.DocumentsExamined, KeysExamined = entry.KeysExamined, IsSlow = true,
            };
            logger.Write(observation);
            if (settings.Current.MetricsEnabled) metrics.Record(observation);
        }
        if (entries.Count > 0) _watermarks[lease] = entries.Max(entry => entry.Timestamp);
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        foreach (var lease in diagnostics.Leases.Where(lease => lease.RestoreOnExit))
            try { await diagnostics.RestoreAsync(lease, deadline.Token).ConfigureAwait(false); }
            catch (Exception) { }
    }
}
