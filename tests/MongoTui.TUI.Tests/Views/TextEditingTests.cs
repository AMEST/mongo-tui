using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class TextEditingTests
{
    [Theory]
    [InlineData("en", "ru", "Clear all text", "Очистить текст")]
    [InlineData("ru", "en", "Очистить текст", "Clear all text")]
    public async Task Should_TranslateNativeAndQueryMenus_WhenLanguageChanges(string initial, string next, string before, string after)
    {
        await using var host = new TuiFeatureTestHost(initial);
        host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"));
        host.App.Begin(host.Window);
        host.App.LayoutAndDraw();
        var editor = host.Tabs.ActiveEditorView!;
        var clear = editor.ContextMenu!.Root!.SubViews.OfType<MenuItem>().Single(item => item.Command == Command.DeleteAll);
        clear.Title.Should().Be(before);

        host.Localizer.SetLanguage(next);
        host.Tabs.RefreshLocalization();

        clear.Title.Should().Be(after);
        clear.HelpText.Should().Be(host.Localizer.Get(LocalizationKeys.TextEditing.ClearHelp));
        clear.TargetView.Should().BeSameAs(editor);
        editor.ContextMenu.Root.SubViews.OfType<MenuItem>().Last().Title.Should().Be(host.Localizer.Get(LocalizationKeys.Menu.QueryRun));
        host.Tabs.ActiveResultView!.ContextMenu!.Root!.SubViews.OfType<MenuItem>()
            .Single(item => item.Command == Command.Copy).Title.Should().Be(host.Localizer.Get(LocalizationKeys.TextEditing.Copy));
    }

    [Fact]
    public async Task Should_ShowAndUseHorizontalScrollbar_WhenTableExceedsViewport()
    {
        await using var host = new TuiFeatureTestHost("en");
        var tab = host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"));
        tab.Result.Load(new QueryResult { Kind = QueryResultKind.Documents,
            Documents = [new BsonDocument("wide", new string('x', 200) + "end-marker")] });
        host.Tabs.SetResultViewMode(ResultViewMode.Table);
        host.App.Begin(host.Window);
        host.App.LayoutAndDraw();
        var result = host.Tabs.ActiveResultView!;
        result.HorizontalScrollBar.Visible.Should().BeTrue();
        result.SetFocus();
        result.NewKeyDownEvent(Key.CursorDown);
        result.NewKeyDownEvent(Key.CursorDown);

        host.App.Keyboard.RaiseKeyDownEvent(Key.End);
        host.App.LayoutAndDraw();

        result.Viewport.X.Should().BeGreaterThan(0);
        result.CurrentRow.Should().Be(2);
        tab.Result.DocumentIndexAtRow(result.CurrentRow).Should().Be(0);
        var screen = host.App.Driver!.Contents!;
        string.Join('\n', Enumerable.Range(0, screen.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, screen.GetLength(1)).Select(column => screen[row, column].Grapheme))))
            .Should().Contain("end-marker");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Should_MoveOrSelectWord_WhenOptionArrowReceived(bool right, bool extend)
    {
        await using var host = new TuiFeatureTestHost("en");
        host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"), script: "alpha beta");
        host.App.Begin(host.Window);
        host.App.LayoutAndDraw();
        var editor = host.Tabs.ActiveEditorView!;
        editor.SetFocus();
        if (right) editor.MoveHome(); else editor.MoveEnd();
        var previous = editor.CurrentColumn;
        var key = (right ? Key.CursorRight : Key.CursorLeft).WithAlt;
        if (extend) key = key.WithShift;

        host.App.Keyboard.RaiseKeyDownEvent(key);

        if (right) editor.CurrentColumn.Should().BeGreaterThan(previous);
        else editor.CurrentColumn.Should().BeLessThan(previous);
        (editor.SelectedLength > 0).Should().Be(extend);
        editor.HasFocus.Should().BeTrue();
    }
}
