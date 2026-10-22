using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Databases;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ConnectionFormDialogRenderingTests
{
    private readonly TaskCompletionSource<IReadOnlyList<DatabaseInfo>> _response =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();

    public ConnectionFormDialogRenderingTests() =>
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(_response.Task);

    [Theory]
    [InlineData("en", false, "Connection test: Running…", "Connection test: OK")]
    [InlineData("ru", false, "Тест подключения: Проверка…", "Тест подключения: OK")]
    [InlineData("en", true, "Connection test: Running…", "Connection test: Error — Timed out.")]
    [InlineData("ru", true, "Тест подключения: Проверка…", "Тест подключения: Ошибка — Timed out.")]
    public void Should_RenderProgressAndResultWithoutFurtherInput_WhenConnectionTestCompletes(
        string language, bool fails, string progress, string expected)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        app.Begin(parent);
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = CreateDialog(app, _gateway, localizer);
        var status = dialog.SubViews.OfType<Label>().Last();
        var test = dialog.Buttons.Single(button => button.Text == localizer.Get("Connections.Test"));
        var observe = ObserveTest(app, dialog, test, fails, expected);

        app.Run(dialog);

        var observed = observe();
        status.Text.Should().Be(expected);
        observed.Progress.Should().Contain(progress);
        observed.Result.Should().Contain(expected);
        observed.Disabled.Should().BeTrue();
        test.Enabled.Should().BeTrue();
    }

    private Func<(string? Progress, string? Result, bool Disabled)> ObserveTest(
        IApplication app, ConnectionFormDialog dialog, Button test, bool fails, string expected)
    {
        string? observedProgress = null;
        string? resultScreen = null;
        var disabledWhileRunning = false;
        var status = dialog.SubViews.OfType<Label>().Last();
        status.TextChanged += (_, _) =>
        {
            if (status.Text != expected) return;
            app.AddTimeout(TimeSpan.Zero, () =>
            {
                resultScreen = ScreenText(app);
                app.RequestStop(dialog);
                return false;
            });
        };
        app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            test.InvokeCommand(Command.Accept);
            observedProgress = ScreenText(app);
            disabledWhileRunning = !test.Enabled;
            _ = CompleteResponseAsync(fails);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(5), () =>
        {
            app.RequestStop(dialog);
            return false;
        });

        return () => (observedProgress, resultScreen, disabledWhileRunning);
    }

    private async Task CompleteResponseAsync(bool fails)
    {
        await Task.Delay(50).ConfigureAwait(false);
        if (fails) _response.SetException(new TimeoutException("Timed out."));
        else _response.SetResult([]);
    }

    private static string ScreenText(IApplication app)
    {
        app.LayoutAndDraw();
        var cells = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }

    private static ConnectionFormDialog CreateDialog(IApplication app, IMongoGateway gateway, ResxLocalizer localizer)
    {
        var connections = new ConnectionService(A.Fake<IConnectionRepository>(), new SecretKeyService(A.Fake<ICryptoService>()),
            gateway, new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway));
        var dialog = new ConnectionFormDialog(connections, localizer, app, profile: null, secret: null);
        dialog.SubViews.OfType<TextField>().First().Text = "local";
        return dialog;
    }
}
