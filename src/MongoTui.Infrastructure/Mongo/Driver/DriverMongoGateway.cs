using System.Collections.Concurrent;
using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Domain.Users;

namespace MongoTui.Infrastructure.Mongo.Driver;

/// <summary>Primary MongoDB channel backed by the official driver.</summary>
public sealed class DriverMongoGateway(MongoDatabaseDiscovery databaseDiscovery) : IDriverMongoGateway, IMongoUserGateway, IDisposable
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
            .RunCommandAsync<BsonDocument>(new BsonDocument(MongoWireNames.Commands.DatabaseStats, 1), cancellationToken: cancellationToken);

        return new DatabaseStats
        {
            Name = database.Value,
            CollectionCount = GetInt(stats, MongoWireNames.Fields.Collections),
            ObjectCount = GetLong(stats, MongoWireNames.Fields.Objects),
            DataSizeBytes = TryGetLong(stats, MongoWireNames.Fields.DataSize),
            StorageSizeBytes = TryGetLong(stats, MongoWireNames.Fields.StorageSize),
            IndexCount = GetInt(stats, MongoWireNames.Fields.Indexes),
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
        try
        {
            var documents = await GetCollection(target, collection)
                .Find(ParseFilter(filterJson), new FindOptions { MaxTime = options.Timeout })
                .Skip(options.Skip)
                .Limit(options.Limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return new QueryResult
            {
                Kind = QueryResultKind.Documents,
                Documents = documents,
                Elapsed = stopwatch.Elapsed,
            };
        }
        catch (MongoExecutionTimeoutException exception) when (options.Timeout is not null)
        {
            throw new QueryTimeoutException(options.Timeout.Value, exception);
        }
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
        GetCollection(target, collection).InsertOneAsync(BsonTextCodec.ParseDocument(documentJson), cancellationToken: cancellationToken);

    public async Task<long> ReplaceOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        string documentJson,
        CancellationToken cancellationToken)
    {
        var result = await GetCollection(target, collection).ReplaceOneAsync(
            ParseFilter(idFilterJson),
            BsonTextCodec.ParseDocument(documentJson),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.MatchedCount;
    }

    public async Task<long> DeleteOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        CancellationToken cancellationToken)
    {
        var result = await GetCollection(target, collection)
            .DeleteOneAsync(ParseFilter(idFilterJson), cancellationToken).ConfigureAwait(false);

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
        string.IsNullOrWhiteSpace(filterJson) ? new BsonDocument() : BsonTextCodec.ParseDocument(filterJson);

    private static int GetInt(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt32() : 0;

    private static long GetLong(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt64() : 0;

    private static long? TryGetLong(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt64() : null;

    public async Task<IReadOnlyList<MongoUser>> ListUsersAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = GetClient(target).GetDatabase(database.Value);
            var command = new BsonDocument(MongoWireNames.Commands.UsersInfo, 1);
            var result = await db.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
            var users = new List<MongoUser>();
            if (result.TryGetValue(MongoWireNames.Fields.Users, out var usersArray) && usersArray.IsBsonArray)
            {
                foreach (var userDoc in usersArray.AsBsonArray)
                {
                    if (userDoc is not BsonDocument doc)
                    {
                        continue;
                    }

                    var username = doc.TryGetValue(MongoWireNames.Fields.User, out var u) ? u.AsString : string.Empty;
                    var dbName = doc.TryGetValue(MongoWireNames.Fields.Database, out var d) ? d.AsString : database.Value;
                    var rolesList = new List<MongoRole>();
                    if (doc.TryGetValue(MongoWireNames.Fields.Roles, out var rolesArr) && rolesArr.IsBsonArray)
                    {
                        foreach (var roleVal in rolesArr.AsBsonArray)
                        {
                            if (roleVal is BsonDocument roleDoc)
                            {
                                var roleName = roleDoc.TryGetValue(MongoWireNames.Fields.Role, out var rn) ? rn.AsString : string.Empty;
                                var roleDb = roleDoc.TryGetValue(MongoWireNames.Fields.Database, out var rdb) ? rdb.AsString : string.Empty;
                                if (!string.IsNullOrEmpty(roleName))
                                {
                                    rolesList.Add(new MongoRole(roleName, roleDb));
                                }
                            }
                        }
                    }
                    users.Add(new MongoUser(username, dbName, rolesList));
                }
            }
            return users;
        }
        catch (MongoCommandException ex)
        {
            throw MapUserManagementException(ex);
        }
    }

    public async Task CreateUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string password,
        IReadOnlyList<MongoRole> roles,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = GetClient(target).GetDatabase(database.Value);
            var command = new BsonDocument
            {
                { MongoWireNames.Commands.CreateUser, username },
                { MongoWireNames.Fields.Password, password },
                { MongoWireNames.Fields.Roles, BuildRolesArray(roles) }
            };
            await db.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoCommandException ex)
        {
            throw MapUserManagementException(ex);
        }
    }

    public async Task UpdateUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string? password,
        IReadOnlyList<MongoRole> roles,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = GetClient(target).GetDatabase(database.Value);
            var command = new BsonDocument
            {
                { MongoWireNames.Commands.UpdateUser, username },
                { MongoWireNames.Fields.Roles, BuildRolesArray(roles) }
            };
            if (!string.IsNullOrEmpty(password))
            {
                command[MongoWireNames.Fields.Password] = password;
            }
            await db.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoCommandException ex)
        {
            throw MapUserManagementException(ex);
        }
    }

    public async Task DeleteUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = GetClient(target).GetDatabase(database.Value);
            var command = new BsonDocument { { MongoWireNames.Commands.DropUser, username } };
            await db.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoCommandException ex)
        {
            throw MapUserManagementException(ex);
        }
    }

    private static BsonArray BuildRolesArray(IReadOnlyList<MongoRole> roles)
    {
        var array = new BsonArray();
        foreach (var role in roles)
        {
            array.Add(new BsonDocument { { MongoWireNames.Fields.Role, role.Name }, { MongoWireNames.Fields.Database, role.Database } });
        }
        return array;
    }

    private static Exception MapUserManagementException(MongoCommandException ex)
    {
        var code = ex.Code;
        if (code == 13)
        {
            return new DomainException("Insufficient privileges to manage users.");
        }
        if (code == 51003 || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return new DomainException("User already exists.");
        }
        if (ex.Message.Contains("role", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return new DomainException("Failed to grant role.");
        }
        if (code == 11 || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return new DomainException("User not found.");
        }
        return new DomainException("MongoDB command failed.");
    }

    private static IndexInfo MapIndex(BsonDocument index)
    {
        var fields = index[MongoWireNames.Fields.IndexKeys].AsBsonDocument.Elements
            .Select(element => new IndexField(element.Name, ToDirection(element.Value)))
            .ToList();

        return new IndexInfo
        {
            Name = index[MongoWireNames.Fields.Name].AsString,
            Fields = fields,
            Unique = index.TryGetValue(MongoWireNames.Fields.Unique, out var unique) && unique.ToBoolean(),
            Sparse = index.TryGetValue(MongoWireNames.Fields.Sparse, out var sparse) && sparse.ToBoolean(),
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
