using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Domain.Transfer;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>Routes Mongo operations to the driver or the CLI channel.</summary>
public sealed class CompositeMongoGateway : IMongoGateway, IMongoTransferGateway
{
    private readonly IDriverMongoGateway _driver;
    private readonly ICliMongoTransferGateway _cli;

    public CompositeMongoGateway(IDriverMongoGateway driver, ICliMongoTransferGateway cli)
    {
        _driver = driver;
        _cli = cli;
    }

    public Task<IReadOnlyList<DatabaseInfo>> ListDatabasesAsync(
        MongoConnectionTarget target,
        CancellationToken cancellationToken) =>
        _driver.ListDatabasesAsync(target, cancellationToken);

    public Task<IReadOnlyList<string>> ListCollectionsAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        _driver.ListCollectionsAsync(target, database, cancellationToken);

    public Task<IReadOnlyList<IndexInfo>> ListIndexesAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _driver.ListIndexesAsync(target, collection, cancellationToken);

    public Task<CollectionStats> GetCollectionStatsAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _driver.GetCollectionStatsAsync(target, collection, cancellationToken);

    public Task<DatabaseStats> GetDatabaseStatsAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        _driver.GetDatabaseStatsAsync(target, database, cancellationToken);

    public Task DropDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        _driver.DropDatabaseAsync(target, database, cancellationToken);

    public Task DropCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _driver.DropCollectionAsync(target, collection, cancellationToken);

    public Task ClearCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _driver.ClearCollectionAsync(target, collection, cancellationToken);

    public Task CreateIndexAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexKeysJson,
        string? name,
        bool unique,
        CancellationToken cancellationToken) =>
        _driver.CreateIndexAsync(target, collection, indexKeysJson, name, unique, cancellationToken);

    public Task DropIndexAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string indexName,
        CancellationToken cancellationToken) =>
        _driver.DropIndexAsync(target, collection, indexName, cancellationToken);

    public Task<QueryResult> FindAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string filterJson,
        QueryExecutionOptions options,
        CancellationToken cancellationToken) =>
        _driver.FindAsync(target, collection, filterJson, options, cancellationToken);

    public Task<QueryResult> AggregateAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string pipelineJson,
        QueryExecutionOptions options,
        CancellationToken cancellationToken) =>
        _driver.AggregateAsync(target, collection, pipelineJson, options, cancellationToken);

    public Task<long> CountAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string filterJson,
        CancellationToken cancellationToken) =>
        _driver.CountAsync(target, collection, filterJson, cancellationToken);

    public Task InsertOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string documentJson,
        CancellationToken cancellationToken) =>
        _driver.InsertOneAsync(target, collection, documentJson, cancellationToken);

    public Task<long> ReplaceOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        string documentJson,
        CancellationToken cancellationToken) =>
        _driver.ReplaceOneAsync(target, collection, idFilterJson, documentJson, cancellationToken);

    public Task<long> DeleteOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        CancellationToken cancellationToken) =>
        _driver.DeleteOneAsync(target, collection, idFilterJson, cancellationToken);

    public Task ExportAsync(
        MongoConnectionTarget target,
        ExportRequest request,
        CancellationToken cancellationToken) =>
        _cli.ExportAsync(target, request, cancellationToken);

    public Task ImportAsync(
        MongoConnectionTarget target,
        ImportRequest request,
        CancellationToken cancellationToken) =>
        _cli.ImportAsync(target, request, cancellationToken);

    public Task DumpDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string outputDirectory,
        CancellationToken cancellationToken) =>
        _cli.DumpDatabaseAsync(target, database, outputDirectory, cancellationToken);

    public Task RestoreDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string inputDirectory,
        CancellationToken cancellationToken) =>
        _cli.RestoreDatabaseAsync(target, database, inputDirectory, cancellationToken);
}
