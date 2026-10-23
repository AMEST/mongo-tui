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

    private UserManagementDialog CreateDialog() =>
        new(_users, _localizer, _application, _connection, DatabaseName.Create("app"), "admin", "admin");

    private Button Button(UserManagementDialog dialog, string key) =>
        dialog.SubViews.OfType<Button>().Single(button => button.Text == _localizer.Get(key));
}
