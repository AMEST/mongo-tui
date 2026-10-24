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
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Color = Terminal.Gui.Drawing.Color;

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
        var hint = dialog.SubViews.OfType<Label>().Single(label => label.Text == _localizer.Get("Users.Form.PasswordKeepHint"));
        hint.GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.DarkGray);
        hint.GetScheme().Normal.Background.Should().Be(dialog.GetScheme().Normal.Background);
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

    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_KeepFieldsAndButtonsSeparateAndInsideDialog_WhenLaidOut(string language, bool editing)
    {
        _localizer.SetLanguage(language);
        var existing = editing ? new MongoUser("bob", "admin", [new MongoRole("customRole", "orders")]) : null;
        using var dialog = new UserFormDialog(_users, _localizer, _application, _connection, DatabaseName.Create("admin"), existing);
        dialog.BeginInit();
        dialog.EndInit();

        dialog.Layout(new Size(80, 24));

        var generate = dialog.SubViews.OfType<Button>().Single(button => button.Text == _localizer.Get("Users.Field.Generate"));
        var add = dialog.SubViews.OfType<Button>().Single(button => button.Text == _localizer.Get("Users.Access.Add"));
        var remove = dialog.SubViews.OfType<Button>().Single(button => button.Text == _localizer.Get("Users.Access.Remove"));
        var password = dialog.SubViews.OfType<TextField>().Single(field => field.Secret);
        password.Frame.Width.Should().BeGreaterThan(0);
        password.Frame.Right.Should().BeLessThan(generate.Frame.Left);
        add.Frame.Right.Should().BeLessThan(remove.Frame.Left);
        foreach (var view in dialog.SubViews.Where(view => view is Button or TextField))
        {
            view.Frame.Left.Should().BeGreaterThanOrEqualTo(0);
            view.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
        }
        var error = dialog.SubViews.OfType<Label>().Last();
        foreach (var view in dialog.SubViews.Where(view => view.Visible && view is CheckBox or ListView))
        {
            view.Frame.Bottom.Should().BeLessThanOrEqualTo(error.Frame.Top);
        }
        error.FrameToScreen().Bottom.Should().BeLessThanOrEqualTo(dialog.Buttons[0].FrameToScreen().Top);
    }
}
