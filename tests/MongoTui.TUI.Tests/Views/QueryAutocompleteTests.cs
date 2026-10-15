using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // The query workspace uses Terminal.Gui's TextView editor.

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryAutocompleteTests
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly MongoConnectionTarget _target = new("mongodb://localhost:27017");

    [Fact]
    public async Task Should_InsertSelectedCollection_WhenCompletionAccepted()
    {
        using var tabs = CreateTabs("db.us");
        await tabs.CompleteActiveAsync(CancellationToken.None);

        var accepted = tabs.ActiveEditorView!.Autocomplete.ProcessKey(Key.Enter);

        accepted.Should().BeTrue();
        tabs.ActiveEditorView.Text.Should().Be("db.users");
        tabs.ActiveTab!.Script.Should().Be("db.users");
    }

    [Fact]
    public async Task Should_ReplaceEntireName_WhenCaretIsInsideToken()
    {
        using var tabs = CreateTabs("db.uses.find({})", 5);
        await tabs.CompleteActiveAsync(CancellationToken.None);

        tabs.ActiveEditorView!.Autocomplete.ProcessKey(Key.Enter);

        tabs.ActiveEditorView.Text.Should().Be("db.users.find({})");
    }

    [Fact]
    public async Task Should_ListDatabases_WhenCompletingSiblingDatabase()
    {
        using var tabs = CreateTabs("db.getSiblingDB(\"ap", database: null);

        var status = await tabs.CompleteActiveAsync(CancellationToken.None);

        status.Should().Be("Autocomplete.Hint");
        tabs.ActiveEditorView!.Autocomplete.Suggestions.Select(item => item.Title).Should().Equal("app");
    }

    [Theory]
    [InlineData("db.users.find({ na", "db.users.find({ \"name\"")]
    [InlineData("db.users.find({\n  na", "db.users.find({\n  \"name\"")]
    [InlineData("db.users.find({ 'profile.na", "db.users.find({ 'profile.name")]
    public async Task Should_InsertField_WhenCompletionAccepted(string script, string expected)
    {
        using var tabs = CreateTabs(script);
        await tabs.CompleteActiveAsync(CancellationToken.None);

        tabs.ActiveEditorView!.Autocomplete.ProcessKey(Key.Enter);

        tabs.ActiveEditorView.Text.Should().Be(expected);
        A.CallTo(() => _gateway.FindAsync(_target,
            new CollectionRef(DatabaseName.Create("app"), CollectionName.Create("users")),
            "{}", A<QueryExecutionOptions>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_KeepText_WhenCompletionDismissed()
    {
        using var tabs = CreateTabs("db.us");
        await tabs.CompleteActiveAsync(CancellationToken.None);

        tabs.ActiveEditorView!.Autocomplete.ProcessKey(Key.Esc);

        tabs.ActiveEditorView.Text.Should().Be("db.us");
        tabs.ActiveEditorView.Autocomplete.Visible.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ShowFailure_WhenGatewayThrows()
    {
        using var tabs = CreateTabs("db.us");
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("unavailable"));

        var status = await tabs.CompleteActiveAsync(CancellationToken.None);

        status.Should().Be("Autocomplete.Failed");
        tabs.ActiveEditorView!.Autocomplete.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_DiscardSuggestions_WhenTextChangesDuringLoad()
    {
        using var tabs = CreateTabs("db.us");
        var pending = new TaskCompletionSource<IReadOnlyList<string>>();
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(pending.Task);

        var completion = tabs.CompleteActiveAsync(CancellationToken.None);
        tabs.ActiveEditorView!.Text = "db.ord";
        pending.SetResult(["users"]);
        var status = await completion;

        status.Should().BeNull();
        tabs.ActiveEditorView.Autocomplete.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_CancelRequest_WhenTabIsClosed()
    {
        using var tabs = CreateTabs("db.us");
        var pending = new TaskCompletionSource<IReadOnlyList<string>>();
        var token = CancellationToken.None;
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, DatabaseName _, CancellationToken cancellation) =>
            {
                token = cancellation;
                return pending.Task;
            });

        var completion = tabs.CompleteActiveAsync(CancellationToken.None);
        await tabs.CloseActiveAsync();
        pending.SetResult(["users"]);
        var status = await completion;

        token.IsCancellationRequested.Should().BeTrue();
        status.Should().BeNull();
    }

    [Fact]
    public async Task Should_FilterVisiblePopup_WhenUserContinuesTyping()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var runnable = new Runnable<bool>();
        var tabs = CreateTabs("db.u");
        tabs.Width = 60;
        tabs.Height = 15;
        runnable.Add(tabs);
        app.Begin(runnable);
        tabs.ActiveEditorView!.SetFocus();
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "updates", "users" });
        await tabs.CompleteActiveAsync(CancellationToken.None);
        app.LayoutAndDraw();

        tabs.ActiveEditorView.NewKeyDownEvent(new Key('s'));
        app.LayoutAndDraw();

        tabs.ActiveEditorView.Text.Should().Be("db.us");
        tabs.ActiveEditorView.Autocomplete.Visible.Should().BeTrue();
        tabs.ActiveEditorView.Autocomplete.Suggestions.Select(item => item.Title).Should().Equal("users");
    }

    [Fact]
    public async Task Should_DiscardSuggestions_WhenEscapePressedDuringLoad()
    {
        using var tabs = CreateTabs("db.us");
        var pending = new TaskCompletionSource<IReadOnlyList<string>>();
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(pending.Task);

        var completion = tabs.CompleteActiveAsync(CancellationToken.None);
        tabs.ActiveEditorView!.NewKeyDownEvent(Key.Esc);
        pending.SetResult(["users"]);
        var status = await completion;

        status.Should().BeNull();
        tabs.ActiveEditorView.Autocomplete.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_InsertQuotedCollectionAccess_WhenNameContainsDash()
    {
        using var tabs = CreateTabs("db.ord");
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "order-items" });
        await tabs.CompleteActiveAsync(CancellationToken.None);

        tabs.ActiveEditorView!.Autocomplete.ProcessKey(Key.Enter);

        tabs.ActiveEditorView.Text.Should().Be("db.getCollection(\"order-items\")");
    }

    [Fact]
    public async Task Should_DiscardSuggestions_WhenUserSwitchesTab()
    {
        using var tabs = CreateTabs("db.us");
        var editor = tabs.ActiveEditorView!;
        var pending = new TaskCompletionSource<IReadOnlyList<string>>();
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(pending.Task);

        var completion = tabs.CompleteActiveAsync(CancellationToken.None);
        tabs.AddTab(ConnectionId.New(), _target, "app", script: "db.ord");
        pending.SetResult(["users"]);
        var status = await completion;

        status.Should().BeNull();
        editor.Autocomplete.Suggestions.Should().BeEmpty();
        tabs.ActiveEditorView!.Autocomplete.Suggestions.Should().BeEmpty();
    }

    private QueryTabsView CreateTabs(string script, int? column = null, string? database = "app")
    {
        A.CallTo(() => _gateway.ListCollectionsAsync(_target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "orders", "users" });
        A.CallTo(() => _gateway.ListDatabasesAsync(_target, A<CancellationToken>._))
            .Returns(new List<DatabaseInfo> { new("admin", false), new("app", false) });
        A.CallTo(() => _gateway.FindAsync(_target, A<CollectionRef>._, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult
            {
                Kind = QueryResultKind.Documents,
                Documents = [BsonDocument.Parse("{name: 'a', profile: {name: 'b'}}")],
            });
        var service = new QueryService(A.Fake<IMongoShellSessionFactory>(), A.Fake<IQueryHistoryRepository>());
        var tabs = new QueryTabsView(service, A.Fake<ILocalizer>(), new AutocompleteService(_gateway));
        tabs.AddTab(ConnectionId.New(), _target, database, "orders", script);
        tabs.EndInit();
        tabs.ActiveEditorView!.MoveEnd();
        if (column is { } position) tabs.ActiveEditorView.InsertionPoint = new System.Drawing.Point(position, 0);
        tabs.ActiveEditorView.SetFocus();
        return tabs;
    }
}
