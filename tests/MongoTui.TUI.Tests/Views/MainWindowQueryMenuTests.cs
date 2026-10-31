using MongoTui.Application.Localization;
using System.Reflection;
using MongoDB.Bson;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.DependencyInjection;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.Bootstrap;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class MainWindowQueryMenuTests : IDisposable
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");

    private readonly ServiceProvider _provider;
    private readonly Terminal.Gui.App.IApplication _app;
    private readonly MainWindow _window;
    private readonly ILocalizer _localizer;
    private readonly IMongoGateway _gateway;
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly IMongoShellSessionFactory _sessions = A.Fake<IMongoShellSessionFactory>();
    private readonly IMongoShellSession _session = A.Fake<IMongoShellSession>();

    public MainWindowQueryMenuTests()
    {
        A.CallTo(() => _sessions.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(_session);
        A.CallTo(() => _session.IsRunning).Returns(true);
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("loaded", "automatically")] });
        _gateway = A.Fake<IMongoGateway>();
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<string>>(["c1"]));

        var services = new ServiceCollection();
        services.AddSingleton(_gateway);
        services.AddSingleton(A.Fake<IMongoTransferGateway>());
        services.AddSingleton(A.Fake<IMongoUserGateway>());
        services.AddSingleton(_repository);
        services.AddSingleton(A.Fake<ICryptoService>());
        services.AddSingleton(A.Fake<IQueryHistoryRepository>());
        services.AddSingleton(A.Fake<IAiAssistant>());
        services.AddSingleton(A.Fake<ISettingsStore>());
        services.AddSingleton(A.Fake<IStorageTransaction>());
        services.AddSingleton(A.Fake<ISshTunnelFactory>());
        services.AddSingleton(A.Fake<ITwoFactorPrompt>());
        services.AddSingleton(_sessions);
        services.AddLogging();
        services.AddSingleton(A.Fake<MongoTui.Application.Abstractions.Diagnostics.ILoggingSettings>());
        services.AddApplication();
        services.AddTui();
        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<SecretKeyService>().SetKek(new byte[32]);
        _localizer = _provider.GetRequiredService<ILocalizer>();

        _window = _provider.GetRequiredService<MainWindow>();
        _app = Terminal.Gui.App.Application.Create();
        _app.Init(DriverRegistry.Names.ANSI);
        _window.Application = _app;
        _window.Compose();
        _ = _app.Begin(_window);
        _window.SetFocus();
    }

    public void Dispose()
    {
        _app.Dispose();
        _ = _provider.DisposeAsync().AsTask().Wait(2000);
    }

    [Fact]
    public async Task Should_OpenDatabaseBoundTab_When_NewQueryChosenOnDatabase()
    {
        _ = AddConnection("app", new DatabaseInfo("app", false));
        var row = Rows().Single(row => row.Kind == TreeRowKind.Database && row.Database == "app");

        await InvokeContextAction(row, LocalizationKeys.Tree.NewQuery);

        var tabs = (QueryTabsView)Field(_window, "_queryTabs");
        tabs.TabCount.Should().Be(1);
        tabs.ActiveTab!.Database.Should().Be("app");
        tabs.ActiveTab.ConnectionName.Should().Be("local");
        tabs.ActiveTab.Collection.Should().BeNull();
        tabs.ActiveTab.Script.Should().Be("// write your query below this line");
        A.CallTo(() => _sessions.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_OpenCollectionTab_When_NewQueryChosenOnCollection()
    {
        var connection = AddConnection("app", new DatabaseInfo("app", false));
        await _tree.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        Invoke(windowRebuild: true);
        var row = Rows().Single(row => row.Kind == TreeRowKind.Collection && row.Collection == "c1");

        await InvokeContextAction(row, LocalizationKeys.Tree.NewQuery);

        var tabs = (QueryTabsView)Field(_window, "_queryTabs");
        tabs.TabCount.Should().Be(1);
        tabs.ActiveTab!.Database.Should().Be("app");
        tabs.ActiveTab.ConnectionName.Should().Be("local");
        tabs.ActiveTab.Collection.Should().Be("c1");
        tabs.ActiveTab.Script.Should().Be("db.getCollection(\"c1\").find({}).limit(50).toArray()");
        tabs.ActiveTab.Script.Should().NotContain("getSiblingDB");
        tabs.ActiveResultView!.Text.Should().Contain("automatically");
        A.CallTo(() => _session.EvaluateAsync(tabs.ActiveTab.Script, "app", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("c1", "\"c1\"")]
    [InlineData("x\");db.dropDatabase();db.getCollection(\"x", "\"x\\u0022);db.dropDatabase();db.getCollection(\\u0022x\"")]
    public async Task Should_ExecuteOnlyGeneratedFind_WhenCollectionIsOpened(string collection, string literal)
    {
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, DatabaseName.Create("app"), A<CancellationToken>._))
            .Returns(new[] { collection });
        var connection = AddConnection("app", new DatabaseInfo("app", false));
        await _tree.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        Invoke(windowRebuild: true);
        ((ListView)Field(_window, "_treeList")).SelectedItem = Rows().FindIndex(row => row.Collection == collection);

        await InvokeAsync("OpenTreeItemAsync");

        var tabs = (QueryTabsView)Field(_window, "_queryTabs");
        tabs.ActiveResultView!.Text.Should().Contain("automatically");
        A.CallTo(() => _session.EvaluateAsync($"db.getCollection({literal}).find({{}}).limit(50).toArray()",
            "app", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Should_BindRunQueryToCtrlR_WhenShortcutsAreBuilt()
    {
        var shortcuts = (Dictionary<Key, Action>)Field(_window, "_shortcuts");

        shortcuts.Keys.Should().Contain(new Key(KeyCode.R | KeyCode.CtrlMask));
        shortcuts.Keys.Should().NotContain(new Key(KeyCode.Enter | KeyCode.CtrlMask));
    }

    [Fact]
    public void Should_OfferAdminUsersAction_When_ConnectionNode()
    {
        var connection = AddConnection("app", new DatabaseInfo("app", false));
        var row = Rows().Single(row => row.Kind == TreeRowKind.Connection);

        var actions = ContextActions(row);

        actions.Should().Contain(action => action.Title == _localizer.Get("Tree.ManageUsersAdmin"));
        connection.Profile.Id.Should().NotBe(default);
    }

    [Fact]
    public void Should_OfferUsersAction_When_DatabaseNode()
    {
        AddConnection("app", new DatabaseInfo("app", false));
        var row = Rows().Single(row => row.Kind == TreeRowKind.Database);

        var actions = ContextActions(row);

        actions.Should().Contain(action => action.Title == _localizer.Get("Tree.ManageUsers"));
    }

    private TreeViewModel _tree => (TreeViewModel)Field(_window, "_tree");

    [Fact]
    public void Should_OfferConnectAndProfileActions_WhenSavedConnectionIsInactive()
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "saved", "mongodb://localhost:27017");
        _tree.SetProfiles([profile]);
        Invoke(windowRebuild: true);

        var titles = ContextActions(Rows().Single()).Select(action => action.Title);

        titles.Should().BeEquivalentTo(new[] { LocalizationKeys.Connection.Connect, LocalizationKeys.Connection.Edit, LocalizationKeys.Connection.Remove }
            .Select(_localizer.Get));
    }

    [Fact]
    public void Should_OfferDatabaseAndProfileActions_WhenConnectionIsActive()
    {
        AddConnection("app", new DatabaseInfo("app", false));

        var titles = ContextActions(Rows().First()).Select(action => action.Title);

        titles.Should().BeEquivalentTo(new[] { LocalizationKeys.Tree.CreateDatabase, LocalizationKeys.Tree.Refresh, LocalizationKeys.Tree.ManageUsersAdmin,
            LocalizationKeys.Tree.Disconnect, LocalizationKeys.Connection.Edit, LocalizationKeys.Connection.Remove }.Select(_localizer.Get));
    }

    [Fact]
    public async Task Should_ConnectWithoutExpandingAndKeepOtherProfiles_WhenSavedNodeAccepted()
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "saved", "mongodb://localhost:27017");
        var other = ConnectionProfile.Create(ConnectionId.New(), "other", "mongodb://localhost:27017");
        _tree.SetProfiles([profile, other]);
        A.CallTo(() => _repository.GetAsync(profile.Id, A<CancellationToken>._)).Returns(new StoredConnection(profile, null));
        A.CallTo(() => _gateway.ListDatabasesAsync(Target, A<CancellationToken>._)).Returns(new[] { new DatabaseInfo("app", false) });
        Invoke(windowRebuild: true);
        ((ListView)Field(_window, "_treeList")).SelectedItem = Rows().FindIndex(row => row.Profile.Id == profile.Id);

        await InvokeAsync("OpenTreeItemAsync");

        _tree.Count.Should().Be(1);
        _tree.IsExpanded(profile.Id).Should().BeFalse();
        Rows().Select(row => row.Profile.Name).Should().Equal("saved", "other");
    }

    [Fact]
    public async Task Should_KeepSavedNodeAndCloseTabs_WhenDisconnectedFromContextMenu()
    {
        var connection = AddConnection("app", new DatabaseInfo("app", false));
        _tree.SetProfiles([connection.Profile]);
        var tabs = (QueryTabsView)Field(_window, "_queryTabs");
        tabs.AddTab(connection.Profile.Id, Target, "app", null);

        await InvokeContextAction(Rows().First(), LocalizationKeys.Tree.Disconnect);

        Rows().Should().ContainSingle().Which.Owner.Should().BeNull();
        tabs.TabCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_PreserveExpandedNodesAndSelection_WhenRefreshed()
    {
        var connection = AddConnection("app", new DatabaseInfo("app", false));
        A.CallTo(() => _repository.GetAllAsync(A<CancellationToken>._)).Returns(new[] { new StoredConnection(connection.Profile, null) });
        A.CallTo(() => _gateway.ListDatabasesAsync(Target, A<CancellationToken>._)).Returns(new[] { new DatabaseInfo("app", false) });
        await _tree.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        Invoke(windowRebuild: true);
        ((ListView)Field(_window, "_treeList")).SelectedItem = Rows().FindIndex(row => row.Collection == "c1");

        await InvokeAsync("RefreshActiveAsync");

        _tree.IsExpanded(connection.Profile.Id).Should().BeTrue();
        _tree.IsExpanded(connection.Profile.Id, "app").Should().BeTrue();
        Rows()[((ListView)Field(_window, "_treeList")).SelectedItem!.Value].Collection.Should().Be("c1");
    }

    private Task InvokeAsync(string name) => (Task)_window.GetType()
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_window, null)!;

    private ActiveConnection AddConnection(string database, DatabaseInfo info)
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        var connection = new ActiveConnection(profile, Target, [info]);
        _tree.AddOrReplace(connection);
        _tree.ExpandConnection(profile.Id);
        Invoke(windowRebuild: true);
        return connection;
    }

    private List<TreeRow> Rows() => (List<TreeRow>)Field(_window, "_rows");

    private void Invoke(bool windowRebuild)
    {
        if (windowRebuild)
        {
            _window.GetType()
                .GetMethod("RebuildTree", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(_window, [-1]);
        }
    }

    private async Task InvokeContextAction(TreeRow row, string localizationKey)
    {
        var actions = ContextActions(row);

        var action = actions.Single(action => action.Title == _localizer.Get(localizationKey));
        await action.Handler();
    }

    private List<(string Title, Func<Task> Handler)> ContextActions(TreeRow row) =>
        (List<(string Title, Func<Task> Handler)>)_window.GetType()
            .GetMethod("BuildContextActions", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_window, [row])!;

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
}
