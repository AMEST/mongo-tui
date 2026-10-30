using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class QueryWorkspacePaginationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    public async Task Should_CommitBrowsePageWithResults_WhenPaginationSucceeds(int page, int direction)
    {
        await using var fixture = new WorkspaceFixture();
        var tab = await BrowseAsync(fixture, page);
        var previous = tab.Result;
        var response = new TaskCompletionSource<QueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(response.Task);
        var result = Result((page + direction) * QueryWorkspace.PageSize, 20);

        var pending = fixture.Workspace.PageAsync(tab, direction);
        var loading = (tab.Page, tab.Result, tab.HasNext, tab.Busy);
        response.SetResult(result);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        loading.Page.Should().Be(page);
        loading.Result.Should().BeSameAs(previous);
        loading.HasNext.Should().BeTrue();
        loading.Busy.Should().BeTrue();
        tab.Page.Should().Be(page + direction);
        tab.Result!.Documents.Should().Equal(result.Documents);
        tab.HasNext.Should().BeFalse();
        tab.Busy.Should().BeFalse();
        tab.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    public async Task Should_KeepDisplayedBrowsePage_WhenPaginationIsCancelled(int page, int direction)
    {
        await using var fixture = new WorkspaceFixture();
        var tab = await BrowseAsync(fixture, page);
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

        var pending = fixture.Workspace.PageAsync(tab, direction);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Workspace.Cancel(tab);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        tab.Page.Should().Be(page);
        tab.Result.Should().BeSameAs(previous);
        tab.EditableCollection.Should().BeSameAs(editable);
        tab.HasNext.Should().BeTrue();
        tab.Busy.Should().BeFalse();
        tab.Error.Should().Be(fixture.Text[LocalizationKeys.Query.Cancelled]);
    }

    [Fact]
    public async Task Should_RetrySameBrowsePage_WhenPreviousRequestWasCancelled()
    {
        await using var fixture = new WorkspaceFixture();
        var tab = await BrowseAsync(fixture, 0);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Throws(new OperationCanceledException());
        await fixture.Workspace.PageAsync(tab, 1);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(Result(call.GetArgument<QueryExecutionOptions>(3)!.Skip)));

        await fixture.Workspace.PageAsync(tab, 1);

        tab.Page.Should().Be(1);
        tab.Result!.Documents.Select(document => document["_id"].AsInt32).Should().Equal(Enumerable.Range(50, 50));
        tab.Error.Should().BeNull();
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, "{}",
            A<QueryExecutionOptions>.That.Matches(options => options.Skip == 50 && options.Limit == 51),
            A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, "{}",
            A<QueryExecutionOptions>.That.Matches(options => options.Skip == 100),
            A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, -1, false)]
    [InlineData(0, 1, true)]
    [InlineData(1, -1, true)]
    public async Task Should_KeepDisplayedBrowsePage_WhenPaginationFails(int page, int direction, bool timeout)
    {
        await using var fixture = new WorkspaceFixture();
        var tab = await BrowseAsync(fixture, page);
        var previous = tab.Result;
        var editable = tab.EditableCollection;
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Throws(timeout ? new QueryTimeoutException(TimeSpan.FromSeconds(90), new OperationCanceledException())
                : new InvalidOperationException("Query failed."));

        await fixture.Workspace.PageAsync(tab, direction);

        tab.Page.Should().Be(page);
        tab.Result.Should().BeSameAs(previous);
        tab.EditableCollection.Should().BeSameAs(editable);
        tab.HasNext.Should().BeTrue();
        tab.Busy.Should().BeFalse();
        tab.Error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -1)]
    public async Task Should_PaginateLoadedScriptResults_WhenPageChanges(int page, int direction)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).Returns(Result(0, 75));
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", "db.orders.find({}).toArray()");
        await fixture.Workspace.ExecuteAsync(tab);
        if (page > 0) await fixture.Workspace.PageAsync(tab, page);
        var previous = tab.Result;

        await fixture.Workspace.PageAsync(tab, direction);

        tab.Page.Should().Be(page + direction);
        tab.Result.Should().BeSameAs(previous);
        tab.HasNext.Should().Be(tab.Page == 0);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private async Task<WebTab> BrowseAsync(WorkspaceFixture fixture, int page)
    {
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(Result(call.GetArgument<QueryExecutionOptions>(3)!.Skip)));
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders");
        tab.Page = page;
        await fixture.Workspace.ExecuteAsync(tab);
        return tab;
    }

    private static QueryResult Result(int skip, int count = 51) => new()
    {
        Kind = QueryResultKind.Documents,
        Documents = Enumerable.Range(skip, count).Select(id => new BsonDocument("_id", id)).ToArray(),
    };
}
