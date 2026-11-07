using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Services;

namespace MongoTui.Infrastructure.Diagnostics;

/// <summary>Keeps the memory-only recorder up to date with preferences from either UI process.</summary>
public sealed class DiagnosticsSettingsMonitor(DiagnosticsSettingsService settings,
    ILogger<DiagnosticsSettingsMonitor> logger) : BackgroundService
{
    private bool _failed;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await settings.ReloadAsync(cancellationToken).ConfigureAwait(false);
            _failed = false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            if (!_failed) logger.LogWarning("Could not refresh diagnostics settings; retaining the previous snapshot");
            _failed = true;
        }
    }
}
