using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MongoTui.TUI.Tests.ViewModels;

[CollectionDefinition("Logging", DisableParallelization = true)]
public sealed class LoggingCollection;

[Collection("Logging")]
public sealed class QueryLoggingTests
{
    [Fact]
    public async Task ShouldNot_LogScriptOrExceptionContents_When_QueryFails()
    {
        const string sensitive = "sensitive-document-password";
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var previous = Log.Logger;
        Log.Logger = logger;
        var session = A.Fake<IMongoShellSession>();
        A.CallTo(() => session.IsRunning).Returns(true);
        A.CallTo(() => session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Throws(new QueryExecutionException(sensitive));
        var factory = A.Fake<IMongoShellSessionFactory>();
        A.CallTo(() => factory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(session);
        var tab = new QueryTabViewModel(new QueryService(factory, A.Fake<IQueryHistoryRepository>()),
            ConnectionId.New(), new MongoConnectionTarget($"mongodb://user:{sensitive}@localhost"), "Query",
            "app", "users", $"db.users.insertOne({{password:'{sensitive}'}})");
        try
        {
            await tab.ExecuteAsync(CancellationToken.None);

            tab.Result.Error.Should().Be(sensitive);
            sink.Messages.Should().NotBeEmpty();
            string.Join('\n', sink.Messages).Should().NotContain(sensitive).And.NotContain("mongodb://");
        }
        finally { Log.Logger = previous; }
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public List<string> Messages { get; } = [];

        public void Emit(LogEvent logEvent) => Messages.Add(logEvent.RenderMessage() + logEvent.Exception);
    }
}
