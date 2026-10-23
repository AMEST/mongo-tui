using System.Reflection;
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
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Bootstrap;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace MongoTui.TUI.Tests.Views;

public sealed class MainWindowQueryMenuTests : IDisposable
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");

    private readonly ServiceProvider _provider;
    private readonly Terminal.Gui.App.IApplication _app;
    private readonly MainWindow _window;
    private readonly ILocalizer _localizer;
    private readonly IMongoGateway _gateway;

    public MainWindowQueryMenuTests()
    {
        _gateway = A.Fake<IMongoGateway>();
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<string>>(["c1"]));

        var services = new ServiceCollection();
        services.AddSingleton(_gateway);
        services.AddSingleton(A.Fake<IMongoTransferGateway>());
        services.AddSingleton(A.Fake<IMongoUserGateway>());
        services.AddSingleton(A.Fake<IConnectionRepository>());
        services.AddSingleton(A.Fake<ICryptoService>());
        services.AddSingleton(A.Fake<IQueryHistoryRepository>());
        services.AddSingleton(A.Fake<IAiAssistant>());
        services.AddSingleton(A.Fake<ISettingsStore>());
        services.AddSingleton(A.Fake<IStorageTransaction>());
        services.AddSingleton(A.Fake<ISshTunnelFactory>());
        services.AddSingleton(A.Fake<ITwoFactorPrompt>());
        services.AddSingleton(A.Fake<IMongoShellSessionFactory>());
        services.AddApplication();
        services.AddTui();
        _provider = services.BuildServiceProvider();
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

        await InvokeContextAction(row, "Tree.NewQuery");

        var tabs = (QueryTabsView)Field(_window, "_queryTabs");
        tabs.TabCount.Should().Be(1);
        tabs.ActiveTab!.Database.Should().Be("app");
        tabs.ActiveTab.Collection.Should().BeNull();
        tabs.ActiveTab.Script.Should().Be("// write your query below this line");
    }

    [Fact]
    public async Task Should_OpenCollectionTab_When_NewQueryChosenOnCollection()
    {
        var connection = AddConnection("app", new DatabaseInfo("app", false));
        await _tree.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        Invoke(windowRebuild: true);
        var row = Rows().Single(row => row.Kind == TreeRowKind.Collection && row.Collection == "c1");

        await InvokeContextAction(row, "Tree.NewQuery");

        var tabs = (QueryTabsView)Field(_window, "_queryTabs");
        tabs.TabCount.Should().Be(1);
        tabs.ActiveTab!.Database.Should().Be("app");
        tabs.ActiveTab.Collection.Should().Be("c1");
        tabs.ActiveTab.Script.Should().Be("db.getCollection(\"c1\").find({}).limit(50).toArray()");
        tabs.ActiveTab.Script.Should().NotContain("getSiblingDB");
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

    private List<(string Title, Func<Task> Handler)> ContextActions(TreeRow row) =>
        (List<(string Title, Func<Task> Handler)>)_window.GetType()
            .GetMethod("BuildContextActions", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_window, [row])!;

    private ActiveConnection AddConnection(string database, DatabaseInfo info)
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        var connection = new ActiveConnection(profile, Target, [info]);
        _tree.AddOrReplace(connection);
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
        var actions = (List<(string Title, Func<Task> Handler)>)_window.GetType()
            .GetMethod("BuildContextActions", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_window, [row])!;

        var action = actions.Single(action => action.Title == _localizer.Get(localizationKey));
        await action.Handler();
    }

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
}
