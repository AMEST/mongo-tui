using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

[CollectionDefinition("Logging", DisableParallelization = true)]
public sealed class LoggingCollection;

[Collection("Logging")]
public sealed class MongoShellLoggingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNot_LogSensitiveOutput_When_ShellReturnsDiagnostics(bool fails)
    {
        const string sensitive = "sensitive-document-password";
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var previous = Log.Logger;
        Log.Logger = logger;
        var runner = A.Fake<IProcessRunner>();
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._))
            .Returns(new ProcessResult(fails ? 1 : 0, fails ? $"{{\"message\":\"{sensitive}\"}}" : "[]", sensitive));
        var session = new MongoShellSession(runner, new MongoToolsLocator(new MongoToolsOptions()),
            new MongoConnectionTarget($"mongodb://user:{sensitive}@localhost"));
        try
        {
            await EvaluateAsync(session, sensitive);

            sink.Messages.Should().NotBeEmpty();
            string.Join('\n', sink.Messages).Should().NotContain(sensitive).And.NotContain("mongodb://");
        }
        finally { Log.Logger = previous; }
    }

    private static async Task EvaluateAsync(MongoShellSession session, string script)
    {
        try { await session.EvaluateAsync(script, "app", CancellationToken.None); }
        catch (QueryExecutionException) { }
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public List<string> Messages { get; } = [];

        public void Emit(LogEvent logEvent) => Messages.Add(logEvent.RenderMessage() + logEvent.Exception);
    }
}
