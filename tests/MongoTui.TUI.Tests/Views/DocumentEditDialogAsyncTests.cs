using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.TUI.Localization;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class DocumentEditDialogAsyncTests
{
    private readonly IDriverMongoGateway _driver = A.Fake<IDriverMongoGateway>();
    private readonly ResxLocalizer _localizer = new();
    private readonly TaskCompletionSource _save = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<QueryResult> _load = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _progressRendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly CollectionRef Collection = new(DatabaseName.Create("app"), CollectionName.Create("items"));

    [Theory]
    [InlineData("en", false, false)]
    [InlineData("ru", false, false)]
    [InlineData("en", false, true)]
    [InlineData("ru", false, true)]
    [InlineData("en", true, false)]
    [InlineData("ru", true, false)]
    [InlineData("en", true, true)]
    [InlineData("ru", true, true)]
    public async Task Should_UpdateNestedDialogWithoutInput_When_BackgroundOperationCompletes(string language, bool load, bool fails)
    {
        _localizer.SetLanguage(language);
        var gateway = new CompositeMongoGateway(_driver, A.Fake<ICliMongoTransferGateway>());
        var model = new DocumentEditViewModel(new DocumentService(gateway, NullLogger<DocumentService>.Instance), Target, Collection,
            load ? new BsonDocument("_id", 1) : null);
        if (load)
        {
            FindReturns(Task.FromResult(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] }));
            (await model.InitializeAsync(CancellationToken.None)).Should().BeTrue();
            FindReturns(_load.Task);
        }
        else A.CallTo(() => _driver.InsertOneAsync(Target, Collection, A<string>._, A<CancellationToken>._)).Returns(_save.Task);
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new DocumentEditDialog(_localizer, app, model);
        var observe = Observe(app, dialog, load, fails);
        app.AddTimeout(TimeSpan.Zero, () => { app.Run(dialog); app.RequestStop(parent); return false; });

        app.Run(parent);

        var result = observe();
        result.Disabled.Should().BeTrue();
        result.Progress.Should().Contain(_localizer.Get(LocalizationKeys.Documents.Working));
        dialog.Saved.Should().Be(!load && !fails);
        if (fails)
        {
            dialog.SubViews.OfType<Label>().Last().Text.Should().Be(_localizer.Get(LocalizationKeys.Documents.UnsafeJson));
            result.Result.Should().Contain(_localizer.Get(LocalizationKeys.Documents.UnsafeJson)[..25]);
        }
        else if (load) model.Json.Should().Contain("fresh").And.Contain("$numberLong");
        dialog.Buttons[0].Enabled.Should().Be(!load || !fails);
    }

    private Func<(bool Disabled, string? Progress, string? Result)> Observe(
        IApplication app, DocumentEditDialog dialog, bool load, bool fails)
    {
        var disabled = false;
        string? progress = null, result = null;
        var started = false;
        app.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            if (!started)
            {
                started = true;
                var action = load ? dialog.SubViews.OfType<Button>().Single(button => button.Text == _localizer.Get(LocalizationKeys.Documents.Load))
                    : dialog.Buttons[0];
                action.InvokeCommand(Command.Accept);
                action.InvokeCommand(Command.Accept);
                disabled = !dialog.Buttons[0].Enabled;
                _ = CompleteAsync(load, fails);
                return true;
            }
            var screen = ScreenText(app);
            if (screen.Contains(_localizer.Get(LocalizationKeys.Documents.Working), StringComparison.Ordinal))
            {
                progress ??= screen;
                _progressRendered.TrySetResult();
            }
            if (dialog.SubViews.OfType<Label>().Last().Text == _localizer.Get(LocalizationKeys.Documents.Working)) return true;
            result = ScreenText(app);
            app.RequestStop(dialog);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); return false; });
        return () => (disabled, progress, result);
    }

    private async Task CompleteAsync(bool load, bool fails)
    {
        await _progressRendered.Task.ConfigureAwait(false);
        await Task.Delay(80).ConfigureAwait(false);
        if (!load)
        {
            if (fails) _save.SetException(new DocumentEditException("Unsafe BSON.", LocalizationKeys.Documents.UnsafeJson));
            else _save.SetResult();
            return;
        }
        var document = new BsonDocument { { "_id", 1 }, { "text", "fresh" }, { "long", new BsonInt64(42) } };
        if (fails) document["literal"] = new BsonDocument("$numberLong", "42");
        _load.SetResult(new QueryResult { Kind = QueryResultKind.Documents, Documents = [document] });
    }

    private void FindReturns(Task<QueryResult> response) => A.CallTo(() => _driver.FindAsync(Target, Collection,
        A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(response);

    private static string ScreenText(IApplication app)
    {
        var cells = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
