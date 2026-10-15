using System.Collections.Concurrent;
using MongoDB.Bson;
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
    private readonly ConcurrentDictionary<(MongoConnectionTarget, string, string),
        (DateTimeOffset FetchedAt, IReadOnlyList<string> Fields)> _fieldCache = new();

    public AutocompleteService(IMongoGateway gateway) => _gateway = gateway;

    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(
        MongoConnectionTarget target,
        string? database,
        string? collection,
        string prefix,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = database is null
            ? (await _gateway.ListDatabasesAsync(target, cancellationToken)).Select(database => database.Name).ToList()
            : collection is null
                ? await _gateway.ListCollectionsAsync(target, DatabaseName.Create(database), cancellationToken)
                : await GetFieldsAsync(target, database, collection, cancellationToken);

        return candidates
            .Where(candidate => candidate.Length > 0 && candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
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
        var key = (target, database, collection);
        if (_fieldCache.TryGetValue(key, out var cached)
            && DateTimeOffset.UtcNow - cached.FetchedAt < TimeSpan.FromMinutes(1))
        {
            return cached.Fields;
        }

        var collectionRef = new CollectionRef(DatabaseName.Create(database), CollectionName.Create(collection));
        var result = await _gateway.FindAsync(
            target,
            collectionRef,
            "{}",
            new QueryExecutionOptions { Limit = 20 },
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in result.Documents)
            CollectFields(document, string.Empty, names, 0);
        var fields = names.OrderBy(field => field, StringComparer.Ordinal).ToList();

        _fieldCache[key] = (DateTimeOffset.UtcNow, fields);

        return fields;
    }

    private static void CollectFields(BsonDocument document, string prefix, ISet<string> names, int depth)
    {
        if (depth >= 8) return;
        foreach (var element in document)
        {
            var path = prefix + element.Name;
            names.Add(path);
            if (element.Value is BsonDocument nested)
                CollectFields(nested, path + ".", names, depth + 1);
            else if (element.Value is BsonArray array)
                foreach (var item in array.OfType<BsonDocument>())
                    CollectFields(item, path + ".", names, depth + 1);
        }
    }
}
