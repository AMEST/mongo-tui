using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Web.Tests;

public sealed class QueryWorkspaceTests
{
    [Theory]
    [InlineData("admin", "system.version")]
    [InlineData("config", "system.sessions")]
    [InlineData("local", "startup_log")]
    public async Task Should_ReadExistingSystemCollection_WhenBrowsing(string database, string name)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var result = new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] };
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(result);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, database, name);

        await fixture.Workspace.ExecuteAsync(tab);

        tab.Error.Should().BeNull();
        tab.Result!.Documents.Should().ContainSingle();
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._,
            A<CollectionRef>.That.Matches(collection => collection.Database.Value == database && collection.Collection.Value == name),
            "{}", A<QueryExecutionOptions>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_PaginateResults_WhenBrowsingCollection()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var result = new QueryResult { Kind = QueryResultKind.Documents,
            Documents = Enumerable.Range(0, 51).Select(i => new BsonDocument("_id", i)).ToArray() };
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(result);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders");

        await fixture.Workspace.ExecuteAsync(tab);
        await fixture.Workspace.PageAsync(tab, 1);

        tab.Result!.Documents.Should().HaveCount(50);
        tab.HasNext.Should().BeTrue();
        tab.EditableCollection!.Collection.Value.Should().Be("orders");
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, "{}",
            A<QueryExecutionOptions>.That.Matches(o => o.Skip == 50 && o.Limit == 51),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_CancelAndReleaseTab_WhenClosedDuringQuery()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(async (string _, string? _, CancellationToken token) =>
            { await Task.Delay(Timeout.Infinite, token); return QueryResult.Empty(QueryResultKind.Documents); });
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", null);
        var running = fixture.Workspace.ExecuteAsync(tab);

        await fixture.Workspace.CloseAsync(tab);

        await running.WaitAsync(TimeSpan.FromSeconds(2));
        fixture.Workspace.Tabs.Should().BeEmpty();
        tab.Busy.Should().BeFalse();
        tab.Result.Should().BeNull();
        A.CallTo(() => fixture.Shell.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("db.orders.find({}).limit(2).toArray()", true)]
    [InlineData("db.orders.find({}, {name: 1}).toArray()", false)]
    [InlineData("db.orders.aggregate([{ $project: {name: 1} }]).toArray()", false)]
    public async Task Should_EnableWritesOnlyForFullDocuments_WhenScriptReturnsResults(string script, bool editable)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] });
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);

        await fixture.Workspace.ExecuteAsync(tab);

        (tab.EditableCollection is not null).Should().Be(editable);
    }

    [Theory]
    [InlineData("db.orders.find({}).toArray()", "orders")]
    [InlineData("db.orders.find({}, {name: 1}).toArray()", null)]
    [InlineData("db.orders.aggregate([]).toArray()", null)]
    public async Task Should_KeepExecutedSource_WhenEditorChangesDuringQuery(string script, string? collection)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<QueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._))
            .Invokes(() => started.TrySetResult()).Returns(response.Task);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);

        var running = fixture.Workspace.ExecuteAsync(tab);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        tab.Script = "db.customers.find({}).toArray()";
        response.SetResult(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] });
        await running.WaitAsync(TimeSpan.FromSeconds(5));

        (tab.EditableCollection?.Collection.Value).Should().Be(collection);
        tab.Result!.Documents.Should().ContainSingle();
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ExecuteSubmittedScript_WhenEditorChangesWhileQueued()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string script = "db.orders.find({}).toArray()";
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, "demo", A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] });
        await fixture.Limiter.EnterAsync(CancellationToken.None);
        await fixture.Limiter.EnterAsync(CancellationToken.None);

        var running = fixture.Workspace.ExecuteAsync(tab);
        tab.Script = "db.customers.find({}).toArray()";
        fixture.Limiter.Exit();
        try { await running.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { fixture.Limiter.Exit(); }

        tab.EditableCollection!.Collection.Value.Should().Be("orders");
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_RejectMoreTabs_WhenCapacityReached()
    {
        await using var fixture = new WorkspaceFixture();
        for (var i = 0; i < 12; i++) fixture.Workspace.Open(fixture.Profile.Id, "demo", null);

        var act = () => fixture.Workspace.Open(fixture.Profile.Id, "demo", null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public async Task Should_KeepDisplayedDocumentsAndActions_WhenRefreshIsPendingOrCancelled()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] });
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders");
        await fixture.Workspace.ExecuteAsync(tab);
        var previous = tab.Result;
        var editable = tab.EditableCollection;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).ReturnsLazily(async call =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, call.GetArgument<CancellationToken>(4));
                return QueryResult.Empty(QueryResultKind.Documents);
            });

        var pending = fixture.Workspace.ExecuteAsync(tab);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        tab.Result.Should().BeSameAs(previous);
        tab.EditableCollection.Should().BeSameAs(editable);
        fixture.Workspace.Cancel(tab);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        tab.Result.Should().BeSameAs(previous);
        tab.EditableCollection.Should().BeSameAs(editable);
        tab.Busy.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_ShowSharedTimeout_WhenBrowseOrScriptExceedsDeadline(bool browse)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage("ru");
        A.CallTo(() => fixture.Settings.GetAsync("query.timeoutSeconds", A<CancellationToken>._)).Returns("1");
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .ReturnsLazily(async (string _, string? _, CancellationToken token) =>
            { await Task.Delay(Timeout.InfiniteTimeSpan, token); return QueryResult.Empty(QueryResultKind.Documents); });
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._))
            .ReturnsLazily(async (MongoConnectionTarget _, CollectionRef _, string _, QueryExecutionOptions _, CancellationToken token) =>
            { await Task.Delay(Timeout.InfiniteTimeSpan, token); return QueryResult.Empty(QueryResultKind.Documents); });
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", browse ? null : "db.orders.find()");

        await fixture.Workspace.ExecuteAsync(tab).WaitAsync(TimeSpan.FromSeconds(5));

        tab.Error.Should().Be("Превышено время выполнения запроса (1 с).");
        tab.Busy.Should().BeFalse();
    }
}
