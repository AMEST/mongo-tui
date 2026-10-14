using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Abstractions.Mongo;

/// <summary>Read/write access to a MongoDB deployment.</summary>
public interface IMongoGateway
{
    /// <summary>Releases the cached client after all operations using this target have stopped.</summary>
    void ReleaseClient(MongoConnectionTarget target);

    Task<IReadOnlyList<DatabaseInfo>> ListDatabasesAsync(
        MongoConnectionTarget target,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListCollectionsAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IndexInfo>> ListIndexesAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken);

    Task<CollectionStats> GetCollectionStatsAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken);

    Task<DatabaseStats> GetDatabaseStatsAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken);

    Task DropDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken);

    Task DropCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken);

    Task ClearCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken);

    Task CreateIndexAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexKeysJson,
        string? name,
        bool unique,
        CancellationToken cancellationToken);

    Task DropIndexAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexName,
        CancellationToken cancellationToken);

    Task<QueryResult> FindAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string filterJson,
        QueryExecutionOptions options,
        CancellationToken cancellationToken);

    Task<QueryResult> AggregateAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string pipelineJson,
        QueryExecutionOptions options,
        CancellationToken cancellationToken);

    Task<long> CountAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string filterJson,
        CancellationToken cancellationToken);

    Task InsertOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string documentJson,
        CancellationToken cancellationToken);

    Task<long> ReplaceOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        string documentJson,
        CancellationToken cancellationToken);

    Task<long> DeleteOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        CancellationToken cancellationToken);
}
