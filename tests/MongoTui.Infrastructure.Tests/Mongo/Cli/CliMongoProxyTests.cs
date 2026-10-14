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

public sealed class CliMongoProxyTests
{
    private readonly IProcessRunner _runner = A.Fake<IProcessRunner>();
    private readonly CliMongoGateway _gateway;

    public CliMongoProxyTests()
    {
        _gateway = new CliMongoGateway(_runner, new MongoToolsLocator(new MongoToolsOptions()));
        A.CallTo(() => _runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._))
            .Returns(new ProcessResult(0, string.Empty, string.Empty));
    }

    [Theory]
    [InlineData("export")]
    [InlineData("import")]
    [InlineData("dump")]
    [InlineData("restore")]
    public async Task ShouldThrow_ProxyTransferNotSupportedExceptionWithoutStartingTool_WhenSocksProxyConfigured(string operation)
    {
        var target = new MongoConnectionTarget("mongodb://user:secret@node/?proxyHost=127.0.0.1&proxyPort=1080");

        var failure = await Record.ExceptionAsync(() => RunAsync(operation, target));

        failure.Should().BeOfType<ProxyTransferNotSupportedException>()
            .Which.Message.Should().Contain("do not support SOCKS").And.NotContain("secret");
        A.CallTo(() => _runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("mongodb+srv://unresolvable.invalid/?proxyHost=proxy&proxyPort=1080")]
    [InlineData("mongodb://node/?PROXYHOST=proxy&PROXYPORT=1080")]
    [InlineData("mongodb://node/?proxyHost=proxy")]
    public async Task ShouldThrow_ProxyTransferNotSupportedExceptionWithoutStartingTool_WhenProxyUriVaries(string uri)
    {
        var target = new MongoConnectionTarget(uri);

        var failure = await Record.ExceptionAsync(() => RunAsync("export", target));

        failure.Should().BeOfType<ProxyTransferNotSupportedException>();
        A.CallTo(() => _runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("mongodb://user:proxyHost%3Dproxy@node/")]
    [InlineData("mongodb://node/?appName=proxyHost%3Dproxy")]
    public async Task Should_RunTool_WhenProxyTextAppearsOutsideProxyOptions(string uri)
    {
        var target = new MongoConnectionTarget(uri);

        await RunAsync("export", target);

        A.CallTo(() => _runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private Task RunAsync(string operation, MongoConnectionTarget target)
    {
        var database = DatabaseName.Create("app");
        var collection = new CollectionRef(database, CollectionName.Create("users"));
        return operation switch
        {
            "export" => _gateway.ExportAsync(target, new ExportRequest
            { Collection = collection, OutputPath = "/tmp/out.json", Format = ExportFormat.Json }, CancellationToken.None),
            "import" => _gateway.ImportAsync(target, new ImportRequest
            { Collection = collection, InputPath = "/tmp/in.json", Format = ImportFormat.Json }, CancellationToken.None),
            "dump" => _gateway.DumpDatabaseAsync(target, database, "/tmp/dump", CancellationToken.None),
            "restore" => _gateway.RestoreDatabaseAsync(target, database, "/tmp/dump", CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }
}
