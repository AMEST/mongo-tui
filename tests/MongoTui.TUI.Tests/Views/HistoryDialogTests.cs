using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // Verifies the history's TextView preview.

namespace MongoTui.TUI.Tests.Views;

public sealed class HistoryDialogTests
{
    private readonly IQueryHistoryRepository _history = A.Fake<IQueryHistoryRepository>();
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();
    private readonly ConnectionProfile _first = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost");
    private readonly ConnectionProfile _second = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://other");

    public HistoryDialogTests() => A.CallTo(() => _application.MainThreadId).Returns(Environment.CurrentManagedThreadId);

    [Theory]
    [InlineData("en", true)]
    [InlineData("ru", false)]
    public async Task Should_ShowDetailsAndReturnEntry_WhenQueryIsOpened(string language, bool success)
    {
        _localizer.SetLanguage(language);
        var entry = Entry(_first.Id) with { Success = success };
        A.CallTo(() => _history.GetRecentAsync(_first.Id, 100, A<CancellationToken>._)).Returns(new[] { entry });
        using var dialog = new HistoryDialog(_history, _localizer, _application, [_first, _second], _first.Id);
        await dialog.LoadAsync();

        Field<ListView>(dialog, "_list").InvokeCommand(Command.Accept);

        dialog.SelectedEntry.Should().Be(entry);
        Field<TextView>(dialog, "_details").Text.Should().Be(entry.Script);
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_SwitchConnectionEvenWithDuplicateNames_WhenProfileChanges()
    {
        var entry = Entry(_second.Id);
        A.CallTo(() => _history.GetRecentAsync(_second.Id, 100, A<CancellationToken>._)).Returns(new[] { entry });
        using var dialog = new HistoryDialog(_history, _localizer, _application, [_first, _second], _first.Id);
        await dialog.LoadAsync();

        Field<DropDownList>(dialog, "_connection").Value = "2. local";

        Field<TextView>(dialog, "_details").Text.Should().Be(entry.Script);
        A.CallTo(() => _history.GetRecentAsync(_second.Id, 100, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ShowErrorAndDisableOpen_WhenLoadingFails()
    {
        A.CallTo(() => _history.GetRecentAsync(_first.Id, 100, A<CancellationToken>._)).ThrowsAsync(new IOException("Unavailable"));
        using var dialog = new HistoryDialog(_history, _localizer, _application, [_first]);

        await dialog.LoadAsync();

        dialog.Buttons[0].Enabled.Should().BeFalse();
        Field<Label>(dialog, "_status").Text.Should().Be(_localizer.Get(LocalizationKeys.History.LoadError));
    }

    private static QueryHistoryEntry Entry(ConnectionId id) => new()
    {
        ConnectionId = id, Database = "app", Collection = "users", Script = "db.users.find({ active: true })",
        RanAt = DateTimeOffset.UtcNow, Success = true, ElapsedMs = 42,
    };

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
}
