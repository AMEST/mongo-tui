using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.Infrastructure.Options;
using MongoTui.TUI.Editor;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class QueryAutocompleteTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Theory]
    [InlineData("order'items")]
    [InlineData("order\"items")]
    [InlineData("order\\items")]
    [InlineData("getSiblingDB")]
    [InlineData("getCollection")]
    [InlineData("stats")]
    [InlineData("constructor")]
    public async Task Should_ExecuteCompletedQuery_WhenCollectionNameNeedsExplicitAccess(string name)
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var collection = NewCollection(name);
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"alice\"}", CancellationToken.None);
        var autocomplete = new AutocompleteService(gateway);
        var root = QueryCompletionContext.Parse("db.", 3, collection.Database.Value)!;
        var databases = await autocomplete.GetSuggestionsAsync(
            target, root.Database, root.Collection, root.Prefix, CancellationToken.None);
        var database = databases.Single(candidate => candidate == collection.Database.Value);
        var script = $"db.{root.Replacement(database)}.getCollection(\"{name[..3]}\")";
        var context = QueryCompletionContext.Parse(script, script.Length - 2, null)!;
        var suggestions = await autocomplete.GetSuggestionsAsync(
            target, context.Database, context.Collection, context.Prefix, CancellationToken.None);
        var completed = script.Remove(context.Start, context.Length)
            .Insert(context.Start, context.Replacement(suggestions.Single())) + ".find({}).toArray()";
        await using var session = new MongoShellSession(
            new MongoContainerProcessRunner(fixture.Container), new MongoToolsLocator(new MongoToolsOptions()), target, NullLogger<MongoShellSession>.Instance);

        var result = await session.EvaluateAsync(completed, null, CancellationToken.None);

        result.Documents.Should().ContainSingle().Subject["name"].AsString.Should().Be("alice");
    }

    [Theory]
    [InlineData("order'items", "order\\'items")]
    [InlineData("order\"items", "order\"items")]
    [InlineData("order\\items", "order\\\\items")]
    [InlineData("order-items", "order\\x2ditems")]
    public async Task Should_LoadFields_WhenDatabaseAndCollectionLiteralsContainEscapes(string name, string literal)
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var collection = NewCollection(name);
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"alice\"}", CancellationToken.None);
        var database = collection.Database.Value.Replace("it_", "\\u0069t_", StringComparison.Ordinal);
        var script = $"db.getSiblingDB('{database}').getCollection('{literal}').find({{ na";
        var context = QueryCompletionContext.Parse(script, script.Length, null)!;
        var autocomplete = new AutocompleteService(gateway);

        var suggestions = await autocomplete.GetSuggestionsAsync(
            target, context.Database, context.Collection, context.Prefix, CancellationToken.None);

        suggestions.Should().Equal("name");
    }

    private static CollectionRef NewCollection(string name) =>
        new(DatabaseName.Create($"it_{Guid.NewGuid():N}"), CollectionName.Create(name));
}
