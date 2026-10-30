using System.Collections.Concurrent;
using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class IndexManagerDialogGatewayTests : IDisposable
{
    private readonly MongoConnectionTarget _target = new("mongodb://localhost");
    private readonly CollectionRef _collection = new(DatabaseName.Create("app"), CollectionName.Create("items"));
    private readonly DriverMongoGateway _gateway = new(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
    private readonly IMongoCollection<BsonDocument> _documents = A.Fake<IMongoCollection<BsonDocument>>();
    private readonly IMongoIndexManager<BsonDocument> _indexes = A.Fake<IMongoIndexManager<BsonDocument>>();
    private readonly TaskCompletionSource _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _progress = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ResxLocalizer _localizer = new();
    private string _stage = string.Empty;

    public IndexManagerDialogGatewayTests()
    {
        var client = A.Fake<IMongoClient>();
        var database = A.Fake<IMongoDatabase>();
        A.CallTo(() => client.GetDatabase("app", A<MongoDatabaseSettings>._)).Returns(database);
        A.CallTo(() => database.GetCollection<BsonDocument>("items", A<MongoCollectionSettings>._)).Returns(_documents);
        A.CallTo(() => _documents.Indexes).Returns(_indexes);
        // Exercise the real gateway while replacing only the MongoDB driver's I/O boundary.
        var clients = (ConcurrentDictionary<string, IMongoClient>)typeof(DriverMongoGateway)
            .GetField("_clients", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_gateway)!;
        clients[_target.ConnectionString] = client;
        ConfigureDriver();
    }

    public void Dispose() => _gateway.Dispose();

    [Theory]
    [InlineData("en", "list", "success")]
    [InlineData("ru", "list", "success")]
    [InlineData("en", "cursor", "success")]
    [InlineData("ru", "cursor", "success")]
    [InlineData("en", "count", "success")]
    [InlineData("ru", "count", "success")]
    [InlineData("en", "statsList", "success")]
    [InlineData("ru", "statsList", "success")]
    [InlineData("en", "statsCursor", "success")]
    [InlineData("ru", "statsCursor", "success")]
    [InlineData("en", "create", "success")]
    [InlineData("ru", "create", "success")]
    [InlineData("en", "list", "error")]
    [InlineData("ru", "list", "error")]
    [InlineData("en", "create", "error")]
    [InlineData("ru", "create", "error")]
    [InlineData("en", "list", "cancelled")]
    [InlineData("ru", "list", "cancelled")]
    public void Should_UpdateNestedDialog_WhenDriverCompletesInBackground(string language, string stage, string outcome)
    {
        _stage = stage;
        _localizer.SetLanguage(language);
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var parent = new Dialog();
        using var dialog = new IndexManagerDialog(new IndexService(_gateway), _localizer, app, _target, _collection);
        dialog.SubViews.OfType<TextField>().First().Text = "{email:1}";
        var observe = Observe(app, dialog, outcome);
        app.AddTimeout(TimeSpan.Zero, () =>
        {
            if (stage == "create") dialog.Buttons[1].InvokeCommand(Command.Accept);
            else _ = dialog.LoadAsync();
            app.Run(dialog);
            app.RequestStop(parent);
            return false;
        });
        _ = CompleteAsync(outcome);

        app.Run(parent);

        var result = observe();
        result.Progress.Should().BeTrue();
        result.Rendered.Should().BeTrue("the dialog must repaint before closing; screen:\n{0}", ScreenText(app));
        dialog.Buttons[0].Enabled.Should().BeTrue();
        dialog.Buttons[1].Enabled.Should().BeTrue();
        dialog.SubViews.OfType<TextField>().Should().OnlyContain(field => field.Enabled);
        if (stage == "create") A.CallTo(() => _indexes.CreateOneAsync(A<CreateIndexModel<BsonDocument>>._,
            A<CreateOneIndexOptions>._, A<CancellationToken>.That.Matches(token => token.CanBeCanceled)))
            .MustHaveHappenedOnceExactly();
    }

    private void ConfigureDriver()
    {
        var listCall = 0;
        A.CallTo(() => _indexes.ListAsync(A<CancellationToken>._)).ReturnsLazily((CancellationToken token) =>
        {
            var first = ++listCall == 1;
            return WaitAsync(first ? "list" : "statsList", Cursor(first ? "cursor" : "statsCursor"), token);
        });
        A.CallTo(() => _documents.CountDocumentsAsync(A<FilterDefinition<BsonDocument>>._,
            A<CountOptions>._, A<CancellationToken>._)).ReturnsLazily(
            (FilterDefinition<BsonDocument> _, CountOptions _, CancellationToken token) => WaitAsync("count", 7L, token));
        A.CallTo(() => _indexes.CreateOneAsync(A<CreateIndexModel<BsonDocument>>._,
            A<CreateOneIndexOptions>._, A<CancellationToken>._)).ReturnsLazily(
            (CreateIndexModel<BsonDocument> _, CreateOneIndexOptions _, CancellationToken token) => WaitAsync("create", "email_1", token));
    }

    private IAsyncCursor<BsonDocument> Cursor(string stage)
    {
        var cursor = A.Fake<IAsyncCursor<BsonDocument>>();
        A.CallTo(() => cursor.Current).Returns([new BsonDocument { { "name", "email_1" }, { "key", new BsonDocument("email", 1) } }]);
        var read = false;
        A.CallTo(() => cursor.MoveNextAsync(A<CancellationToken>._)).ReturnsLazily((CancellationToken token) =>
        {
            if (read) return Task.FromResult(false);
            read = true;
            return WaitAsync(stage, true, token);
        });
        return cursor;
    }

    private async Task<T> WaitAsync<T>(string stage, T value, CancellationToken cancellationToken)
    {
        if (stage == _stage) await _response.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return value;
    }

    private Func<(bool Progress, bool Rendered)> Observe(IApplication app, IndexManagerDialog dialog, string outcome)
    {
        var progress = false;
        var rendered = false;
        var working = _localizer.Get(LocalizationKeys.Common.Working);
        app.AddTimeout(TimeSpan.FromMilliseconds(20), () =>
        {
            var screen = ScreenText(app);
            if (!dialog.Buttons[0].Enabled && screen.Contains(working, StringComparison.Ordinal))
            {
                progress = true;
                _progress.TrySetResult();
            }
            if (!_response.Task.IsCompleted || !dialog.Buttons[0].Enabled) return true;
            var error = dialog.SubViews.OfType<Label>().Last().Text;
            rendered = outcome == "success"
                ? screen.Contains("email: 1", StringComparison.Ordinal)
                    && screen.Contains($"{_localizer.Get(LocalizationKeys.Stats.Documents)}: 7", StringComparison.Ordinal)
                : !string.IsNullOrEmpty(error) && error != working && screen.Contains(error, StringComparison.Ordinal);
            if (!rendered) return true;
            app.RequestStop(dialog);
            return false;
        });
        app.AddTimeout(TimeSpan.FromSeconds(3), () => { app.RequestStop(dialog); return false; });
        return () => (progress, rendered);
    }

    private async Task CompleteAsync(string outcome)
    {
        await _progress.Task.ConfigureAwait(false);
        await Task.Delay(60).ConfigureAwait(false);
        if (outcome == "error") _response.TrySetException(new IOException("Index request failed."));
        else if (outcome == "cancelled") _response.TrySetCanceled();
        else _response.TrySetResult();
    }

    private static string ScreenText(IApplication app)
    {
        var screen = app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, screen.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, screen.GetLength(1)).Select(column => screen[row, column].Grapheme))));
    }
}
