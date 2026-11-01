using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Queries;

namespace MongoTui.Web.Tests;

public sealed class QueryWorkspaceRefreshTests
{
    [Fact]
    public async Task Should_ReplayOnlyExecutedFragment_WhenRefreshingSelectionResult()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string selected = "db.customers.find({}).toArray()";
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", "db.orders.deleteMany({});\n" + selected);
        A.CallTo(() => fixture.Shell.EvaluateAsync(selected, "demo", A<CancellationToken>._)).Returns(Result());
        await fixture.Workspace.ExecuteScriptAsync(tab, selected);
        tab.Script = "db.customers.deleteMany({})";

        await fixture.Workspace.RefreshAsync(tab);

        tab.Script.Should().Be("db.customers.deleteMany({})");
        tab.EditableCollection!.Collection.Value.Should().Be("customers");
        A.CallTo(() => fixture.Shell.EvaluateAsync(selected, "demo", A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>.That.Contains("deleteMany"), A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_RefreshSubmittedSnapshot_WhenEditorChangesDuringExecution()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string script = "db.orders.find({}).toArray()";
        const string draft = "db.orders.deleteMany({})";
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._))
            .Invokes(() => tab.Script = draft).Returns(Result());
        await fixture.Workspace.ExecuteAsync(tab);

        await fixture.Workspace.RefreshAsync(tab);

        tab.Script.Should().Be(draft);
        tab.EditableCollection!.Collection.Value.Should().Be("orders");
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(draft, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_KeepDisplayedSnapshot_WhenNextExecutionFailsOrIsCancelled(bool cancelled)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string script = "db.orders.find({}).toArray()";
        const string failedScript = "db.customers.find({}).toArray()";
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._)).Returns(Result());
        A.CallTo(() => fixture.Shell.EvaluateAsync(failedScript, "demo", A<CancellationToken>._))
            .Throws(cancelled ? new OperationCanceledException() : new InvalidOperationException("Query failed."));
        await fixture.Workspace.ExecuteAsync(tab);
        tab.Script = failedScript;
        await fixture.Workspace.ExecuteAsync(tab);
        var previous = tab.Result;

        await fixture.Workspace.RefreshAsync(tab);

        tab.Error.Should().BeNull();
        tab.Script.Should().Be(failedScript);
        tab.Result!.Documents.Should().Equal(previous!.Documents);
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(failedScript, "demo", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_UseLatestSuccessfulSnapshot_WhenAnotherTabIsSelected()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string firstScript = "db.orders.find({}).toArray()";
        const string latestScript = "db.orders.find({status: 'paid'}).toArray()";
        const string otherScript = "db.customers.find({}).toArray()";
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).Returns(Result());
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", firstScript);
        await fixture.Workspace.ExecuteAsync(tab);
        tab.Script = latestScript;
        await fixture.Workspace.ExecuteAsync(tab);
        var other = fixture.Workspace.Open(fixture.Profile.Id, "demo", "customers", otherScript);
        await fixture.Workspace.ExecuteAsync(other);
        tab.Script = "db.orders.deleteMany({})";

        await fixture.Workspace.RefreshAsync(tab);

        fixture.Workspace.Selected.Should().BeSameAs(other);
        A.CallTo(() => fixture.Shell.EvaluateAsync(firstScript, "demo", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(latestScript, "demo", A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(otherScript, "demo", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(tab.Script, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_RefreshBrowseFilterAndPageSnapshot_WhenFilterDraftChanges()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string filter = "{\"status\":\"paid\"}";
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(Result());
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders");
        tab.Filter = filter;
        await fixture.Workspace.ExecuteAsync(tab, 2);
        tab.Filter = "{\"status\":\"draft\"}";

        await fixture.Workspace.RefreshAsync(tab);

        tab.Filter.Should().Be("{\"status\":\"draft\"}");
        tab.Page.Should().Be(2);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, filter,
            A<QueryExecutionOptions>.That.Matches(options => options.Skip == 100 && options.Limit == 51),
            A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ShouldNot_ExecuteDraft_WhenTabHasNoExecutedResults()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", "db.orders.deleteMany({})");

        await fixture.Workspace.RefreshAsync(tab);

        tab.Result.Should().BeNull();
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static QueryResult Result() => new() { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] };
}
