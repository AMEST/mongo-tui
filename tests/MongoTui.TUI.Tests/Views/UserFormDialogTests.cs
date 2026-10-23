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

public sealed class UserFormDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();
    private readonly UserManagementService _users = new(A.Fake<IMongoUserGateway>(), new PasswordGenerator());
    private readonly ActiveConnection _connection;

    public UserFormDialogTests()
    {
        _localizer.SetLanguage("ru");
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        _connection = new ActiveConnection(profile, new MongoConnectionTarget(profile.Uri), []);
    }

    [Fact]
    public void Should_AlwaysShowPasswordHint_When_Editing()
    {
        var existing = new MongoUser("bob", "admin", [new MongoRole("read", "orders")]);

        using var dialog = new UserFormDialog(_users, _localizer, _application, _connection, DatabaseName.Create("admin"), existing);

        dialog.SubViews.OfType<Label>()
            .Should().Contain(label => label.Text == _localizer.Get("Users.Form.PasswordKeepHint") && label.Visible);
    }

    [Fact]
    public void Should_NotShowPasswordHint_When_Creating()
    {
        using var dialog = new UserFormDialog(_users, _localizer, _application, _connection, DatabaseName.Create("admin"), null);

        dialog.SubViews.OfType<Label>()
            .Should().NotContain(label => label.Text == _localizer.Get("Users.Form.PasswordKeepHint") && label.Visible);
    }

    [Fact]
    public void Should_DisableOtherManagedRoles_When_AdministratorChecked()
    {
        using var dialog = new UserFormDialog(_users, _localizer, _application, _connection, DatabaseName.Create("admin"), null);
        var checks = dialog.SubViews.OfType<CheckBox>().ToList();
        var administrator = checks.Single(check => check.Text == _localizer.Get("Users.ClusterRoles.Administrator"));

        administrator.Value = CheckState.Checked;

        checks.Where(check => check != administrator)
            .Should().OnlyContain(check => !check.Enabled);
    }
}
