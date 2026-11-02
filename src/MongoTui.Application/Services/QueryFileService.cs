using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

/// <summary>Reads and writes JavaScript query files and tracks the most recently used ones.</summary>
public sealed class QueryFileService
{
    public const int MaximumRecentFiles = 10;
    public const string DefaultExtension = ".js";
    private const string RecentKey = "query.recentFiles";

    private readonly IQueryFileStore _store;
    private readonly ISettingsStore _settings;
    private readonly ILogger<QueryFileService> _logger;

    public QueryFileService(IQueryFileStore store, ISettingsStore settings, ILogger<QueryFileService> logger)
    {
        _store = store;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Reads a query file and remembers it as recently used.</summary>
    public async Task<QueryFile> OpenAsync(string path, CancellationToken cancellationToken)
    {
        var normalized = ValidatePath(path);
        var script = await _store.ReadAsync(normalized, cancellationToken).ConfigureAwait(false);
        await AddRecentAsync(normalized, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Query file opened");
        return new QueryFile(normalized, script);
    }

    /// <summary>Writes a query file (adding the default extension when missing) and remembers it.</summary>
    public async Task<QueryFile> SaveAsync(string path, string script, CancellationToken cancellationToken)
    {
        var normalized = NormalizeSavePath(path);
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new DomainException("Query script must not be empty.");
        }

        await _store.WriteAsync(normalized, script, cancellationToken).ConfigureAwait(false);
        await AddRecentAsync(normalized, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Query file saved");
        return new QueryFile(normalized, script);
    }

    /// <summary>Returns the most recently opened or saved files, newest first.</summary>
    public async Task<IReadOnlyList<string>> GetRecentAsync(CancellationToken cancellationToken)
    {
        var value = await _settings.GetAsync(RecentKey, cancellationToken).ConfigureAwait(false);
        return Parse(value);
    }

    /// <summary>Moves a path to the front of the recent list, keeping at most ten entries.</summary>
    public async Task AddRecentAsync(string path, CancellationToken cancellationToken)
    {
        var value = await _settings.GetAsync(RecentKey, cancellationToken).ConfigureAwait(false);
        var list = Parse(value).Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase)).ToList();
        list.Insert(0, path);
        if (list.Count > MaximumRecentFiles)
        {
            list.RemoveRange(MaximumRecentFiles, list.Count - MaximumRecentFiles);
        }

        await _settings.SetAsync(RecentKey, string.Join('\n', list), cancellationToken).ConfigureAwait(false);
    }

    public Task ClearRecentAsync(CancellationToken cancellationToken) =>
        _settings.SetAsync(RecentKey, string.Empty, cancellationToken);

    private static string ValidatePath(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException("Query file path is required.");
        }

        return trimmed;
    }

    private static string NormalizeSavePath(string path)
    {
        var trimmed = ValidatePath(path);
        return Path.HasExtension(trimmed) ? trimmed : trimmed + DefaultExtension;
    }

    private static List<string> Parse(string? value) => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
