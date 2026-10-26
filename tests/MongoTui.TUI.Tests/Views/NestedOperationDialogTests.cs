using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class NestedOperationDialogTests
{
    private readonly ResxLocalizer _localizer = new();
    private readonly ISettingsStore _store = A.Fake<ISettingsStore>();
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly TaskCompletionSource<string?> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _progress = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Theory]
    [InlineData("en", false, false)]
    [InlineData("ru", false, false)]
    [InlineData("en", false, true)]
    [InlineData("ru", false, true)]
    [InlineData("en", true, false)]
    [InlineData("ru", true, false)]
    [InlineData("en", true, true)]
    [InlineData("ru", true, true)]
    public async Task Should_FinishNestedDialog_WhenBackgroundSaveCompletes(string language, bool settings, bool fails)
    {
        _localizer.SetLanguage(language);
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = await CreateDialogAsync(app, settings);
        var error = settings ? _localizer.Get(LocalizationKeys.Query.SettingsSaveError) : "Creation denied.";
        var observe = Observe(app, dialog, () => dialog.Buttons[0].InvokeCommand(Command.Accept),
            LocalizationKeys.Common.Working, fails ? error : null);
        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });
        _ = CompleteAsync(fails);

        app.Run(parent);

        var observed = observe();
        observed.TimedOut.Should().BeFalse("feedback: {0}; response completed: {1}; screen: {2}",
            dialog.SubViews.OfType<Label>().Last().Text, _response.Task.IsCompleted, observed.Result);
        observed.Disabled.Should().BeTrue();
        observed.Progress.Should().Contain(_localizer.Get(LocalizationKeys.Common.Working));
        dialog.Buttons[0].Enabled.Should().BeTrue();
        dialog.SubViews.OfType<TextField>().Should().OnlyContain(field => field.Enabled);
        if (settings) ((QuerySettingsDialog)dialog).Saved.Should().Be(!fails);
        else (((CreateDatabaseDialog)dialog).CreatedCollection is not null).Should().Be(!fails);
        if (fails) observed.Result.Should().Contain(error);
        if (settings) A.CallTo(() => _store.SetAsync("query.timeoutSeconds", "120",
            A<CancellationToken>.That.Matches(token => token.CanBeCanceled))).MustHaveHappenedOnceExactly();
        else A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._,
            A<CancellationToken>.That.Matches(token => token.CanBeCanceled))).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public void Should_LoadNestedSettings_WhenBackgroundReadCompletes(string language, bool fails)
    {
        _localizer.SetLanguage(language);
        A.CallTo(() => _store.GetAsync("query.timeoutSeconds", A<CancellationToken>._)).Returns(_response.Task);
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new QuerySettingsDialog(new SettingsService(_store), _localizer, app);
        var error = _localizer.Get(LocalizationKeys.Query.SettingsLoadError);
        var observe = Observe(app, dialog, () => _ = dialog.LoadAsync(CancellationToken.None),
            LocalizationKeys.Common.Loading, fails ? error : string.Empty);
        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });
        _ = CompleteAsync(fails);

        app.Run(parent);

        observe().TimedOut.Should().BeFalse("feedback: {0}; response completed: {1}; screen: {2}",
            dialog.SubViews.OfType<Label>().Last().Text, _response.Task.IsCompleted, observe().Result);
        observe().Disabled.Should().BeTrue();
        observe().Progress.Should().Contain(_localizer.Get(LocalizationKeys.Common.Loading));
        if (fails) observe().Result.Should().Contain(error);
        else dialog.SubViews.OfType<TextField>().Single().Text.Should().Be("120");
        dialog.Buttons[0].Enabled.Should().Be(!fails);
        dialog.Saved.Should().BeFalse();
    }

    private async Task<Dialog> CreateDialogAsync(IApplication app, bool settings)
    {
        if (settings)
        {
            A.CallTo(() => _store.GetAsync("query.timeoutSeconds", A<CancellationToken>._)).Returns("120");
            A.CallTo(() => _store.SetAsync("query.timeoutSeconds", "120", A<CancellationToken>._)).Returns(_response.Task);
            var dialog = new QuerySettingsDialog(new SettingsService(_store), _localizer, app);
            await dialog.LoadAsync(CancellationToken.None);
            return dialog;
        }
        ConfigureDatabaseDiscovery();
        var databases = new DatabaseService(_gateway);
        var create = new CreateDatabaseDialog(_localizer, app, (collection, token) =>
            databases.CreateDatabaseAsync(new MongoConnectionTarget("mongodb://localhost"), collection, token));
        var fields = create.SubViews.OfType<TextField>().ToArray();
        fields[0].Text = "demo";
        fields[1].Text = "orders";
        return create;
    }

    private void ConfigureDatabaseDiscovery()
    {
        var client = A.Fake<IMongoClient>();
        var cursor = A.Fake<IAsyncCursor<BsonDocument>>();
        A.CallTo(() => client.Settings).Returns(new MongoClientSettings());
        A.CallTo(() => client.ListDatabasesAsync(A<CancellationToken>._)).ReturnsLazily(async () =>
        {
            await _response.Task.ConfigureAwait(false);
            return cursor;
        });
        var discovery = new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance);
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, CancellationToken token) => discovery.ListAsync(client, null, token));
    }

    private Func<(bool TimedOut, bool Disabled, string? Progress, string? Result)> Observe(
        IApplication app, Dialog dialog, Action start, string progressKey, string? expected)
    {
        var timedOut = false;
        var disabled = false;
        string? progress = null, result = null;
        app.AddTimeout(TimeSpan.FromMilliseconds(40), () =>
        {
            start();
            if (progressKey == LocalizationKeys.Common.Working) start();
            disabled = !dialog.Buttons[0].Enabled && dialog.SubViews.OfType<TextField>().All(field => !field.Enabled);
            return false;
        });
        app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            var screen = ScreenText(app);
            result = screen;
            if (screen.Contains(_localizer.Get(progressKey), StringComparison.Ordinal))
            {
                progress = screen;
                _progress.TrySetResult();
            }
            if (expected is null || !_response.Task.IsCompleted || dialog.SubViews.OfType<Label>().Last().Text != expected
                || expected.Length > 0 && !screen.Contains(expected, StringComparison.Ordinal)) return true;
            result = screen;
            app.RequestStop(dialog);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { timedOut = true; app.RequestStop(dialog); return false; });
        return () => (timedOut, disabled, progress, result);
    }

    private async Task CompleteAsync(bool fails)
    {
        await _progress.Task.ConfigureAwait(false);
        await Task.Delay(60).ConfigureAwait(false);
        if (fails) _response.TrySetException(new DomainException("Creation denied."));
        else _response.TrySetResult("120");
    }

    private static string ScreenText(IApplication app)
    {
        var cells = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
