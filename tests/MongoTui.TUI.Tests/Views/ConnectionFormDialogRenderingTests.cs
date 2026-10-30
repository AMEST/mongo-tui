using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Localization;
using System.Diagnostics;
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
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Color = Terminal.Gui.Drawing.Color;

namespace MongoTui.TUI.Tests.Views;

public sealed class ConnectionFormDialogRenderingTests
{
    private readonly TaskCompletionSource<IReadOnlyList<DatabaseInfo>> _response =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private CancellationToken _requestCancellation;

    public ConnectionFormDialogRenderingTests() =>
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, CancellationToken cancellationToken) =>
            {
                _requestCancellation = cancellationToken;
                return _response.Task.WaitAsync(cancellationToken);
            });

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
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = CreateDialog(app, _gateway, localizer);
        var status = dialog.SubViews.OfType<Label>().Last();
        var test = dialog.Buttons.Single(button => button.Text == localizer.Get(LocalizationKeys.Connections.Test));
        var observe = ObserveTest(app, dialog, test, fails, expected);

        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });
        app.Run(parent);

        var observed = observe();
        status.Text.Should().Be(expected);
        observed.Progress.Should().Contain(progress);
        observed.Result.Should().Contain(expected);
        observed.Disabled.Should().BeTrue();
        test.Enabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("en", "Connection test: Error — Connection timed out after 3 seconds.")]
    [InlineData("ru", "Тест подключения: Ошибка — Превышено время ожидания подключения (3 с).")]
    public void Should_CancelRequestAndRenderLocalizedError_WhenSharedLimitExceeded(string language, string expected)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var dialog = CreateDialog(app, _gateway, localizer);
        var status = dialog.SubViews.OfType<Label>().Last();
        var test = dialog.Buttons.Single(button => button.Text == localizer.Get(LocalizationKeys.Connections.Test));
        var observe = ObserveTest(app, dialog, test, fails: null, expected);
        var elapsed = Stopwatch.StartNew();

        app.Run(dialog);

        observe().Result.Should().Contain(expected);
        _requestCancellation.IsCancellationRequested.Should().BeTrue();
        status.GetScheme().Normal.Foreground.Should().Be((Color)ColorName16.BrightRed);
        test.Enabled.Should().BeTrue();
        elapsed.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(ConnectionService.TestTimeoutSeconds - 0.2));
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(ConnectionService.TestTimeoutSeconds + 3));
    }

    private Func<(string? Progress, string? Result, bool Disabled)> ObserveTest(
        IApplication app, ConnectionFormDialog dialog, Button test, bool? fails, string expected)
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
            if (fails is { } failure) _ = CompleteResponseAsync(failure);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(15), () =>
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
            gateway, new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway), NullLogger<ConnectionService>.Instance);
        return new ConnectionFormDialog(connections, localizer, app, profile: null, secret: null);
    }
}
