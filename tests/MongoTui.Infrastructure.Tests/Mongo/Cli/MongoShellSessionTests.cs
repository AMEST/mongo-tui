using System.Text;
using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Mongo.Cli;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class MongoShellSessionTests
{
    [Fact]
    public async Task Should_WrapNonJsonOutput_When_ScriptPrintsScalar()
    {
        var session = CreateSession("42\n__MONGOTUI_END_1__\n");

        var result = await session.EvaluateAsync("1 + 1", CancellationToken.None);

        result.Documents.Should().ContainSingle();
        result.Documents[0]["output"].AsString.Should().Be("42");
    }

    [Fact]
    public async Task Should_ParseJsonArray_When_ScriptPrintsArray()
    {
        var session = CreateSession("[ { \"a\": 1 }, { \"a\": 2 } ]\n__MONGOTUI_END_1__\n");

        var result = await session.EvaluateAsync("db.items.find().toArray()", CancellationToken.None);

        result.Documents.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_WriteScriptAndMarker_When_Evaluated()
    {
        var input = new MemoryStream();
        var process = A.Fake<IProcessSession>();
        A.CallTo(() => process.StandardOutput)
            .Returns(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes("__MONGOTUI_END_1__\n"))));
        A.CallTo(() => process.StandardInput)
            .Returns(new StreamWriter(input, new UTF8Encoding(false)));
        var session = new MongoShellSession(process);

        await session.EvaluateAsync("print(\"hi\")", CancellationToken.None);

        var written = Encoding.UTF8.GetString(input.ToArray());
        written.Should().Contain("print(\"hi\")");
        written.Should().Contain("__MONGOTUI_END_1__");
    }

    [Fact]
    public async Task Should_Throw_When_MongoshExitsWithoutOutput()
    {
        var process = A.Fake<IProcessSession>();
        A.CallTo(() => process.StandardOutput).Returns(new StreamReader(new MemoryStream()));
        A.CallTo(() => process.StandardInput).Returns(new StreamWriter(new MemoryStream(), new UTF8Encoding(false)));
        A.CallTo(() => process.HasExited).Returns(true);
        var session = new MongoShellSession(process);

        var act = () => session.EvaluateAsync("db.items.find()", CancellationToken.None);

        await act.Should().ThrowAsync<QueryExecutionException>();
    }

    [Fact]
    public async Task Should_ReturnStderrMessage_When_MongoshFailsToStart()
    {
        const string stderr = "dyld[50760]: Library not loaded: libicui18n.74.dylib\n";
        var process = A.Fake<IProcessSession>();
        A.CallTo(() => process.StandardOutput).Returns(new StreamReader(new MemoryStream()));
        A.CallTo(() => process.StandardError).Returns(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stderr))));
        A.CallTo(() => process.StandardInput).Returns(new StreamWriter(new MemoryStream(), new UTF8Encoding(false)));
        A.CallTo(() => process.HasExited).Returns(true);
        var session = new MongoShellSession(process);

        var act = () => session.EvaluateAsync("db.items.find()", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<QueryExecutionException>();
        exception.Which.Message.Should().Contain("libicui18n.74.dylib");
    }

    private static MongoShellSession CreateSession(string output)
    {
        var process = A.Fake<IProcessSession>();
        A.CallTo(() => process.StandardOutput)
            .Returns(new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(output))));
        A.CallTo(() => process.StandardInput)
            .Returns(new StreamWriter(new MemoryStream(), new UTF8Encoding(false)));

        return new MongoShellSession(process);
    }
}
