using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Domain.Users;
using MongoTui.Infrastructure.Mongo.Driver;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DriverMongoGatewayTests : IClassFixture<MongoFixture>
{
    private readonly MongoConnectionTarget _target;

    public DriverMongoGatewayTests(MongoFixture fixture) => _target = new MongoConnectionTarget(fixture.ConnectionString);

    [Fact]
    public async Task Should_InsertAndFindDocument_When_CollectionAvailable()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();

        await gateway.InsertOneAsync(_target, collection, "{ \"name\": \"alice\", \"age\": 30 }", CancellationToken.None);
        var result = await gateway.FindAsync(_target, collection, "{}", new QueryExecutionOptions(), CancellationToken.None);

        result.Documents.Should().ContainSingle();
        result.Documents[0]["name"].AsString.Should().Be("alice");
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_CollectionIsEmpty()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();

        var result = await gateway.FindAsync(_target, collection, "{}", new QueryExecutionOptions(), CancellationToken.None);

        result.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_CountDocuments_When_CollectionHasDocs()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"v\": 1 }", CancellationToken.None);
        await gateway.InsertOneAsync(_target, collection, "{ \"v\": 2 }", CancellationToken.None);

        var count = await gateway.CountAsync(_target, collection, "{}", CancellationToken.None);

        count.Should().Be(2);
    }

    [Fact]
    public async Task Should_ReplaceDocument_When_IdFilterMatches()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"_id\": 1, \"v\": 1 }", CancellationToken.None);

        var modified = await gateway.ReplaceOneAsync(
            _target,
            collection,
            "{ \"_id\": 1 }",
            "{ \"_id\": 1, \"v\": 42 }",
            CancellationToken.None);
        var result = await gateway.FindAsync(_target, collection, "{ \"_id\": 1 }", new QueryExecutionOptions(), CancellationToken.None);

        modified.Should().Be(1);
        result.Documents[0]["v"].AsInt32.Should().Be(42);
    }

    [Fact]
    public async Task Should_DeleteDocument_When_IdFilterMatches()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"_id\": 1, \"v\": 1 }", CancellationToken.None);

        var deleted = await gateway.DeleteOneAsync(_target, collection, "{ \"_id\": 1 }", CancellationToken.None);
        var count = await gateway.CountAsync(_target, collection, "{}", CancellationToken.None);

        deleted.Should().Be(1);
        count.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    public async Task Should_ReturnMatchedCount_When_ReplacementIsUnchangedOrMissing(int id, long expected)
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{\"_id\":1,\"v\":1}", CancellationToken.None);

        var matched = await gateway.ReplaceOneAsync(_target, collection,
            $"{{\"_id\":{id}}}", $"{{\"_id\":{id},\"v\":1}}", CancellationToken.None);

        matched.Should().Be(expected);
    }

    [Fact]
    public async Task Should_ListCollections_When_DatabaseHasCollection()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"v\": 1 }", CancellationToken.None);

        var collections = await gateway.ListCollectionsAsync(_target, collection.Database, CancellationToken.None);

        collections.Should().Contain(collection.Collection.Value);
    }

    [Fact]
    public async Task Should_ListIndexes_When_CollectionExists()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"v\": 1 }", CancellationToken.None);

        var indexes = await gateway.ListIndexesAsync(_target, collection, CancellationToken.None);

        indexes.Should().Contain(index => index.Name == "_id_");
    }

    [Fact]
    public async Task Should_AggregateDocuments_When_PipelineProvided()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"v\": 1 }", CancellationToken.None);
        await gateway.InsertOneAsync(_target, collection, "{ \"v\": 2 }", CancellationToken.None);

        var result = await gateway.AggregateAsync(
            _target,
            collection,
            "[ { \"$match\": {} }, { \"$count\": \"total\" } ]",
            new QueryExecutionOptions(),
            CancellationToken.None);

        result.Documents.Should().ContainSingle();
        result.Documents[0]["total"].AsInt32.Should().Be(2);
    }

    [Fact]
    public async Task Should_CreateAndDropIndex_When_Requested()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"email\": \"a\" }", CancellationToken.None);
        await gateway.CreateIndexAsync(_target, collection, "{ \"email\": 1 }", "email_1", false, false, CancellationToken.None);

        var indexes = await gateway.ListIndexesAsync(_target, collection, CancellationToken.None);

        indexes.Should().Contain(index => index.Name == "email_1");
    }

    [Fact]
    public async Task Should_ReturnStats_When_CollectionHasDocuments()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        await gateway.InsertOneAsync(_target, collection, "{ \"v\": 1 }", CancellationToken.None);

        var stats = await gateway.GetCollectionStatsAsync(_target, collection, CancellationToken.None);

        stats.DocumentCount.Should().Be(1);
        stats.IndexCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_CreateEmptyCollectionAndDatabase_WhenRequested()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var collection = NewCollection();
        var service = new DatabaseService(gateway);

        await service.CreateCollectionAsync(_target, collection, CancellationToken.None);

        var collections = await gateway.ListCollectionsAsync(_target, collection.Database, CancellationToken.None);
        collections.Should().ContainSingle().Which.Should().Be(collection.Collection.Value);
        (await gateway.CountAsync(_target, collection, "{}", CancellationToken.None)).Should().Be(0);
        (await gateway.ListDatabasesAsync(_target, CancellationToken.None))
            .Should().Contain(database => database.Name == collection.Database.Value);
    }

    [Fact]
    public async Task Should_CreateListUpdateAndDeleteUser_When_Requested()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery());
        var userGateway = (IMongoUserGateway)gateway;
        var database = DatabaseName.Create($"it_users_{Guid.NewGuid():N}");
        var username = "reader";

        await userGateway.CreateUserAsync(_target, database, username, "secret1",
            [new MongoRole("read", database.Value)], CancellationToken.None);
        var users = await userGateway.ListUsersAsync(_target, database, CancellationToken.None);
        await userGateway.UpdateUserAsync(_target, database, username, "secret2",
            [new MongoRole("readWrite", database.Value)], CancellationToken.None);
        await userGateway.DeleteUserAsync(_target, database, username, CancellationToken.None);
        var remaining = await userGateway.ListUsersAsync(_target, database, CancellationToken.None);

        users.Should().ContainSingle(user => user.Username == username);
        remaining.Should().NotContain(user => user.Username == username);
    }

    private static CollectionRef NewCollection()
    {
        var database = DatabaseName.Create($"it_{Guid.NewGuid():N}");
        var collection = CollectionName.Create("items");

        return new CollectionRef(database, collection);
    }
}
