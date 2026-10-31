using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.Infrastructure.Options;
using MongoTui.TUI.Editor;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class QueryMethodCompletionTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Theory]
    [InlineData("db.getCollection('items').fi|({}).toArray()", "find", 2)]
    [InlineData("db.getCollection('items').find({}).li|mt(1).toArray()", "limit", 1)]
    [InlineData("db.getCollection('items').find({}).toA|", "toArray", 2)]
    [InlineData("db.getCollection('items').find({}).limit(1).toA|", "toArray", 1)]
    [InlineData("db.getCollection('items').find({}).maxT|meMS(5000).toArray()", "maxTimeMS", 2)]
    [InlineData("db.getCollection('items').find({}).maxTimeMS(5000).toA|", "toArray", 2)]
    [InlineData("db.getCollection('items').find({}).so|rt({name: 1}).limit(1).toArray()", "sort", 1)]
    public async Task Should_ReturnDocuments_WhenCompletedMethodChainExecuted(string marked, string method, int count)
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var collection = NewCollection();
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"alice\"}", CancellationToken.None);
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"bob\"}", CancellationToken.None);
        await using var session = CreateSession(target);
        var script = Complete(marked, method, collection.Database.Value);

        var result = await session.EvaluateAsync(script, collection.Database.Value, CancellationToken.None);

        result.Documents.Should().HaveCount(count);
        result.Documents.Should().OnlyContain(document => document.Contains("name"));
    }

    [Theory]
    [InlineData("db.getCollection('items').find({}).co|", "count")]
    [InlineData("db.getCollection('items').countD|", "countDocuments")]
    public async Task Should_ReturnCount_WhenCompletedCountMethodExecuted(string marked, string method)
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var collection = NewCollection();
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"alice\"}", CancellationToken.None);
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"bob\"}", CancellationToken.None);
        await using var session = CreateSession(target);
        var script = Complete(marked, method, collection.Database.Value);

        var result = await session.EvaluateAsync(script, collection.Database.Value, CancellationToken.None);

        result.Documents.Should().ContainSingle().Subject["output"].AsString.Should().Be("2");
    }

    private static string Complete(string marked, string method, string database)
    {
        var script = marked.Replace("|", string.Empty, StringComparison.Ordinal);
        var context = QueryCompletionContext.Parse(script, marked.IndexOf('|'), database)!;
        return script.Remove(context.Start, context.Length).Insert(context.Start, context.Replacement(method));
    }

    private MongoShellSession CreateSession(MongoConnectionTarget target) =>
        new(new MongoContainerProcessRunner(fixture.Container), new MongoToolsLocator(new MongoToolsOptions()), target, NullLogger<MongoShellSession>.Instance);

    private static CollectionRef NewCollection() =>
        new(DatabaseName.Create($"it_{Guid.NewGuid():N}"), CollectionName.Create("items"));
}
