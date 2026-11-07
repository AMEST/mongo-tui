using System.Text.Json;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Services;

public sealed partial class DiagnosticsSettingsService
{
    public const string ProfilerLastKeyPrefix = "diagnostics.profiler.last.";

    public async Task<ProfilerPreferences?> GetProfilerPreferencesAsync(ConnectionId? connectionId, CancellationToken cancellationToken)
    {
        if (connectionId is null) return null;
        var json = await store.GetAsync(ProfilerLastKeyPrefix + connectionId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var preferences = JsonSerializer.Deserialize<ProfilerPreferences>(json);
            if (preferences is null) return null;
            ValidateProfilerPreferences(preferences);
            return preferences;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or DomainException) { return null; }
    }

    public Task SaveProfilerPreferencesAsync(ConnectionId? connectionId, ProfilerPreferences preferences, CancellationToken cancellationToken)
    {
        ValidateProfilerPreferences(preferences);
        return connectionId is null ? Task.CompletedTask : store.SetAsync(ProfilerLastKeyPrefix + connectionId,
            JsonSerializer.Serialize(preferences), cancellationToken);
    }

    public async Task SaveProfilerFormAsync(ConnectionId? connectionId, ProfilerPreferences preferences, bool mirror, CancellationToken cancellationToken)
    {
        ValidateProfilerPreferences(preferences);
        await SaveAsync(Current with { LogSlowQueries = preferences.Mode == SlowQueryMode.Client,
            SlowThresholdMs = preferences.SlowMs, MirrorServerSlowToApplicationLog = mirror }, cancellationToken).ConfigureAwait(false);
        await SaveProfilerPreferencesAsync(connectionId, preferences, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateProfilerPreferences(ProfilerPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!Enum.IsDefined(preferences.Mode) || preferences.Databases is null || preferences.Databases.Count is < 1 or > 1000)
            throw new ArgumentException("Invalid profiler preferences.", nameof(preferences));
        foreach (var database in preferences.Databases) DatabaseName.Create(database);
        ArgumentOutOfRangeException.ThrowIfNegative(preferences.SlowMs);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(preferences.SlowMs, 600000);
        if (!double.IsFinite(preferences.SampleRate) || preferences.SampleRate is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(preferences));
        if (preferences.AutoDisableMinutes is < 0 or > 1440) throw new ArgumentOutOfRangeException(nameof(preferences));
    }
}
