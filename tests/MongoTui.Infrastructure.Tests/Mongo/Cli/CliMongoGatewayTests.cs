using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class CliMongoGatewayTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");

    [Fact]
    public async Task Should_AddQueryAndType_When_JsonExportRequested()
    {
        var gateway = CreateGateway(out var captured);
        var request = new ExportRequest
        {
            Collection = NewCollection(),
            OutputPath = "/tmp/out.json",
            Format = ExportFormat.Json,
            FilterJson = "{ \"a\": 1 }",
        };

        await gateway.ExportAsync(Target, request, CancellationToken.None);

        captured()!.FileName.Should().EndWith("mongoexport");
        captured()!.Arguments.Should().Contain("--type=json");
        captured()!.Arguments.Should().Contain("--query={ \"a\": 1 }");
        captured()!.Arguments.Should().Contain("--out=/tmp/out.json");
    }

    [Fact]
    public async Task Should_AddCanonicalFormat_When_CanonicalJsonRequested()
    {
        var gateway = CreateGateway(out var captured);
        var request = new ExportRequest
        {
            Collection = NewCollection(),
            OutputPath = "/tmp/out.json",
            Format = ExportFormat.CanonicalJson,
        };

        await gateway.ExportAsync(Target, request, CancellationToken.None);

        captured()!.Arguments.Should().Contain("--type=json");
        captured()!.Arguments.Should().Contain("--jsonFormat=canonical");
    }

    [Fact]
    public async Task ShouldThrow_NotSupportedException_When_BsonExportRequested()
    {
        var gateway = CreateGateway(out _);
        var request = new ExportRequest
        {
            Collection = NewCollection(),
            OutputPath = "/tmp/out.bson",
            Format = ExportFormat.Bson,
        };

        var act = async () => await gateway.ExportAsync(Target, request, CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Should_AddUpsertMode_When_ImportUpsertRequested()
    {
        var gateway = CreateGateway(out var captured);
        var request = new ImportRequest
        {
            Collection = NewCollection(),
            InputPath = "/tmp/in.csv",
            Format = ImportFormat.Csv,
            Mode = ImportMode.Upsert,
        };

        await gateway.ImportAsync(Target, request, CancellationToken.None);

        captured()!.FileName.Should().EndWith("mongoimport");
        captured()!.Arguments.Should().Contain("--type=csv");
        captured()!.Arguments.Should().Contain("--mode=upsert");
        captured()!.Arguments.Should().NotContain("--drop");
    }

    [Fact]
    public async Task Should_AddDropFlag_When_DropRequested()
    {
        var gateway = CreateGateway(out var captured);
        var request = new ImportRequest
        {
            Collection = NewCollection(),
            InputPath = "/tmp/in.json",
            Format = ImportFormat.Json,
            DropCollection = true,
        };

        await gateway.ImportAsync(Target, request, CancellationToken.None);

        captured()!.Arguments.Should().Contain("--drop");
    }

    [Fact]
    public async Task Should_RunMongoDump_When_DumpRequested()
    {
        var gateway = CreateGateway(out var captured);

        await gateway.DumpDatabaseAsync(Target, DatabaseName.Create("app"), "/tmp/dump", CancellationToken.None);

        captured()!.FileName.Should().EndWith("mongodump");
        captured()!.Arguments.Should().Contain("--db=app");
        captured()!.Arguments.Should().Contain("--out=/tmp/dump");
    }

    [Fact]
    public async Task ShouldThrow_QueryExecutionException_When_ToolFails()
    {
        var gateway = CreateGateway(out _, new ProcessResult(2, string.Empty, "boom"));
        var request = new ExportRequest
        {
            Collection = NewCollection(),
            OutputPath = "/tmp/out.json",
            Format = ExportFormat.Json,
        };

        var act = async () => await gateway.ExportAsync(Target, request, CancellationToken.None);

        await act.Should().ThrowAsync<QueryExecutionException>();
    }

    private static CliMongoGateway CreateGateway(out Func<ProcessRequest?> captured, ProcessResult? result = null)
    {
        ProcessRequest? request = null;
        var runner = A.Fake<IProcessRunner>();
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._))
            .Invokes((ProcessRequest value, CancellationToken _) => request = value)
            .Returns(Task.FromResult(result ?? new ProcessResult(0, string.Empty, string.Empty)));

        captured = () => request;

        return new CliMongoGateway(runner, new MongoToolsLocator(new MongoToolsOptions()));
    }

    private static CollectionRef NewCollection() =>
        new(DatabaseName.Create("app"), CollectionName.Create("users"));
}
