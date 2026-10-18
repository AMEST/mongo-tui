using System.Collections.Concurrent;
using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Infrastructure.Mongo.Driver;

/// <summary>Primary MongoDB channel backed by the official driver.</summary>
public sealed class DriverMongoGateway(MongoDatabaseDiscovery databaseDiscovery) : IDriverMongoGateway, IDisposable
{
    private readonly ConcurrentDictionary<string, IMongoClient> _clients = new(StringComparer.Ordinal);

    public void ReleaseClient(MongoConnectionTarget target)
    {
        if (_clients.TryRemove(target.ConnectionString, out var client))
        {
            ClusterRegistry.Instance.UnregisterAndDisposeCluster(client.Cluster);
            client.Dispose();
        }
    }

    public Task<IReadOnlyList<DatabaseInfo>> ListDatabasesAsync(
        MongoConnectionTarget target,
        CancellationToken cancellationToken) =>
        databaseDiscovery.ListAsync(GetClient(target), MongoUrl.Create(target.ConnectionString).DatabaseName, cancellationToken);

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        var db = GetClient(target).GetDatabase(database.Value);
        using var cursor = await db.ListCollectionNamesAsync(cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IndexInfo>> ListIndexesAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken)
    {
        using var cursor = await GetCollection(target, collection).Indexes.ListAsync(cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);

        return documents.Select(MapIndex).ToList();
    }

    public async Task<CollectionStats> GetCollectionStatsAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken)
    {
        var mongoCollection = GetCollection(target, collection);
        var count = await mongoCollection.CountDocumentsAsync(
            FilterDefinition<BsonDocument>.Empty,
            cancellationToken: cancellationToken);
        var indexes = await ListIndexesAsync(target, collection, cancellationToken);

        return new CollectionStats
        {
            Name = collection.Collection.Value,
            DocumentCount = count,
            IndexCount = indexes.Count,
        };
    }

    public async Task<DatabaseStats> GetDatabaseStatsAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        var stats = await GetClient(target)
            .GetDatabase(database.Value)
            .RunCommandAsync<BsonDocument>(new BsonDocument("dbStats", 1), cancellationToken: cancellationToken);

        return new DatabaseStats
        {
            Name = database.Value,
            CollectionCount = GetInt(stats, "collections"),
            ObjectCount = GetLong(stats, "objects"),
            DataSizeBytes = TryGetLong(stats, "dataSize"),
            StorageSizeBytes = TryGetLong(stats, "storageSize"),
            IndexCount = GetInt(stats, "indexes"),
        };
    }

    public Task DropDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        GetClient(target).DropDatabaseAsync(database.Value, cancellationToken);

    public Task DropCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        GetClient(target)
            .GetDatabase(collection.Database.Value)
            .DropCollectionAsync(collection.Collection.Value, cancellationToken);

    public Task CreateCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        GetClient(target).GetDatabase(collection.Database.Value)
            .CreateCollectionAsync(collection.Collection.Value, cancellationToken: cancellationToken);

    public Task ClearCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        GetCollection(target, collection)
            .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken);

    public async Task CreateIndexAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexKeysJson,
        string? name,
        bool unique,
        bool background,
        CancellationToken cancellationToken)
    {
        var keys = new BsonDocumentIndexKeysDefinition<BsonDocument>(BsonDocument.Parse(indexKeysJson));
        var options = new CreateIndexOptions { Name = name, Unique = unique, Background = background };
        var model = new CreateIndexModel<BsonDocument>(keys, options);

        await GetCollection(target, collection).Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }

    public Task DropIndexAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexName,
        CancellationToken cancellationToken) =>
        GetCollection(target, collection).Indexes.DropOneAsync(indexName, cancellationToken);

    public async Task<QueryResult> FindAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string filterJson,
        QueryExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var documents = await GetCollection(target, collection)
            .Find(ParseFilter(filterJson))
            .Skip(options.Skip)
            .Limit(options.Limit)
            .ToListAsync(cancellationToken);
        stopwatch.Stop();

        return new QueryResult
        {
            Kind = QueryResultKind.Documents,
            Documents = documents,
            Elapsed = stopwatch.Elapsed,
        };
    }

    public async Task<QueryResult> AggregateAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string pipelineJson,
        QueryExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var stages = BsonSerializer.Deserialize<BsonArray>(pipelineJson).Select(stage => stage.AsBsonDocument);
        var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(stages);

        var stopwatch = Stopwatch.StartNew();
        using var cursor = await GetCollection(target, collection)
            .AggregateAsync(pipeline, cancellationToken: cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);
        stopwatch.Stop();

        return new QueryResult
        {
            Kind = QueryResultKind.Aggregation,
            Documents = documents,
            Elapsed = stopwatch.Elapsed,
        };
    }

    public Task<long> CountAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string filterJson,
        CancellationToken cancellationToken) =>
        GetCollection(target, collection).CountDocumentsAsync(ParseFilter(filterJson), cancellationToken: cancellationToken);

    public Task InsertOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string documentJson,
        CancellationToken cancellationToken) =>
        GetCollection(target, collection).InsertOneAsync(BsonDocument.Parse(documentJson), cancellationToken: cancellationToken);

    public async Task<long> ReplaceOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        string documentJson,
        CancellationToken cancellationToken)
    {
        var result = await GetCollection(target, collection).ReplaceOneAsync(
            ParseFilter(idFilterJson),
            BsonDocument.Parse(documentJson),
            cancellationToken: cancellationToken);

        return result.MatchedCount;
    }

    public async Task<long> DeleteOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        CancellationToken cancellationToken)
    {
        var result = await GetCollection(target, collection)
            .DeleteOneAsync(ParseFilter(idFilterJson), cancellationToken);

        return result.DeletedCount;
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        _clients.Clear();
    }

    private IMongoClient GetClient(MongoConnectionTarget target) =>
        _clients.GetOrAdd(target.ConnectionString, static connectionString => new MongoClient(connectionString));

    private IMongoCollection<BsonDocument> GetCollection(MongoConnectionTarget target, CollectionRef collection) =>
        GetClient(target)
            .GetDatabase(collection.Database.Value)
            .GetCollection<BsonDocument>(collection.Collection.Value);

    private static BsonDocument ParseFilter(string filterJson) =>
        string.IsNullOrWhiteSpace(filterJson) ? new BsonDocument() : BsonDocument.Parse(filterJson);

    private static int GetInt(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt32() : 0;

    private static long GetLong(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt64() : 0;

    private static long? TryGetLong(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt64() : null;

    private static IndexInfo MapIndex(BsonDocument index)
    {
        var fields = index["key"].AsBsonDocument.Elements
            .Select(element => new IndexField(element.Name, ToDirection(element.Value)))
            .ToList();

        return new IndexInfo
        {
            Name = index["name"].AsString,
            Fields = fields,
            Unique = index.TryGetValue("unique", out var unique) && unique.ToBoolean(),
            Sparse = index.TryGetValue("sparse", out var sparse) && sparse.ToBoolean(),
        };
    }

    private static IndexDirection ToDirection(BsonValue value) => value.BsonType switch
    {
        BsonType.Int32 => value.AsInt32 < 0 ? IndexDirection.Descending : IndexDirection.Ascending,
        BsonType.Int64 => value.AsInt64 < 0 ? IndexDirection.Descending : IndexDirection.Ascending,
        BsonType.Double => value.AsDouble < 0 ? IndexDirection.Descending : IndexDirection.Ascending,
        BsonType.String => ToStringDirection(value.AsString),
        _ => IndexDirection.Ascending,
    };

    private static IndexDirection ToStringDirection(string value) => value switch
    {
        "hashed" => IndexDirection.Hashed,
        "text" => IndexDirection.Text,
        "2d" => IndexDirection.Geo2d,
        "2dsphere" => IndexDirection.Geo2dSphere,
        _ => IndexDirection.Ascending,
    };
}
