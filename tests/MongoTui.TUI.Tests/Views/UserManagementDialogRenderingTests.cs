using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.TUI.Localization;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class UserManagementDialogRenderingTests
{
    private readonly IMongoUserGateway _gateway = A.Fake<IMongoUserGateway>();
    private readonly TaskCompletionSource<IReadOnlyList<MongoUser>> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ResxLocalizer _localizer = new();
    private readonly ActiveConnection _connection;

    public UserManagementDialogRenderingTests()
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        _connection = new ActiveConnection(profile, new MongoConnectionTarget(profile.Uri), []);
        A.CallTo(() => _gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MongoUser>>([new("old_reader", "app", [new("read", "app")])]));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RenderNewUsers_WhenRefreshCompletes(string language)
    {
        _localizer.SetLanguage(language);
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new UserManagementDialog(new UserManagementService(_gateway, new PasswordGenerator()),
            _localizer, app, _connection, DatabaseName.Create("app"), "admin", "admin");
        dialog.Width = Dim.Fill(1);
        await dialog.LoadAsync();
        A.CallTo(() => _gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(_response.Task);
        var observe = ObserveRefresh(app, dialog);
        app.AddTimeout(TimeSpan.Zero, () =>
        {
            app.Run(dialog);
            app.RequestStop(parent);
            return false;
        });

        app.Run(parent);

        var screen = observe();
        screen.Should().Contain("new_writer  [orders: rw]");
        screen.Should().NotContain("old_reader");
        A.CallTo(() => _gateway.ListUsersAsync(_connection.Target, DatabaseName.Create("app"), A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    private Func<string?> ObserveRefresh(IApplication app, UserManagementDialog dialog)
    {
        string? screen = null;
        app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            dialog.Buttons.Single(button => button.Text == _localizer.Get("Users.Refresh")).InvokeCommand(Command.Accept);
            _ = CompleteResponseAsync();
            app.AddTimeout(TimeSpan.FromMilliseconds(150), () =>
            {
                screen = ScreenText(app);
                app.RequestStop(dialog);
                return false;
            });
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); return false; });
        return () => screen;
    }

    private async Task CompleteResponseAsync()
    {
        await Task.Delay(50).ConfigureAwait(false);
        _response.SetResult([new("new_writer", "app", [new("readWrite", "orders")])]);
    }

    private static string ScreenText(IApplication app)
    {
        var cells = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
