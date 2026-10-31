using AwesomeAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Models;
using MongoTui.Domain.Transfer;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class CollectionExportTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("admin", false)]
    [InlineData("admin", true)]
    public async Task Should_ExportCanonicalJsonFromSelectedDatabase_WhenUriHasDifferentDefault(string? defaultDatabase, bool explicitAuthSource)
    {
        using var database = new DocumentTestDatabase(fixture);
        var document = new BsonDocument { { "_id", 1 }, { "value", new BsonInt64(42) } };
        await database.InsertAsync(document);
        var uri = new MongoUrlBuilder(fixture.ConnectionString)
        {
            DatabaseName = defaultDatabase,
            AuthenticationSource = explicitAuthSource ? "admin" : null,
        };
        var output = $"/tmp/mongotui-export-{Guid.NewGuid():N}.jsonl";
        var gateway = new CliMongoGateway(new MongoContainerProcessRunner(fixture.Container), new MongoToolsLocator(new MongoToolsOptions()));

        await gateway.ExportAsync(new MongoConnectionTarget(uri.ToString()), new ExportRequest
        { Collection = database.Collection, OutputPath = output, Format = ExportFormat.CanonicalJson }, CancellationToken.None);

        var result = await fixture.Container.ExecAsync(["cat", output]);
        result.Stdout.Should().Contain("$numberLong");
        BsonDocument.Parse(result.Stdout).ToBson().Should().Equal(document.ToBson());
    }
}
