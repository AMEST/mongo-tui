using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;
using MongoTui.Testing;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class MongoShellLoggingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNot_LogSensitiveOutput_When_ShellReturnsDiagnostics(bool fails)
    {
        const string sensitive = "sensitive-document-password";
        var sink = new LoggingTestSink();
        using var loggers = sink.CreateFactory();
        var runner = A.Fake<IProcessRunner>();
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._))
            .Returns(new ProcessResult(fails ? 1 : 0, fails ? $"{{\"message\":\"{sensitive}\"}}" : "[]", sensitive));
        await using var session = new MongoShellSession(runner, new MongoToolsLocator(new MongoToolsOptions()),
            new MongoConnectionTarget($"mongodb://user:{sensitive}@localhost"), loggers.CreateLogger<MongoShellSession>());

        try { await session.EvaluateAsync(sensitive, "app", CancellationToken.None); }
        catch (QueryExecutionException) { }

        sink.Entries.Should().NotBeEmpty().And.OnlyContain(entry => entry.Category == typeof(MongoShellSession).FullName);
        sink.Text.Should().NotContain(sensitive).And.NotContain("mongodb://");
    }
}
