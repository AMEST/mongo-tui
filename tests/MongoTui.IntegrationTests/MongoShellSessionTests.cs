using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoDB.Driver;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.Infrastructure.Options;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class MongoShellSessionTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task Should_RetainScriptOutput_WhenShellCompletes()
    {
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        await using var session = CreateSession(target);
        await using var output = new ScriptOutput();
        const string script = "console.log('  {migration started}'); console.log('[not JSON]'); console.log(db.getName()); console.log('done  ');";

        await session.EvaluateAsync(script, "raw_test", output, CancellationToken.None);

        output.ReadPreview().Text.Should().Be("  {migration started}\n[not JSON]\nraw_test\ndone  \n");
    }

    [Fact]
    public async Task Should_RetainCompleteLogAndExtractCause_WhenShellFailsAfterLongOutput()
    {
        await using var session = CreateSession(new MongoConnectionTarget(fixture.ConnectionString));
        await using var output = new ScriptOutput();
        const string script = "print('begin|' + 'x'.repeat(100000) + '|end'); process.stderr.write('warning'); throw new Error('migration failed');";

        var failure = await Assert.ThrowsAsync<QueryExecutionException>(() =>
            session.EvaluateAsync(script, null, output, CancellationToken.None));

        failure.Message.Should().Be("migration failed");
        await using var stream = await output.OpenReadAsync(CancellationToken.None);
        using var reader = new StreamReader(stream);
        var log = await reader.ReadToEndAsync();
        log.Should().StartWith("begin|" + new string('x', 100000) + "|end\n")
            .And.Contain("migration failed").And.EndWith("[stderr] warning");
    }

    [Theory]
    [InlineData("db.getCollection('items').find({}).toArray()")]
    [InlineData("db.items.find({}).toArray()")]
    [InlineData("'use strict'; const items = db.getCollection('items'); items.find({}).toArray()")]
    public async Task Should_QuerySelectedDatabase_WhenScriptUsesUnqualifiedDb(string script)
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var collection = NewCollection();
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"alice\"}", CancellationToken.None);
        await using var session = CreateSession(target);

        var result = await session.EvaluateAsync(script, collection.Database.Value, CancellationToken.None);

        result.Documents.Should().ContainSingle().Subject["name"].AsString.Should().Be("alice");
    }

    [Fact]
    public async Task Should_UseExplicitDatabase_WhenScriptOverridesTabDatabase()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var collection = NewCollection();
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"alice\"}", CancellationToken.None);
        await using var session = CreateSession(target);
        var script = $"db.getSiblingDB('{collection.Database.Value}').getCollection('items').find({{}}).toArray()";

        var result = await session.EvaluateAsync(script, "other_database", CancellationToken.None);

        result.Documents.Should().ContainSingle().Subject["name"].AsString.Should().Be("alice");
    }

    [Fact]
    public async Task Should_KeepConnectionDatabase_WhenTabHasNoSelectedDatabase()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var collection = NewCollection();
        var connection = new MongoUrlBuilder(fixture.ConnectionString)
        {
            DatabaseName = collection.Database.Value,
            AuthenticationSource = "admin",
        };
        var target = new MongoConnectionTarget(connection.ToString());
        await gateway.InsertOneAsync(target, collection, "{\"name\":\"alice\"}", CancellationToken.None);
        await using var session = CreateSession(target);

        var result = await session.EvaluateAsync("db.getCollection('items').find({}).toArray()", null, CancellationToken.None);

        result.Documents.Should().ContainSingle().Subject["name"].AsString.Should().Be("alice");
    }

    [Fact]
    public async Task Should_UseChangedDatabase_WhenSessionReused()
    {
        using var gateway = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var target = new MongoConnectionTarget(fixture.ConnectionString);
        var first = NewCollection();
        var second = NewCollection();
        await gateway.InsertOneAsync(target, first, "{\"name\":\"alice\"}", CancellationToken.None);
        await gateway.InsertOneAsync(target, second, "{\"name\":\"bob\"}", CancellationToken.None);
        await using var session = CreateSession(target);
        const string script = "db.getCollection('items').find({}).toArray()";
        await session.EvaluateAsync(script, first.Database.Value, CancellationToken.None);

        var result = await session.EvaluateAsync(script, second.Database.Value, CancellationToken.None);

        result.Documents.Should().ContainSingle().Subject["name"].AsString.Should().Be("bob");
    }

    private MongoShellSession CreateSession(MongoConnectionTarget target) =>
        new(new MongoContainerProcessRunner(fixture.Container), new MongoToolsLocator(new MongoToolsOptions()), target, NullLogger<MongoShellSession>.Instance);

    private static CollectionRef NewCollection() =>
        new(DatabaseName.Create($"it_{Guid.NewGuid():N}"), CollectionName.Create("items"));
}
