using System.Drawing;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Exceptions;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Color = Terminal.Gui.Drawing.Color;

namespace MongoTui.TUI.Tests.Views;

public sealed class ConnectionFormDialogTests
{
    private readonly IConnectionRepository _repository = A.Fake<IConnectionRepository>();
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ConnectionService _connections;

    public ConnectionFormDialogTests()
    {
        _connections = new ConnectionService(_repository, new SecretKeyService(A.Fake<ICryptoService>()), _gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), _gateway));
    }

    [Theory]
    [InlineData("en", "Secrets are locked. Unlock with the master password.")]
    [InlineData("ru", "Секреты заблокированы. Разблокируйте мастер-паролем.")]
    public void Should_ShowLocalizedErrorAndKeepFormOpen_WhenSavingSecretWhileLocked(string language, string expected)
    {
        _localizer.SetLanguage(language);
        using var dialog = CreateDialog();

        SaveButton(dialog).InvokeCommand(Command.Accept);

        Status(dialog).Text.Should().Be(expected);
        Status(dialog).GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightRed);
        dialog.Saved.Should().BeFalse();
        A.CallTo(() => _repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en", false, true)]
    [InlineData("ru", false, true)]
    [InlineData("en", true, true)]
    [InlineData("ru", true, true)]
    [InlineData("en", true, false)]
    [InlineData("ru", true, false)]
    public void Should_KeepStatusBelowAllFieldsAndAboveButtons_WhenActionCompletes(string language, bool test, bool fails)
    {
        _localizer.SetLanguage(language);
        if (fails)
        {
            A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
                .Throws(new ConnectionFailedException("Connection refused."));
        }
        using var dialog = CreateDialog();
        var actionButton = Button(dialog, test ? "Connections.Test" : "Connections.Save");
        actionButton.InvokeCommand(Command.Accept);
        dialog.BeginInit();
        dialog.EndInit();

        dialog.Layout(new Size(80, 24));

        var status = Status(dialog);
        foreach (var field in dialog.SubViews.Where(view => view is TextField or ListView))
        {
            field.Frame.Bottom.Should().BeLessThan(status.Frame.Top);
        }
        status.Text.Should().NotBeNullOrWhiteSpace();
        status.Frame.Height.Should().BeGreaterThan(0);
        status.Frame.Bottom.Should().BeLessThanOrEqualTo(dialog.Viewport.Height);
        status.Frame.Right.Should().BeLessThanOrEqualTo(dialog.Viewport.Width);
        foreach (var button in dialog.Buttons)
        {
            status.FrameToScreen().Bottom.Should().BeLessThanOrEqualTo(button.FrameToScreen().Top);
        }
    }

    [Theory]
    [InlineData("en", "Connection test: OK")]
    [InlineData("ru", "Тест подключения: OK")]
    public void Should_ShowGreenLocalizedStatus_WhenConnectionTestSucceeds(string language, string expected)
    {
        _localizer.SetLanguage(language);
        using var dialog = CreateDialog();

        Button(dialog, "Connections.Test").InvokeCommand(Command.Accept);

        Status(dialog).Text.Should().Be(expected);
        Status(dialog).GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightGreen);
        dialog.Saved.Should().BeFalse();
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("en", "Connection test: Error — Connection refused.")]
    [InlineData("ru", "Тест подключения: Ошибка — Connection refused.")]
    public void Should_ShowRedLocalizedStatusWithErrorDetails_WhenConnectionTestFails(string language, string expected)
    {
        _localizer.SetLanguage(language);
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Throws(new ConnectionFailedException("Connection refused."));
        using var dialog = CreateDialog();

        Button(dialog, "Connections.Test").InvokeCommand(Command.Accept);

        Status(dialog).Text.Should().Be(expected);
        Status(dialog).GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightRed);
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_ShowRedStatusWithErrorDetails_WhenConnectionTestTimesOut()
    {
        _localizer.SetLanguage("en");
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .ThrowsAsync(new TimeoutException("Server selection timed out."));
        using var dialog = CreateDialog();

        Button(dialog, "Connections.Test").InvokeCommand(Command.Accept);

        Status(dialog).Text.Should().Be("Connection test: Error — Server selection timed out.");
        Status(dialog).GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightRed);
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    [Fact]
    public void Should_ChangeStatusToGreen_WhenRetrySucceedsAfterFailedTest()
    {
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Throws(new ConnectionFailedException("Connection refused."));
        using var dialog = CreateDialog();
        Button(dialog, "Connections.Test").InvokeCommand(Command.Accept);
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Returns(Array.Empty<DatabaseInfo>());

        Button(dialog, "Connections.Test").InvokeCommand(Command.Accept);

        Status(dialog).Text.Should().Be(_localizer.Get("Connections.TestOk"));
        Status(dialog).GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightGreen);
    }

    [Theory]
    [InlineData("Connections.Save")]
    [InlineData("Connections.FromUri")]
    public void Should_ChangeStatusToRed_WhenValidationFailsAfterSuccessfulTest(string action)
    {
        using var dialog = CreateDialog();
        Button(dialog, "Connections.Test").InvokeCommand(Command.Accept);
        dialog.SubViews.OfType<TextField>().ElementAt(1).Text = "invalid";

        Button(dialog, action).InvokeCommand(Command.Accept);

        Status(dialog).Text.Should().NotBeNullOrWhiteSpace().And.NotBe(_localizer.Get("Connections.TestOk"));
        Status(dialog).GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightRed);
    }

    private ConnectionFormDialog CreateDialog()
    {
        var dialog = new ConnectionFormDialog(_connections, _localizer, _application, profile: null, secret: null);
        dialog.SubViews.OfType<TextField>().First().Text = "local";
        dialog.SubViews.OfType<TextField>().Single(field => field.Secret).Text = "password";
        return dialog;
    }

    private Button SaveButton(ConnectionFormDialog dialog) =>
        Button(dialog, "Connections.Save");

    private Button Button(ConnectionFormDialog dialog, string key) =>
        dialog.Buttons.Single(button => button.Text == _localizer.Get(key));

    private static Label Status(ConnectionFormDialog dialog) => dialog.SubViews.OfType<Label>().Last();
}
