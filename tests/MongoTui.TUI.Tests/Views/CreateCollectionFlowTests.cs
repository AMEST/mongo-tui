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
using MongoTui.Domain.Collections;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Bootstrap;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class CreateCollectionFlowTests : IDisposable
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");

    private readonly ServiceProvider _provider;
    private readonly Terminal.Gui.App.IApplication _app;
    private readonly MainWindow _window;
    private readonly ILocalizer _localizer;

    public CreateCollectionFlowTests()
    {
        var gateway = A.Fake<IMongoGateway>();
        A.CallTo(() => gateway.ListCollectionsAsync(Target, DatabaseName.Create("draft"), A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<string>>(["c1"]));
        A.CallTo(() => gateway.CreateCollectionAsync(Target, A<CollectionRef>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(gateway);
        services.AddSingleton(A.Fake<IMongoTransferGateway>());
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
    public async Task Tree_Should_RenderFirstCollection_When_CreatedAfterNewDatabase()
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        var connection = new ActiveConnection(profile, Target, [new DatabaseInfo("admin", false)]);
        var tree = (TreeViewModel)Field(_window, "_tree");
        tree.AddOrReplace(connection);
        RebuildTree();

        AcceptNextDialog("draft");
        await InvokeAsync(_window, "CreateDatabaseAsync", connection);
        Rows().Should().Contain(row => row.Kind == TreeRowKind.Database && row.Database == "draft");

        AcceptNextDialog("c1");
        await InvokeAsync(_window, "CreateCollectionAsync", connection, "draft");

        Rows().Should().Contain(row =>
            row.Kind == TreeRowKind.Collection && row.Database == "draft" && row.Collection == "c1");
        Rows().Should().NotContain(row => row.Kind == TreeRowKind.Database
            && row.Text.Contains(_localizer.Get("Database.Empty")));
    }

    private List<TreeRow> Rows() => (List<TreeRow>)Field(_window, "_rows");

    private void RebuildTree() =>
        _window.GetType().GetMethod("RebuildTree", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_window, [-1]);

    private static Task InvokeAsync(object target, string name, params object[] args) =>
        (Task)target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args)!;

    private void AcceptNextDialog(string name)
    {
        _app.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            if (_app.TopRunnableView is CreateNameDialog dialog)
            {
                dialog.SubViews.OfType<TextField>().Single().Text = name;
                dialog.Buttons[0].InvokeCommand(Command.Accept);
                return false;
            }

            return true;
        });
    }

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
}
