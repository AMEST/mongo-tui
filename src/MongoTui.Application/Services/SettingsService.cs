using MongoTui.Application.Abstractions.Persistence;

namespace MongoTui.Application.Services;

/// <summary>Typed access to user-facing UI settings persisted in the settings store.</summary>
public sealed class SettingsService
{
    private const string LanguageKey = "ui.language";
    private const string DisplayModeKey = "ui.displayMode";

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
}
