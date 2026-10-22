using System.Drawing;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ConnectionFormDialogTests
{
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();
    private readonly ConnectionService _connections;

    public ConnectionFormDialogTests()
    {
        var gateway = A.Fake<IMongoGateway>();
        _connections = new ConnectionService(_repository, new SecretKeyService(A.Fake<ICryptoService>()), gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway));
    }

    [Theory]
    [InlineData("en", "Secrets are locked. Unlock with the master password.")]
    [InlineData("ru", "Секреты заблокированы. Разблокируйте мастер-паролем.")]
    public void Should_ShowLocalizedErrorAndKeepFormOpen_WhenSavingSecretWhileLocked(string language, string expected)
    {
        _localizer.SetLanguage(language);
        using var dialog = CreateDialog();

        SaveButton(dialog).InvokeCommand(Command.Accept);

        dialog.SubViews.OfType<Label>().Last().Text.Should().Be(expected);
        dialog.Saved.Should().BeFalse();
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Should_KeepErrorBelowAllFieldsAndAboveButtons_WhenSavingFails(string language)
    {
        _localizer.SetLanguage(language);
        using var dialog = CreateDialog();
        SaveButton(dialog).InvokeCommand(Command.Accept);
        dialog.BeginInit();
        dialog.EndInit();

        dialog.Layout(new Size(80, 24));

        var error = dialog.SubViews.OfType<Label>().Last();
        foreach (var field in dialog.SubViews.Where(view => view is TextField or ListView))
        {
            field.Frame.Bottom.Should().BeLessThan(error.Frame.Top);
        }
        error.Frame.Height.Should().BeGreaterThan(0);
        error.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
        error.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
        foreach (var button in dialog.Buttons)
        {
            error.FrameToScreen().Bottom.Should().BeLessThanOrEqualTo(button.FrameToScreen().Top);
        }
    }

    private ConnectionFormDialog CreateDialog()
    {
        var dialog = new ConnectionFormDialog(_connections, _localizer, _application, profile: null, secret: null);
        dialog.SubViews.OfType<TextField>().First().Text = "local";
        dialog.SubViews.OfType<TextField>().Single(field => field.Secret).Text = "password";
        return dialog;
    }

    private Button SaveButton(ConnectionFormDialog dialog) =>
        dialog.Buttons.Single(button => button.Text == _localizer.Get("Connections.Save"));
}
