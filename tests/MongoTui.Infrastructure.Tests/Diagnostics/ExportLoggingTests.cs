using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;
using MongoTui.Testing;

namespace MongoTui.Infrastructure.Tests.Diagnostics;

public sealed class ExportLoggingTests
{
    private const string Sensitive = "private-export-document-password";
    private readonly LoggingTestSink _sink = new();
    private readonly MongoConnectionTarget _target = new($"mongodb://user:{Sensitive}@localhost");
    private readonly ExportRequest _request = new()
    {
        Collection = new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("users")),
        Format = ExportFormat.CanonicalJson,
        OutputPath = $"/tmp/{Sensitive}.json",
        FilterJson = $"{{password:'{Sensitive}'}}",
        Fields = [Sensitive],
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_LogSafeFailureOnce_WhenMongoexportFails(bool delayed)
    {
        using var loggers = _sink.CreateFactory();
        using var cancellation = new CancellationTokenSource();
        var runner = A.Fake<IProcessRunner>();
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, cancellation.Token))
            .ReturnsLazily(() => ToolResultAsync(delayed, cancellation.Token));
        var gateway = new CliMongoGateway(runner, new MongoToolsLocator(new MongoToolsOptions()));
        var service = new ExportService(gateway, loggers.CreateLogger<ExportService>());

        var error = await Record.ExceptionAsync(() => service.ExportAsync(_target, _request, cancellation.Token));

        error.Should().BeOfType<QueryExecutionException>().Which.Message.Should().Contain(Sensitive);
        AssertFailure(LogLevel.Warning, nameof(QueryExecutionException));
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, cancellation.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_LogValidationFailure_WhenOutputPathMissing()
    {
        using var loggers = _sink.CreateFactory();
        var transfer = A.Fake<IMongoTransferGateway>();
        var service = new ExportService(transfer, loggers.CreateLogger<ExportService>());

        var error = await Record.ExceptionAsync(() => service.ExportAsync(_target, _request with { OutputPath = " " }, CancellationToken.None));

        error.Should().BeOfType<DomainException>();
        AssertFailure(LogLevel.Warning, nameof(DomainException));
        A.CallTo(() => transfer.ExportAsync(A<MongoConnectionTarget>._, A<ExportRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_LogProxyFailure_WhenExportBlockedBeforeToolStarts()
    {
        using var loggers = _sink.CreateFactory();
        var runner = A.Fake<IProcessRunner>();
        var gateway = new CliMongoGateway(runner, new MongoToolsLocator(new MongoToolsOptions()));
        var service = new ExportService(gateway, loggers.CreateLogger<ExportService>());
        var target = new MongoConnectionTarget($"mongodb://user:{Sensitive}@localhost/?proxyHost=127.0.0.1&proxyPort=1080");

        var error = await Record.ExceptionAsync(() => service.ExportAsync(target, _request, CancellationToken.None));

        error.Should().BeOfType<ProxyTransferNotSupportedException>();
        AssertFailure(LogLevel.Warning, nameof(ProxyTransferNotSupportedException));
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_LogErrorAndPreserveException_WhenUnexpectedFailureOccurs(bool asynchronous)
    {
        using var loggers = _sink.CreateFactory();
        var transfer = A.Fake<IMongoTransferGateway>();
        var failure = new InvalidOperationException(Sensitive, new IOException($"mongodb://user:{Sensitive}@localhost"));
        var call = A.CallTo(() => transfer.ExportAsync(_target, _request, A<CancellationToken>._));
        if (asynchronous) call.ThrowsAsync(failure);
        else call.Throws(failure);
        var service = new ExportService(transfer, loggers.CreateLogger<ExportService>());

        var error = await Record.ExceptionAsync(() => service.ExportAsync(_target, _request, CancellationToken.None));

        error.Should().BeSameAs(failure);
        AssertFailure(LogLevel.Error, nameof(InvalidOperationException));
        _sink.Text.Should().Contain(nameof(IOException));
    }

    [Fact]
    public async Task Should_LogCancellationWithoutFailure_WhenExportCancelled()
    {
        using var loggers = _sink.CreateFactory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var transfer = A.Fake<IMongoTransferGateway>();
        A.CallTo(() => transfer.ExportAsync(_target, _request, cancellation.Token)).Returns(Task.FromCanceled(cancellation.Token));
        var service = new ExportService(transfer, loggers.CreateLogger<ExportService>());

        var error = await Record.ExceptionAsync(() => service.ExportAsync(_target, _request, cancellation.Token));

        error.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(cancellation.Token);
        var entry = _sink.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("cancelled");
        _sink.Text.Should().NotContain(Sensitive).And.NotContain("mongodb://");
    }

    [Fact]
    public async Task ShouldNot_LogFailure_WhenExportSucceeds()
    {
        using var loggers = _sink.CreateFactory();
        var transfer = A.Fake<IMongoTransferGateway>();
        var service = new ExportService(transfer, loggers.CreateLogger<ExportService>());

        await service.ExportAsync(_target, _request, CancellationToken.None);

        _sink.Entries.Should().BeEmpty();
        A.CallTo(() => transfer.ExportAsync(_target, _request, CancellationToken.None)).MustHaveHappenedOnceExactly();
    }

    private void AssertFailure(LogLevel level, string type)
    {
        var entry = _sink.Entries.Should().ContainSingle().Subject;
        entry.Category.Should().Be(typeof(ExportService).FullName);
        entry.Level.Should().Be(level);
        entry.Properties["Database"].Should().Be("app");
        entry.Properties["Collection"].Should().Be("users");
        entry.Properties["Action"].Should().Be("export");
        entry.Properties["Format"].Should().Be(ExportFormat.CanonicalJson);
        entry.Message.Should().Contain("export failed").And.Contain(type);
        entry.Exception.Should().BeNull();
        _sink.Text.Should().NotContain(Sensitive).And.NotContain("mongodb://");
    }

    private static async Task<ProcessResult> ToolResultAsync(bool delayed, CancellationToken cancellationToken)
    {
        if (delayed) await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        return new ProcessResult(2, Sensitive, Sensitive);
    }
}
