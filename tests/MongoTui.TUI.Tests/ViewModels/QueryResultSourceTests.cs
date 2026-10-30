using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Tests.ViewModels;

public sealed class QueryResultSourceTests
{
    private readonly IMongoShellSession _session = A.Fake<IMongoShellSession>();
    private readonly QueryTabViewModel _tab;

    public QueryResultSourceTests()
    {
        A.CallTo(() => _session.IsRunning).Returns(true);
        var factory = A.Fake<IMongoShellSessionFactory>();
        A.CallTo(() => factory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(_session);
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).Returns(Result());
        _tab = new QueryTabViewModel(QueryTestServices.Create(factory, A.Fake<IQueryHistoryRepository>()),
            new MongoTui.TUI.Localization.ResxLocalizer(),
            ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"), "Query", "app", "users");
    }

    [Fact]
    public async Task Should_UseExecutedCollection_When_ScriptTargetsAnotherCollection()
    {
        _tab.Script = "db.getSiblingDB('other').orders.find({})";

        await _tab.ExecuteAsync(CancellationToken.None);

        _tab.ResultCollection!.Database.Value.Should().Be("other");
        _tab.ResultCollection.Collection.Value.Should().Be("orders");
        _tab.Collection.Should().Be("users");
    }

    [Theory]
    [InlineData("db.users.aggregate([])")]
    [InlineData("db.users.find({}, {_id:1})")]
    [InlineData("db.users.find({}); db.orders.find({})")]
    public async Task ShouldNot_EnableDocumentWrites_When_SourceIsAmbiguous(string script)
    {
        _tab.Script = script;

        await _tab.ExecuteAsync(CancellationToken.None);

        _tab.Result.Documents.Should().ContainSingle();
        _tab.ResultCollection.Should().BeNull();
    }

    [Fact]
    public async Task ShouldNot_EnableDocumentWrites_When_ResultHasNoId()
    {
        _tab.Script = "db.users.find({})";
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Scalar, Documents = [new BsonDocument("output", "null")] });

        await _tab.ExecuteAsync(CancellationToken.None);

        _tab.ResultCollection.Should().BeNull();
    }

    [Fact]
    public async Task Should_RetainExecutedSource_When_EditorChangesDuringExecution()
    {
        var pending = new TaskCompletionSource<QueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).Returns(pending.Task);
        _tab.Script = "db.orders.find({})";

        await ExecuteWhileEditingAsync(pending);

        _tab.ResultCollection!.Collection.Value.Should().Be("orders");
    }

    [Fact]
    public async Task Should_ClearPreviousSource_When_NextExecutionFails()
    {
        _tab.Script = "db.users.find({})";
        await _tab.ExecuteAsync(CancellationToken.None);
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("failure"));

        await _tab.ExecuteAsync(CancellationToken.None);

        _tab.ResultCollection.Should().BeNull();
    }

    private async Task ExecuteWhileEditingAsync(TaskCompletionSource<QueryResult> pending)
    {
        var execution = _tab.ExecuteAsync(CancellationToken.None);
        _tab.Script = "db.users.find({})";
        _tab.Database = "another";
        pending.SetResult(Result());
        await execution;
    }

    [Fact]
    public async Task Should_RefreshExecutedSnapshot_WhenDraftAndDatabaseHaveChanged()
    {
        _tab.Script = "db.getSiblingDB('other').orders.find({})";
        await _tab.ExecuteAsync(CancellationToken.None);
        _tab.Script = "db.users.deleteMany({})";
        _tab.Database = "draft";

        await _tab.RefreshAsync(action => { action(); return Task.CompletedTask; }, CancellationToken.None);

        A.CallTo(() => _session.EvaluateAsync("db.getSiblingDB('other').orders.find({})", "app", A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
        A.CallTo(() => _session.EvaluateAsync("db.users.deleteMany({})", A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        _tab.Script.Should().Be("db.users.deleteMany({})");
        _tab.Database.Should().Be("draft");
        _tab.ResultCollection!.Database.Value.Should().Be("other");
    }

    [Theory]
    [InlineData("db.users.deleteMany({})")]
    [InlineData("db.users.find({}); db.users.insertOne({name:'x'})")]
    [InlineData("db.users.aggregate([])")]
    public async Task ShouldNot_ReplayScript_WhenDisplayedSourceIsUnverified(string script)
    {
        _tab.Script = script;
        await _tab.ExecuteAsync(CancellationToken.None);

        await _tab.RefreshAsync(action => { action(); return Task.CompletedTask; }, CancellationToken.None);

        A.CallTo(() => _session.EvaluateAsync(script, "app", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static QueryResult Result() => new() { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] };
}
