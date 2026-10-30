using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.Infrastructure.Options;

namespace MongoTui.IntegrationTests;

internal sealed class DocumentTestDatabase(MongoFixture fixture) : IDisposable
{
    public MongoConnectionTarget Target { get; } = new(fixture.ConnectionString);
    public CollectionRef Collection { get; } = new(DatabaseName.Create($"it_documents_{Guid.NewGuid():N}"), CollectionName.Create("items"));
    public DriverMongoGateway Gateway { get; } = new(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
    private readonly MongoClient _client = new(fixture.ConnectionString);

    public Task InsertAsync(BsonDocument document) => TypedCollection().InsertOneAsync(document);

    public Task<BsonDocument> ReadAsync() => TypedCollection().Find(new BsonDocument()).SingleAsync();

    public async Task<byte[]> ReadBytesAsync()
    {
        using var document = await _client.GetDatabase(Collection.Database.Value)
            .GetCollection<RawBsonDocument>(Collection.Collection.Value).Find(new BsonDocument()).SingleAsync();
        return document.ToBson();
    }

    public async Task<BsonDocument> ReadShellAsync()
    {
        await using var shell = new MongoShellSession(new MongoContainerProcessRunner(fixture.Container),
            new MongoToolsLocator(new MongoToolsOptions()), Target, NullLogger<MongoShellSession>.Instance);
        var result = await shell.EvaluateAsync("db.items.find({}).toArray()", Collection.Database.Value, CancellationToken.None);
        return result.Documents.Single();
    }

    public void Dispose() { Gateway.Dispose(); _client.Dispose(); }

    private IMongoCollection<BsonDocument> TypedCollection() =>
        _client.GetDatabase(Collection.Database.Value).GetCollection<BsonDocument>(Collection.Collection.Value);
}
