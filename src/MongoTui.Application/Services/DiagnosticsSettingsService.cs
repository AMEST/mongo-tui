using System.Globalization;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Services;

/// <summary>Loads a complete settings snapshot; query recording never performs I/O.</summary>
public sealed partial class DiagnosticsSettingsService(ISettingsStore store)
{
    public const string MetricsEnabledKey = "diagnostics.metrics.enabled";
    public const string SlowModeKey = "diagnostics.slow.mode";
    public const string SlowThresholdKey = "diagnostics.slow.thresholdMs";
    public const string MemoryRetentionKey = "diagnostics.metrics.memoryRetentionMinutes";
    private ClientDiagnosticsSettings _current = new();

    public ClientDiagnosticsSettings Current => Volatile.Read(ref _current);

    public async Task<ClientDiagnosticsSettings> ReloadAsync(CancellationToken cancellationToken)
    {
        var values = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var settings = new ClientDiagnosticsSettings
        {
            PersistMetrics = bool.TryParse(values.GetValueOrDefault("diagnostics.metrics.persist"), out var persist) && persist,
            MirrorServerSlowToApplicationLog = bool.TryParse(values.GetValueOrDefault("diagnostics.slow.mirrorServerLog"), out var mirror) && mirror,
            PersistentRetentionDays = ReadInt(values, "diagnostics.metrics.persistentRetentionDays", 1, 365, 7),
            MaximumPersistentRows = ReadInt(values, "diagnostics.metrics.maximumPersistentRows", 1000, 1000000, 50000),
            MetricsEnabled = !bool.TryParse(values.GetValueOrDefault(MetricsEnabledKey), out var enabled) || enabled,
            LogSlowQueries = values.GetValueOrDefault(SlowModeKey) == nameof(SlowQueryMode.Client),
            SlowThresholdMs = ReadInt(values, SlowThresholdKey, 0, 600_000, 100),
            MemoryRetentionMinutes = ReadInt(values, MemoryRetentionKey, 1, 1440, 60),
        };
        Volatile.Write(ref _current, settings);
        return settings;
    }

    public async Task SaveAsync(ClientDiagnosticsSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.SlowThresholdMs);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.SlowThresholdMs, 600_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MemoryRetentionMinutes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.MemoryRetentionMinutes, 1440);
        ValidateRetention(settings);
        await SaveRetentionAsync(settings, cancellationToken).ConfigureAwait(false);
        await store.SetAsync(MetricsEnabledKey, settings.MetricsEnabled.ToString(), cancellationToken).ConfigureAwait(false);
        await store.SetAsync(SlowModeKey, settings.LogSlowQueries ? nameof(SlowQueryMode.Client) : nameof(SlowQueryMode.Off),
            cancellationToken).ConfigureAwait(false);
        await store.SetAsync(SlowThresholdKey, settings.SlowThresholdMs.ToString(CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);
        await store.SetAsync(MemoryRetentionKey, settings.MemoryRetentionMinutes.ToString(CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _current, settings);
    }

    private static void ValidateRetention(ClientDiagnosticsSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.PersistentRetentionDays, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.PersistentRetentionDays, 365);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaximumPersistentRows, 1000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.MaximumPersistentRows, 1000000);
    }

    private async Task SaveRetentionAsync(ClientDiagnosticsSettings settings, CancellationToken cancellationToken)
    {
        await store.SetAsync("diagnostics.metrics.persist", settings.PersistMetrics.ToString(), cancellationToken).ConfigureAwait(false);
        await store.SetAsync("diagnostics.slow.mirrorServerLog", settings.MirrorServerSlowToApplicationLog.ToString(), cancellationToken).ConfigureAwait(false);
        await store.SetAsync("diagnostics.metrics.persistentRetentionDays", settings.PersistentRetentionDays.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await store.SetAsync("diagnostics.metrics.maximumPersistentRows", settings.MaximumPersistentRows.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int min, int max, int fallback) =>
        int.TryParse(values.GetValueOrDefault(key), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
        && value >= min && value <= max ? value : fallback;
}
