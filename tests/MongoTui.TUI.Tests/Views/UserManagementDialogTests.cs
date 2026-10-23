using System.Drawing;
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
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class UserManagementDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();
    private readonly IMongoUserGateway _gateway = A.Fake<IMongoUserGateway>();
    private readonly UserManagementService _users;
    private readonly ActiveConnection _connection;

    public UserManagementDialogTests()
    {
        _localizer.SetLanguage("ru");
        _users = new UserManagementService(_gateway, new PasswordGenerator());
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        _connection = new ActiveConnection(profile, new MongoConnectionTarget(profile.Uri), []);
    }

    [Fact]
    public async Task Should_DisableEditAndDelete_When_ListIsEmpty()
    {
        A.CallTo(() => _gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MongoUser>>([]));
        using var dialog = CreateDialog();

        await dialog.LoadAsync();

        Button(dialog, "Users.Edit").Enabled.Should().BeFalse();
        Button(dialog, "Users.Delete").Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_FormatRolesAndEnableActions_When_UsersExist()
    {
        A.CallTo(() => _gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MongoUser>>(
            [
                new MongoUser("reader", "app", [new MongoRole("read", "orders"), new MongoRole("readAnyDatabase", "admin")]),
            ]));
        using var dialog = CreateDialog();

        await dialog.LoadAsync();
        var rows = dialog.SubViews.OfType<ListView>().Single().Source!.ToList().Cast<string>().ToList();

        rows.Should().ContainSingle().Which.Should().Be("reader  [orders: r] [readAnyDatabase]");
        Button(dialog, "Users.Edit").Enabled.Should().BeTrue();
        Button(dialog, "Users.Delete").Enabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Should_KeepListOpenAndEditSelection_WhenEnterPressed(int userCount)
    {
        var users = Enumerable.Range(0, userCount)
            .Select(index => new MongoUser($"reader{index}", "app", [new MongoRole("read", "app")])).ToList();
        A.CallTo(() => _gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MongoUser>>(users));
        string? editedUsername = null;
        A.CallTo(() => _application.Run(A<IRunnable>._, A<Func<Exception, bool>?>._))
            .Invokes((IRunnable runnable, Func<Exception, bool>? _) => editedUsername =
                ((UserFormDialog)runnable).SubViews.OfType<TextField>().Single(field => !field.Secret).Text);
        using var dialog = CreateDialog();
        await dialog.LoadAsync();
        dialog.BeginInit();
        dialog.EndInit();
        var list = dialog.SubViews.OfType<ListView>().Single();
        list.SelectedItem = userCount > 0 ? userCount - 1 : null;
        list.SetFocus();

        list.NewKeyDownEvent(Key.Enter);

        editedUsername.Should().Be(userCount > 0 ? $"reader{userCount - 1}" : null);
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
        var run = A.CallTo(() => _application.Run(A<IRunnable>._, A<Func<Exception, bool>?>._));
        if (userCount > 0) run.MustHaveHappenedOnceExactly();
        else run.MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en", 0)]
    [InlineData("ru", 0)]
    [InlineData("en", 1)]
    [InlineData("ru", 1)]
    [InlineData("en", 25)]
    [InlineData("ru", 25)]
    public async Task Should_KeepButtonsBelowListAndStatus_WhenDialogLaidOut(string language, int userCount)
    {
        _localizer.SetLanguage(language);
        var users = Enumerable.Range(0, userCount)
            .Select(index => new MongoUser($"reader{index}", "app", [new MongoRole("read", "app")])).ToList();
        A.CallTo(() => _gateway.ListUsersAsync(A<MongoConnectionTarget>._, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MongoUser>>(users));
        using var dialog = CreateDialog();
        await dialog.LoadAsync();
        dialog.BeginInit();
        dialog.EndInit();

        dialog.Layout(new Size(100, 30));

        var list = dialog.SubViews.OfType<ListView>().Single().FrameToScreen();
        var status = dialog.SubViews.OfType<Label>().Single().FrameToScreen();
        list.Height.Should().BeGreaterThan(0);
        list.Bottom.Should().BeLessThanOrEqualTo(status.Top);
        dialog.Buttons.Should().HaveCount(5);
        foreach (var button in dialog.Buttons)
        {
            var frame = button.FrameToScreen();
            frame.Height.Should().BeGreaterThan(0);
            status.Bottom.Should().BeLessThanOrEqualTo(frame.Top);
            dialog.FrameToScreen().Contains(frame).Should().BeTrue();
        }
        var frames = dialog.Buttons.Select(button => button.FrameToScreen()).OrderBy(frame => frame.Left).ToList();
        frames.Should().OnlyContain(frame => frame.Top == frames[0].Top);
        frames.Zip(frames.Skip(1)).Should().OnlyContain(pair => pair.First.Right <= pair.Second.Left);
    }

    private UserManagementDialog CreateDialog() =>
        new(_users, _localizer, _application, _connection, DatabaseName.Create("app"), "admin", "admin");

    private Button Button(UserManagementDialog dialog, string key) =>
        dialog.Buttons.Single(button => button.Text == _localizer.Get(key));
}
