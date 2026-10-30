using System.Globalization;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;

namespace MongoTui.Application.Services;

/// <summary>Typed access to user-facing UI settings persisted in the settings store.</summary>
public sealed class SettingsService
{
    public const int DefaultQueryTimeoutSeconds = 30;
    public const int MaximumQueryTimeoutSeconds = 86_400; // 24 hours
    private const string LanguageKey = "ui.language";
    private const string DisplayModeKey = "ui.displayMode";
    private const string QueryTimeoutKey = "query.timeoutSeconds";
    private const string LogLevelKey = "logging.level";

    private readonly ISettingsStore _store;

    public SettingsService(ISettingsStore store) => _store = store;

    /// <summary>Gets the saved UI language code (e.g. <c>en</c>, <c>ru</c>), or <see langword="null"/>.</summary>
    public Task<string?> GetLanguageAsync(CancellationToken cancellationToken) =>
        _store.GetAsync(LanguageKey, cancellationToken);

    /// <summary>Persists the UI language code.</summary>
    public Task SetLanguageAsync(string language, CancellationToken cancellationToken) =>
        _store.SetAsync(LanguageKey, language, cancellationToken);

    /// <summary>Gets the saved result display mode name, or <see langword="null"/>.</summary>
    public Task<string?> GetDisplayModeAsync(CancellationToken cancellationToken) =>
        _store.GetAsync(DisplayModeKey, cancellationToken);

    /// <summary>Persists the result display mode name.</summary>
    public Task SetDisplayModeAsync(string mode, CancellationToken cancellationToken) =>
        _store.SetAsync(DisplayModeKey, mode, cancellationToken);

    /// <summary>Gets the shared query deadline in seconds. Zero disables it.</summary>
    public async Task<int> GetQueryTimeoutSecondsAsync(CancellationToken cancellationToken)
    {
        var value = await _store.GetAsync(QueryTimeoutKey, cancellationToken).ConfigureAwait(false);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds is >= 0 and <= MaximumQueryTimeoutSeconds ? seconds : DefaultQueryTimeoutSeconds;
    }

    /// <summary>Changes the deadline for subsequent queries in both interfaces.</summary>
    public Task SetQueryTimeoutSecondsAsync(int seconds, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(seconds, MaximumQueryTimeoutSeconds);
        return _store.SetAsync(QueryTimeoutKey, seconds.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    public async Task<ApplicationLogLevel> GetLogLevelAsync(CancellationToken cancellationToken)
    {
        var value = await _store.GetAsync(LogLevelKey, cancellationToken).ConfigureAwait(false);
        return Enum.TryParse<ApplicationLogLevel>(value, ignoreCase: true, out var level) && Enum.IsDefined(level)
            ? level : ApplicationLogLevel.Information;
    }

    public Task SetLogLevelAsync(ApplicationLogLevel level, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level));
        return _store.SetAsync(LogLevelKey, level.ToString(), cancellationToken);
    }
}
