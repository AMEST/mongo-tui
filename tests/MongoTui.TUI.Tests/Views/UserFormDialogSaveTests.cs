using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.TUI.Localization;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class UserFormDialogSaveTests
{
    private readonly IMongoUserGateway _gateway = A.Fake<IMongoUserGateway>();
    private readonly ResxLocalizer _localizer = new();
    private readonly TaskCompletionSource _response = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_RenderProgressAndResult_WhenSaveCompletes(string language, bool fails)
    {
        _localizer.SetLanguage(language);
        A.CallTo(() => _gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._,
            A<string>._, A<string>._, A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._)).Returns(_response.Task);
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = CreateDialog(app);
        var observe = ObserveSave(app, dialog, fails);
        app.AddTimeout(TimeSpan.Zero, () =>
        {
            app.Run(dialog);
            app.RequestStop(parent);
            return false;
        });

        app.Run(parent);

        var observed = observe();
        observed.Progress.Should().Contain(_localizer.Get("Users.Saving"));
        observed.Disabled.Should().BeTrue();
        dialog.Buttons[0].Enabled.Should().BeTrue();
        dialog.Saved.Should().Be(!fails);
        if (fails)
        {
            observed.Result.Should().Contain(_localizer.Get("Users.Error.Unauthorized"));
            dialog.SubViews.OfType<Label>().Last().GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightRed);
            dialog.SavedPassword.Should().BeNull();
        }
        else
        {
            dialog.Username.Should().Be("reader");
            dialog.SavedPassword.Should().Be("secret");
        }
        A.CallTo(() => _gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._,
            "reader", "secret", A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("en", "username", "Users.UsernameRequired")]
    [InlineData("ru", "username", "Users.UsernameRequired")]
    [InlineData("en", "password", "Users.PasswordRequired")]
    [InlineData("ru", "password", "Users.PasswordRequired")]
    [InlineData("en", "access", "Users.Access.Required")]
    [InlineData("ru", "access", "Users.Access.Required")]
    public void Should_ShowValidationError_WhenRequiredValueMissing(string language, string missing, string expectedKey)
    {
        _localizer.SetLanguage(language);
        var app = A.Fake<IApplication>();
        using var dialog = CreateDialog(app);
        if (missing == "access") dialog.SubViews.OfType<CheckBox>().ToList().ForEach(check => check.Value = CheckState.UnChecked);
        else dialog.SubViews.OfType<TextField>().Single(field => field.Secret == (missing == "password")).Text = string.Empty;

        dialog.Buttons[0].InvokeCommand(Command.Accept);

        dialog.SubViews.OfType<Label>().Last().Text.Should().Be(_localizer.Get(expectedKey));
        dialog.Saved.Should().BeFalse();
        A.CallTo(() => app.RequestStop(dialog)).MustNotHaveHappened();
        A.CallTo(() => _gateway.CreateUserAsync(A<MongoConnectionTarget>._, A<DatabaseName>._,
            A<string>._, A<string>._, A<IReadOnlyList<MongoRole>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private Func<(string? Progress, string? Result, bool Disabled)> ObserveSave(
        IApplication app, UserFormDialog dialog, bool fails)
    {
        string? progress = null;
        string? result = null;
        var disabled = false;
        var status = dialog.SubViews.OfType<Label>().Last();
        status.TextChanged += (_, _) =>
        {
            if (status.Text != _localizer.Get("Users.Error.Unauthorized")) return;
            app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
            {
                result = ScreenText(app);
                app.RequestStop(dialog);
                return false;
            });
        };
        app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            dialog.Buttons[0].InvokeCommand(Command.Accept);
            dialog.Buttons[0].InvokeCommand(Command.Accept);
            app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
            {
                progress = ScreenText(app);
                disabled = !dialog.Buttons[0].Enabled;
                _ = CompleteResponseAsync(fails);
                return false;
            });
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); return false; });
        return () => (progress, result, disabled);
    }

    private async Task CompleteResponseAsync(bool fails)
    {
        await Task.Delay(50).ConfigureAwait(false);
        if (fails) _response.SetException(new DomainException("Insufficient privileges to manage users."));
        else _response.SetResult();
    }

    private UserFormDialog CreateDialog(IApplication app)
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        var connection = new ActiveConnection(profile, new MongoConnectionTarget(profile.Uri), []);
        var users = new UserManagementService(_gateway, new PasswordGenerator());
        var dialog = new UserFormDialog(users, _localizer, app, connection, DatabaseName.Create("app"), null);
        dialog.SubViews.OfType<TextField>().Single(field => !field.Secret).Text = "reader";
        dialog.SubViews.OfType<TextField>().Single(field => field.Secret).Text = "secret";
        dialog.SubViews.OfType<CheckBox>().Single(check => check.Text == _localizer.Get("Users.ClusterRoles.ClusterMonitor"))
            .Value = CheckState.Checked;
        return dialog;
    }

    private static string ScreenText(IApplication app)
    {
        var cells = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
