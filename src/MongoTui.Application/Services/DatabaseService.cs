using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Services;

/// <summary>Database- and collection-level maintenance operations.</summary>
public sealed class DatabaseService
{
    private readonly IMongoGateway _gateway;

    public DatabaseService(IMongoGateway gateway) => _gateway = gateway;

    public Task<DatabaseStats> GetStatsAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        _gateway.GetDatabaseStatsAsync(target, database, cancellationToken);

    public Task DropDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken) =>
        _gateway.DropDatabaseAsync(target, database, cancellationToken);

    public Task DropCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _gateway.DropCollectionAsync(target, collection, cancellationToken);

    public Task ClearCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken) =>
        _gateway.ClearCollectionAsync(target, collection, cancellationToken);

    public Task CreateCollectionAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        CancellationToken cancellationToken)
    {
        collection.Collection.ValidateForCreation();
        return _gateway.CreateCollectionAsync(target, collection, cancellationToken);
    }

    /// <summary>Creates a new database by creating its first empty collection; existing databases are rejected.</summary>
    public async Task CreateDatabaseAsync(
        MongoConnectionTarget target,
        CollectionRef firstCollection,
        CancellationToken cancellationToken)
    {
        firstCollection.Collection.ValidateForCreation();
        var databases = await _gateway.ListDatabasesAsync(target, cancellationToken).ConfigureAwait(false);
        if (databases.Any(database => database.Name.Equals(firstCollection.Database.Value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("A database with this name already exists.");
        }

        await _gateway.CreateCollectionAsync(target, firstCollection, cancellationToken).ConfigureAwait(false);
    }
}
