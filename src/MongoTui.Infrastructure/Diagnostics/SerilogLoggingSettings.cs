using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;

namespace MongoTui.Infrastructure.Diagnostics;

/// <summary>Serializes preference reads/writes and changes the live switches only after persistence succeeds.</summary>
public sealed class SerilogLoggingSettings(SettingsService settings, SerilogLevelControl levels) : ILoggingSettings, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<LoggingSettings> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return levels.Apply(await settings.GetLogLevelAsync(cancellationToken).ConfigureAwait(false)); }
        finally { _gate.Release(); }
    }

    public async Task<LoggingSettings> SaveAsync(ApplicationLogLevel level, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await settings.SetLogLevelAsync(level, cancellationToken).ConfigureAwait(false);
            return levels.Apply(level);
        }
        finally { _gate.Release(); }
    }

    public void Dispose() => _gate.Dispose();
}
