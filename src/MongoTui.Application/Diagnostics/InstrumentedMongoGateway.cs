using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Primitives;

using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Diagnostics;

/// <summary>Records document operations; metadata discovery stays outside user metrics.</summary>
public sealed class InstrumentedMongoGateway(IMongoGateway inner, IQueryMetricsRecorder recorder) : IMongoGateway
{
    public void ReleaseClient(MongoConnectionTarget target) => inner.ReleaseClient(target);

    public Task<IReadOnlyList<DatabaseInfo>> ListDatabasesAsync(MongoConnectionTarget target, CancellationToken cancellationToken) =>
        inner.ListDatabasesAsync(target, cancellationToken);

    public Task<IReadOnlyList<string>> ListCollectionsAsync(MongoConnectionTarget target, DatabaseName database, CancellationToken cancellationToken) =>
        inner.ListCollectionsAsync(target, database, cancellationToken);

    public Task<IReadOnlyList<IndexInfo>> ListIndexesAsync(MongoConnectionTarget target, CollectionRef collection, CancellationToken cancellationToken) =>
        inner.ListIndexesAsync(target, collection, cancellationToken);

    public Task<CollectionStats> GetCollectionStatsAsync(MongoConnectionTarget target, CollectionRef collection, CancellationToken cancellationToken) =>
        inner.GetCollectionStatsAsync(target, collection, cancellationToken);

    public Task<DatabaseStats> GetDatabaseStatsAsync(MongoConnectionTarget target, DatabaseName database, CancellationToken cancellationToken) =>
        inner.GetDatabaseStatsAsync(target, database, cancellationToken);

    public Task DropDatabaseAsync(MongoConnectionTarget target, DatabaseName database, CancellationToken cancellationToken) =>
        inner.DropDatabaseAsync(target, database, cancellationToken);

    public Task DropCollectionAsync(MongoConnectionTarget target, CollectionRef collection, CancellationToken cancellationToken) =>
        inner.DropCollectionAsync(target, collection, cancellationToken);

    public Task CreateCollectionAsync(MongoConnectionTarget target, CollectionRef collection, CancellationToken cancellationToken) =>
        inner.CreateCollectionAsync(target, collection, cancellationToken);

    public Task ClearCollectionAsync(MongoConnectionTarget target, CollectionRef collection, CancellationToken cancellationToken) =>
        recorder.ObserveAsync(target.ConnectionId, collection.Database.Value, collection.Collection.Value,
            QueryOperationKind.Delete, QuerySource.Driver, QueryPurpose.User,
            async () => { await inner.ClearCollectionAsync(target, collection, cancellationToken).ConfigureAwait(false); return true; });

    public Task CreateIndexAsync(MongoConnectionTarget target, CollectionRef collection, string indexKeysJson, string? name, bool unique, bool background, CancellationToken cancellationToken) =>
        inner.CreateIndexAsync(target, collection, indexKeysJson, name, unique, background, cancellationToken);

    public Task DropIndexAsync(MongoConnectionTarget target, CollectionRef collection, string indexName, CancellationToken cancellationToken) =>
        inner.DropIndexAsync(target, collection, indexName, cancellationToken);

    public Task<QueryResult> FindAsync(MongoConnectionTarget target, CollectionRef collection, string filterJson, QueryExecutionOptions options, CancellationToken cancellationToken) =>
        recorder.ObserveAsync(target.ConnectionId, collection.Database.Value, collection.Collection.Value,
            QueryOperationKind.Find, QuerySource.Driver, options.Purpose,
            () => inner.FindAsync(target, collection, filterJson, options, cancellationToken), result => (long?)result.Documents.Count);

    public Task<QueryResult> AggregateAsync(MongoConnectionTarget target, CollectionRef collection, string pipelineJson, QueryExecutionOptions options, CancellationToken cancellationToken) =>
        recorder.ObserveAsync(target.ConnectionId, collection.Database.Value, collection.Collection.Value,
            QueryOperationKind.Aggregate, QuerySource.Driver, options.Purpose,
            () => inner.AggregateAsync(target, collection, pipelineJson, options, cancellationToken), result => (long?)result.Documents.Count);

    public Task<long> CountAsync(MongoConnectionTarget target, CollectionRef collection, string filterJson, CancellationToken cancellationToken) =>
        recorder.ObserveAsync(target.ConnectionId, collection.Database.Value, collection.Collection.Value,
            QueryOperationKind.Count, QuerySource.Driver, QueryPurpose.User,
            () => inner.CountAsync(target, collection, filterJson, cancellationToken));

    public Task InsertOneAsync(MongoConnectionTarget target, CollectionRef collection, string documentJson, CancellationToken cancellationToken) =>
        recorder.ObserveAsync(target.ConnectionId, collection.Database.Value, collection.Collection.Value,
            QueryOperationKind.Insert, QuerySource.Driver, QueryPurpose.User,
            async () => { await inner.InsertOneAsync(target, collection, documentJson, cancellationToken).ConfigureAwait(false); return true; }, affectedCount: _ => 1);

    public Task<long> ReplaceOneAsync(MongoConnectionTarget target, CollectionRef collection, string idFilterJson, string documentJson, CancellationToken cancellationToken) =>
        recorder.ObserveAsync(target.ConnectionId, collection.Database.Value, collection.Collection.Value,
            QueryOperationKind.Update, QuerySource.Driver, QueryPurpose.User,
            () => inner.ReplaceOneAsync(target, collection, idFilterJson, documentJson, cancellationToken), affectedCount: result => result);

    public Task<long> DeleteOneAsync(MongoConnectionTarget target, CollectionRef collection, string idFilterJson, CancellationToken cancellationToken) =>
        recorder.ObserveAsync(target.ConnectionId, collection.Database.Value, collection.Collection.Value,
            QueryOperationKind.Delete, QuerySource.Driver, QueryPurpose.User,
            () => inner.DeleteOneAsync(target, collection, idFilterJson, cancellationToken), affectedCount: result => result);
}
