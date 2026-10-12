using System.Collections.Concurrent;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Services;

/// <summary>Provides context-aware completion candidates with a field cache.</summary>
public sealed class AutocompleteService
{
    private readonly IMongoGateway _gateway;
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _fieldCache = new(StringComparer.Ordinal);

    public AutocompleteService(IMongoGateway gateway) => _gateway = gateway;

    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(
        MongoConnectionTarget target,
        string? database,
        string? collection,
        string prefix,
        CancellationToken cancellationToken)
    {
        var candidates = database is null
            ? (await _gateway.ListDatabasesAsync(target, cancellationToken)).Select(database => database.Name).ToList()
            : collection is null
                ? await _gateway.ListCollectionsAsync(target, DatabaseName.Create(database), cancellationToken)
                : await GetFieldsAsync(target, database, collection, cancellationToken);

        return candidates
            .Where(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Clears the cached collection fields.</summary>
    public void Invalidate() => _fieldCache.Clear();

    private async Task<IReadOnlyList<string>> GetFieldsAsync(
        MongoConnectionTarget target,
        string database,
        string collection,
        CancellationToken cancellationToken)
    {
        var key = $"{target.ConnectionString}|{database}|{collection}";
        if (_fieldCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var collectionRef = new CollectionRef(DatabaseName.Create(database), CollectionName.Create(collection));
        var result = await _gateway.FindAsync(
            target,
            collectionRef,
            "{}",
            new QueryExecutionOptions { Limit = 20 },
            cancellationToken);

        var fields = result.Documents
            .SelectMany(document => document.Names)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();

        _fieldCache[key] = fields;

        return fields;
    }
}
