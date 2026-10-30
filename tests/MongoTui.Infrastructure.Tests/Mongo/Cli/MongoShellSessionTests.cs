using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
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

    [Theory]
    [InlineData("{ \"$numberInt\": \"0\" }", "0")]
    [InlineData("{ \"$numberInt\": \"2\" }", "2")]
    [InlineData("{ \"$numberLong\": \"9223372036854775807\" }", "9223372036854775807")]
    [InlineData("{ \"$numberDouble\": \"2.5\" }", "2.5")]
    [InlineData("{ \"$numberDecimal\": \"1234567890.123456789\" }", "1234567890.123456789")]
    public async Task Should_UnwrapScalar_WhenMongoshReturnsCanonicalNumber(string stdout, string expected)
    {
        var session = CreateSession(new ProcessResult(0, stdout, string.Empty));

        var result = await session.EvaluateAsync("db.items.countDocuments()", null, CancellationToken.None);

        result.Documents.Should().ContainSingle().Subject["output"].AsString.Should().Be(expected);
        result.RawOutput.Should().Be(stdout);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PreserveBsonTypes_WhenMongoshReturnsDocuments(bool array)
    {
        const string json = "{ \"_id\": { \"$oid\": \"507f1f77bcf86cd799439011\" }, \"count\": { \"$numberLong\": \"9007199254740993\" } }";
        var stdout = array ? $"[{json}]" : json;
        var session = CreateSession(new ProcessResult(0, stdout, string.Empty));

        var result = await session.EvaluateAsync("db.items.findOne()", null, CancellationToken.None);

        var document = result.Documents.Should().ContainSingle().Subject;
        document["_id"].AsObjectId.Should().Be(ObjectId.Parse("507f1f77bcf86cd799439011"));
        document["count"].BsonType.Should().Be(BsonType.Int64);
        document["count"].AsInt64.Should().Be(9007199254740993L);
    }

    [Fact]
    public async Task Should_CaptureRawStdout_When_MongoshPrintsText()
    {
        var session = CreateSession(new ProcessResult(0, "migration complete\n", string.Empty));

        var result = await session.EvaluateAsync("print('migration complete')", null, CancellationToken.None);

        result.RawOutput.Should().Be("migration complete");
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
            "mongodb://127.0.0.1:27017", "--quiet", "--norc", "--json=canonical", "--eval", script);
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
            "mongodb://127.0.0.1:27017", "--quiet", "--norc", "--json=canonical", "--eval", selection, "--eval", script);
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

        return new MongoShellSession(runner, tools, new MongoConnectionTarget("mongodb://127.0.0.1:27017"), NullLogger<MongoShellSession>.Instance);
    }
}
