using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Diagnostics;

namespace MongoTui.Infrastructure.Diagnostics;

/// <summary>Loads preferences before UI startup and observes changes made by another process.</summary>
public sealed class LoggingSettingsMonitor(ILoggingSettings settings, ILogger<LoggingSettingsMonitor> logger) : BackgroundService
{
    private const int RefreshIntervalSeconds = 3;
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(RefreshIntervalSeconds));
        var failed = false;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await settings.GetAsync(stoppingToken).ConfigureAwait(false);
                    failed = false;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    if (!failed) logger.LogWarning("Could not refresh logging settings: {Failure}", ExceptionDiagnostics.Describe(exception));
                    failed = true;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
