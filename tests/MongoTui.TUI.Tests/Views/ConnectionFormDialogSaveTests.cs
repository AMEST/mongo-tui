using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class ConnectionFormDialogSaveTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_FinishNestedSave_WhenRepositoryCompletesInBackground(string language, bool fails)
    {
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = A.Fake<IConnectionRepository>();
        A.CallTo(() => repository.GetAsync(A<MongoTui.Domain.Primitives.ConnectionId>._, A<CancellationToken>._))
            .Returns((StoredConnection?)null);
        A.CallTo(() => repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).Returns(response.Task);
        var gateway = A.Fake<IMongoGateway>();
        var connections = new ConnectionService(repository, new SecretKeyService(A.Fake<ICryptoService>()), gateway,
            new ConnectionTunnelService(A.Fake<ISshTunnelFactory>(), A.Fake<ITwoFactorPrompt>(), gateway), NullLogger<ConnectionService>.Instance);
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        var localizer = new ResxLocalizer();
        localizer.SetLanguage(language);
        using var parent = new Dialog();
        using var dialog = new ConnectionFormDialog(connections, localizer, app, null, null);
        dialog.SubViews.OfType<TextField>().First().Text = "local";
        var save = dialog.Buttons.Single(button => button.Text == localizer.Get(LocalizationKeys.Connections.Save));
        var observe = Observe(app, dialog, save, response, fails, localizer);
        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });

        app.Run(parent);

        var result = observe();
        result.TimedOut.Should().BeFalse();
        result.Progress.Should().Contain(localizer.Get(LocalizationKeys.Common.Working));
        result.Disabled.Should().BeTrue();
        save.Enabled.Should().BeTrue();
        dialog.Saved.Should().Be(!fails);
        if (fails) result.Error.Should().Contain("Save denied.");
        A.CallTo(() => repository.AddAsync(A<StoredConnection>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static Func<(bool TimedOut, bool Disabled, string? Progress, string? Error)> Observe(
        IApplication app, ConnectionFormDialog dialog, Button save, TaskCompletionSource response, bool fails, ResxLocalizer localizer)
    {
        var timedOut = false;
        var disabled = false;
        string? progress = null, error = null;
        app.AddTimeout(TimeSpan.FromMilliseconds(40), () =>
        {
            save.InvokeCommand(Command.Accept);
            save.InvokeCommand(Command.Accept);
            disabled = !save.Enabled && dialog.SubViews.OfType<TextField>().All(field => !field.Enabled);
            return false;
        });
        app.AddTimeout(TimeSpan.FromMilliseconds(80), () =>
        {
            var screen = ScreenText(app);
            if (progress is null && screen.Contains(localizer.Get(LocalizationKeys.Common.Working), StringComparison.Ordinal))
            {
                progress = screen;
                _ = CompleteAsync(response, fails);
            }
            if (!fails || !screen.Contains("Save denied.", StringComparison.Ordinal)) return true;
            error = screen;
            app.RequestStop(dialog);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { timedOut = true; app.RequestStop(dialog); return false; });
        return () => (timedOut, disabled, progress, error);
    }

    private static async Task CompleteAsync(TaskCompletionSource response, bool fails)
    {
        await Task.Delay(60).ConfigureAwait(false);
        if (fails) response.SetException(new DomainException("Save denied."));
        else response.SetResult();
    }

    private static string ScreenText(IApplication app)
    {
        var cells = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
