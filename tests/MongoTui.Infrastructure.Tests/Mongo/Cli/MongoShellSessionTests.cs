using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Options;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class MongoShellSessionTests
{
    [Fact]
    public async Task Should_ParseDocuments_When_MongoshReturnsJsonArray()
    {
        var session = CreateSession(new ProcessResult(0, "[ { \"a\": 1 }, { \"a\": 2 } ]", string.Empty));

        var result = await session.EvaluateAsync("db.items.find().toArray()", null, CancellationToken.None);

        result.Documents.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_WrapNonJsonOutput_When_MongoshPrintsScalar()
    {
        var session = CreateSession(new ProcessResult(0, "42", string.Empty));

        var result = await session.EvaluateAsync("1 + 1", null, CancellationToken.None);

        result.Documents.Should().ContainSingle();
        result.Documents[0]["output"].AsString.Should().Be("42");
    }

    [Fact]
    public async Task Should_Throw_When_MongoshReportsError()
    {
        const string stdout = "{ \"message\": \"oops\", \"name\": \"MongoServerError\" }";
        var session = CreateSession(new ProcessResult(1, stdout, string.Empty));

        var act = () => session.EvaluateAsync("db.items.find()", null, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<QueryExecutionException>();
        exception.Which.Message.Should().Be("oops");
    }

    [Fact]
    public async Task Should_ReturnStderrMessage_When_MongoshFailsToStart()
    {
        const string stderr = "dyld[50760]: Library not loaded: libicui18n.74.dylib\n";
        var session = CreateSession(new ProcessResult(1, string.Empty, stderr));

        var act = () => session.EvaluateAsync("db.items.find()", null, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<QueryExecutionException>();
        exception.Which.Message.Should().Contain("libicui18n.74.dylib");
    }

    [Fact]
    public async Task Should_PassScriptVerbatim_When_Evaluated()
    {
        const string script = "db.getSiblingDB(\"nominations\").getCollection(\"Templates\").find({}).limit(50).toArray()";
        var captured = new List<ProcessRequest>();
        var session = CreateSession(new ProcessResult(0, "[]", string.Empty), captured);

        await session.EvaluateAsync(script, null, CancellationToken.None);

        var request = captured.Should().ContainSingle().Subject;
        request.FileName.Should().Contain("mongosh");
        request.Arguments.Should().Equal(
            "mongodb://127.0.0.1:27017", "--quiet", "--norc", "--json=relaxed", "--eval", script);
    }

    [Theory]
    [InlineData("app", "db = db.getSiblingDB(\"app\")")]
    [InlineData("a'b", "db = db.getSiblingDB(\"a\\u0027b\")")]
    public async Task Should_SelectDatabaseBeforeOriginalScript_WhenDatabaseProvided(string database, string selection)
    {
        const string script = "db.getCollection('items').find({}).toArray()";
        var captured = new List<ProcessRequest>();
        var session = CreateSession(new ProcessResult(0, "[]", string.Empty), captured);

        await session.EvaluateAsync(script, database, CancellationToken.None);

        captured.Should().ContainSingle().Subject.Arguments.Should().Equal(
            "mongodb://127.0.0.1:27017", "--quiet", "--norc", "--json=relaxed", "--eval", selection, "--eval", script);
    }

    private static MongoShellSession CreateSession(ProcessResult result, List<ProcessRequest>? captured = null)
    {
        var runner = A.Fake<IProcessRunner>();
        A.CallTo(() => runner.RunAsync(A<ProcessRequest>._, A<CancellationToken>._))
            .ReturnsLazily((ProcessRequest request, CancellationToken _) =>
            {
                captured?.Add(request);

                return Task.FromResult(result);
            });

        var tools = new MongoToolsLocator(new MongoToolsOptions());

        return new MongoShellSession(runner, tools, new MongoConnectionTarget("mongodb://127.0.0.1:27017"));
    }
}
