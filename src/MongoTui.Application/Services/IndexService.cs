using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;

namespace MongoTui.Application.Services;

/// <summary>Collection index and statistics operations.</summary>
public sealed class IndexService
{
    private readonly IMongoGateway _gateway;

    public IndexService(IMongoGateway gateway) => _gateway = gateway;

    public Task<IReadOnlyList<IndexInfo>> ListAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _gateway.ListIndexesAsync(target, collection, cancellationToken);

    public Task<CollectionStats> GetStatsAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _gateway.GetCollectionStatsAsync(target, collection, cancellationToken);

    public Task CreateAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexKeysJson,
        string? name,
        bool unique,
        CancellationToken cancellationToken) =>
        _gateway.CreateIndexAsync(target, collection, indexKeysJson, name, unique, cancellationToken);

    public Task DropAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexName,
        CancellationToken cancellationToken) =>
        _gateway.DropIndexAsync(target, collection, indexName, cancellationToken);
}
