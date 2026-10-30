using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.Localization;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryTabsExecutionTests : IDisposable
{
    private readonly IApplication _app;
    private readonly Dialog _parent = new();
    private readonly Dialog _dialog = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly ResxLocalizer _localizer = new();
    private readonly IMongoShellSession _session = A.Fake<IMongoShellSession>();
    private readonly TaskCompletionSource<QueryResult> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly QueryTabsView _tabs;
    private readonly MongoConnectionTarget _target = new("mongodb://localhost");
    private Task<QueryRunSummary?>? _operation;
    private Task? _close;
    private QueryTabViewModel? _original;
    private CancellationToken _token;
    private bool _rendered;

    public QueryTabsExecutionTests()
    {
        var sessions = A.Fake<IMongoShellSessionFactory>();
        A.CallTo(() => sessions.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(_session);
        A.CallTo(() => _session.IsRunning).Returns(true);
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Invokes((string script, string? database, CancellationToken token) => _token = token).Returns(_response.Task);
        _tabs = new QueryTabsView(QueryTestServices.Create(sessions, A.Fake<IQueryHistoryRepository>()),
            _localizer, new AutocompleteService(A.Fake<IMongoGateway>())) { Width = Dim.Fill(), Height = Dim.Fill() };
        _dialog.Add(_tabs);
        _app = Terminal.Gui.App.Application.Create();
        _app.Init(DriverRegistry.Names.ANSI);
    }

    public void Dispose()
    {
        _dialog.Dispose();
        _parent.Dispose();
        _app.Dispose();
    }

    [Theory]
    [InlineData("en", false, false, false)]
    [InlineData("ru", false, false, false)]
    [InlineData("en", true, false, false)]
    [InlineData("ru", true, false, false)]
    [InlineData("en", false, true, false)]
    [InlineData("ru", false, true, false)]
    [InlineData("en", false, false, true)]
    [InlineData("ru", false, false, true)]
    public async Task Should_ApplyBackgroundResultSafely_WhenQueryRunsInsideNestedLoop(
        string language, bool switchTab, bool closeTab, bool fails)
    {
        _localizer.SetLanguage(language);
        ScheduleExecution(switchTab, closeTab, fails);

        _app.Run(_parent);

        _operation.Should().NotBeNull();
        _operation!.IsCompleted.Should().BeTrue();
        var summary = await _operation;
        A.CallTo(() => _session.EvaluateAsync("db.orders.find({}).limit(50).toArray()", "app",
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _original!.IsExecuting.Should().BeFalse();
        if (closeTab)
        {
            summary.Should().BeNull();
            _tabs.TabCount.Should().Be(0);
            _token.IsCancellationRequested.Should().BeTrue();
        }
        else if (switchTab) _tabs.ActiveEditorView!.HasFocus.Should().BeTrue();
        else _rendered.Should().BeTrue();
    }

    [Theory]
    [InlineData("en", false, false)]
    [InlineData("ru", true, false)]
    [InlineData("en", false, true)]
    public async Task Should_RefreshOriginalTabWithoutExecutingDraft_WhenBackgroundResultArrives(
        string language, bool switchTab, bool closeTab)
    {
        _localizer.SetLanguage(language);
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(Task.FromResult(new QueryResult { Kind = QueryResultKind.Documents,
                Documents = [new BsonDocument("_id", 1)] }), _response.Task);
        ScheduleExecution(switchTab, closeTab, false, refresh: true);

        _app.Run(_parent);

        await _operation!;
        A.CallTo(() => _session.EvaluateAsync("db.orders.find({}).limit(50).toArray()", "app", A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
        A.CallTo(() => _session.EvaluateAsync("db.orders.deleteMany({})", A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        _original!.Script.Should().Be("db.orders.deleteMany({})");
        if (closeTab) _tabs.TabCount.Should().Be(0);
        else if (switchTab) _tabs.ActiveEditorView!.HasFocus.Should().BeTrue();
        else _rendered.Should().BeTrue();
    }

    private void ScheduleExecution(bool switchTab, bool closeTab, bool fails, bool refresh = false)
    {
        _app.AddTimeout(TimeSpan.Zero, () => { _app.Run(_dialog); _app.RequestStop(_parent); return false; });
        _app.AddTimeout(TimeSpan.FromMilliseconds(40), () =>
        {
            _original = _tabs.AddTab(ConnectionId.New(), _target, "app", "orders",
                "db.orders.find({}).limit(50).toArray()", "Production");
            if (refresh)
            {
                _tabs.RunActiveAsync(_app).IsCompleted.Should().BeTrue();
                _original.Script = "db.orders.deleteMany({})";
                _original.Database = "draft";
                _operation = _tabs.RefreshAsync(_app, _original);
            }
            else { _operation = _tabs.RunActiveAsync(_app); _ = _tabs.RunActiveAsync(_app); }
            if (switchTab) _tabs.AddTab(ConnectionId.New(), _target, "other", script: "db.unrelated.find({})");
            if (closeTab) _close = _tabs.CloseActiveAsync();
            _ = CompleteAsync(fails);
            return false;
        });
        _app.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            if (_operation?.IsCompleted != true || _close is { IsCompleted: false }) return true;
            _rendered = ScreenText().Contains(fails ? "Automatic query failed." : "automatically", StringComparison.Ordinal);
            if (!switchTab && !closeTab && !_rendered) return true;
            _app.RequestStop(_dialog);
            return false;
        });
        _app.AddTimeout(TimeSpan.FromSeconds(3), () => { _app.RequestStop(_dialog); return false; });
    }

    private async Task CompleteAsync(bool fails)
    {
        await Task.Delay(80).ConfigureAwait(false);
        if (fails) _response.SetException(new DomainException("Automatic query failed."));
        else _response.SetResult(new QueryResult { Kind = QueryResultKind.Documents,
            Documents = [new BsonDocument { { "_id", 1 }, { "loaded", "automatically" } }] });
    }

    private string ScreenText()
    {
        var cells = _app.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
