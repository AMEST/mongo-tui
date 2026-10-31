using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Driver;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class CliMongoGatewayDatabaseTests
{
    public static IEnumerable<object?[]> ConnectionCases()
    {
        foreach (var operation in new[] { "export", "import", "dump", "restore" })
        {
            yield return [operation, "mongodb://user:pass@node/", "admin"];
            yield return [operation, "mongodb://user:pass@node/original", "original"];
            yield return [operation, "mongodb://user:pass@node/original?authSource=auth", "auth"];
            yield return [operation, "mongodb://node/original", null];
            yield return [operation, "mongodb+srv://user:pass@cluster.invalid/original?authSource=admin&tls=true", "admin"];
        }
    }

    [Theory]
    [MemberData(nameof(ConnectionCases))]
    public async Task Should_AlignUriWithSelectedDatabaseAndPreserveAuthentication_WhenToolStarted(
        string operation, string connectionString, string? authSource)
    {
        ProcessRequest? command = null;
        var runner = A.Fake<IProcessRunner>();
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._))
            .Invokes((ProcessRequest request, CancellationToken _) => command = request)
            .Returns(new ProcessResult(0, string.Empty, string.Empty));
        var gateway = new CliMongoGateway(runner, new MongoToolsLocator(new MongoToolsOptions()));

        await RunAsync(gateway, operation, new MongoConnectionTarget(connectionString));

        var argument = command!.Arguments.Single(value => value.StartsWith("--uri=", StringComparison.Ordinal));
        var actual = new MongoUrl(argument[6..]);
        var original = new MongoUrl(connectionString);
        actual.DatabaseName.Should().Be("selected");
        actual.AuthenticationSource.Should().Be(authSource);
        actual.Username.Should().Be(original.Username);
        actual.Password.Should().Be(original.Password);
        actual.Scheme.Should().Be(original.Scheme);
        actual.UseTls.Should().Be(original.UseTls);
        command.Arguments.Should().Contain("--db=selected");
    }

    private static Task RunAsync(CliMongoGateway gateway, string operation, MongoConnectionTarget target)
    {
        var database = DatabaseName.Create("selected");
        var collection = new CollectionRef(database, CollectionName.Create("items"));
        return operation switch
        {
            "export" => gateway.ExportAsync(target, new ExportRequest
            { Collection = collection, OutputPath = "/tmp/out.json", Format = ExportFormat.CanonicalJson }, CancellationToken.None),
            "import" => gateway.ImportAsync(target, new ImportRequest
            { Collection = collection, InputPath = "/tmp/in.json", Format = ImportFormat.Json }, CancellationToken.None),
            "dump" => gateway.DumpDatabaseAsync(target, database, "/tmp/dump", CancellationToken.None),
            "restore" => gateway.RestoreDatabaseAsync(target, database, "/tmp/restore", CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }
}
