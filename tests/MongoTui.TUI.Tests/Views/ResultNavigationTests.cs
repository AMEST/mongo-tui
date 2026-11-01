using System.Drawing;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.Views;
using Terminal.Gui.Input;

namespace MongoTui.TUI.Tests.Views;

public sealed class ResultNavigationTests
{
    [Theory]
    [InlineData("en", -1, 0)]
    [InlineData("ru", 1, 0)]
    [InlineData("en", 0, -1)]
    [InlineData("ru", 0, 1)]
    public async Task Should_ScrollImmediately_WhenArrowPressedInFocusedResults(string language, int dx, int dy)
    {
        await using var host = new TuiFeatureTestHost(language);
        var tab = host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"));
        tab.Result.Load(new QueryResult { Kind = QueryResultKind.Documents,
            Documents = Enumerable.Range(0, 100).Select(index => new BsonDocument
                { { "_id", index }, { "wide", new string('x', 200) } }).ToArray() });
        host.Tabs.SetResultViewMode(ResultViewMode.Table);
        var result = host.Tabs.ActiveResultView!;
        var actual = Point.Empty;
        var focused = false;
        var key = (dx, dy) switch { (-1, 0) => Key.CursorLeft, (1, 0) => Key.CursorRight,
            (0, -1) => Key.CursorUp, _ => Key.CursorDown };
        host.App.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            result.SetFocus();
            result.ScrollTo(new Point(5, 5));
            host.App.Keyboard.RaiseKeyDownEvent(key);
            host.App.LayoutAndDraw();
            actual = result.Viewport.Location;
            focused = result.HasFocus;
            host.App.RequestStop(host.Window);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(3), () => { host.App.RequestStop(host.Window); return false; });

        host.App.Run(host.Window);

        actual.Should().Be(new Point(5 + dx, 5 + dy));
        focused.Should().BeTrue();
        result.HorizontalScrollBar.Value.Should().Be(actual.X);
        result.VerticalScrollBar.Value.Should().Be(actual.Y);
        ((ResultTextView)result).DocumentRow.Should().BeInRange(actual.Y, actual.Y + result.Viewport.Height - 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task Should_KeepFocusAndText_WhenArrowsReachContentEdges(string text)
    {
        await using var host = new TuiFeatureTestHost("en");
        host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"));
        var tab = host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"));
        var result = host.Tabs.ActiveResultView!;
        result.Text = text;
        host.App.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            result.SetFocus();
            foreach (var key in new[] { Key.CursorUp, Key.CursorDown, Key.CursorLeft, Key.CursorRight })
                for (var press = 0; press < 30; press++) host.App.Keyboard.RaiseKeyDownEvent(key);
            host.App.RequestStop(host.Window);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(3), () => { host.App.RequestStop(host.Window); return false; });

        host.App.Run(host.Window);

        result.HasFocus.Should().BeTrue();
        result.Viewport.Location.Should().Be(Point.Empty);
        result.Text.Should().Be(text);
        host.Tabs.ActiveTab.Should().BeSameAs(tab);
    }

    [Fact]
    public async Task Should_ScrollWideContent_WhenCaretIsOnShortLine()
    {
        await using var host = new TuiFeatureTestHost("ru");
        host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"));
        var result = host.Tabs.ActiveResultView!;
        const string prefix = "left-marker";
        result.Text = "x\n" + prefix + new string('x', 200);
        host.App.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            result.SetFocus();
            result.MoveHome();
            for (var press = 0; press < 20; press++) host.App.Keyboard.RaiseKeyDownEvent(Key.CursorRight);
            host.App.LayoutAndDraw();
            host.App.RequestStop(host.Window);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(3), () => { host.App.RequestStop(host.Window); return false; });

        host.App.Run(host.Window);

        result.Viewport.X.Should().Be(20);
        result.CurrentRow.Should().Be(0);
        result.Text.Should().StartWith("x\n" + prefix);
    }

    [Fact]
    public async Task Should_SelectTextWithoutEditing_WhenShiftArrowPressed()
    {
        await using var host = new TuiFeatureTestHost("en");
        host.Tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost"));
        var result = host.Tabs.ActiveResultView!;
        result.Text = "alpha beta";
        host.App.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            result.SetFocus();
            result.MoveHome();
            for (var press = 0; press < 5; press++) host.App.Keyboard.RaiseKeyDownEvent(Key.CursorRight.WithShift);
            host.App.RequestStop(host.Window);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(3), () => { host.App.RequestStop(host.Window); return false; });

        host.App.Run(host.Window);

        result.SelectedText.Should().Be("alpha");
        result.Text.Should().Be("alpha beta");
        result.HasFocus.Should().BeTrue();
    }
}
