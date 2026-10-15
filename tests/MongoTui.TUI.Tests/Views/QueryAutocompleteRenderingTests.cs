using System.Drawing;
using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // The query workspace uses Terminal.Gui's TextView editor.

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryAutocompleteRenderingTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public async Task Should_RenderHighlightedSuggestion_WhenArrowChangesSelection(int presses, bool up)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var runnable = new Runnable<bool>();
        var tabs = CreateTabs();
        runnable.Add(tabs);
        app.Begin(runnable);
        var editor = tabs.ActiveEditorView!;
        editor.SetFocus();
        app.LayoutAndDraw();
        var status = await tabs.CompleteActiveAsync(CancellationToken.None);
        status.Should().Be("Autocomplete.Hint");
        app.LayoutAndDraw();
        var screen = app.Driver!.Contents!;
        var first = FindText(screen, "field00");
        screen[first.Y, first.X].Attribute.Should().Be(editor.Autocomplete.Scheme.Focus);

        for (var count = 0; count < presses; count++)
        {
            editor.NewKeyDownEvent(up ? Key.CursorUp : Key.CursorDown);
            app.LayoutAndDraw();
        }

        var expected = (up ? 12 - presses % 12 : presses) % 12;
        editor.Autocomplete.SelectedIdx.Should().Be(expected);
        var selected = FindText(screen, $"field{expected:D2}");
        screen[selected.Y, selected.X].Attribute.Should().Be(editor.Autocomplete.Scheme.Focus);
        var neighbor = up && expected < 11 ? expected + 1 : expected - 1;
        var unselected = FindText(screen, $"field{neighbor:D2}");
        screen[unselected.Y, unselected.X].Attribute.Should().Be(editor.Autocomplete.Scheme.Normal);
    }

    private static Point FindText(Cell[,] screen, string text)
    {
        for (var row = 0; row < screen.GetLength(0); row++)
        {
            var line = string.Concat(Enumerable.Range(0, screen.GetLength(1)).Select(column => screen[row, column].Grapheme));
            var column = line.IndexOf(text, StringComparison.Ordinal);
            if (column >= 0) return new Point(column, row);
        }
        var contents = string.Join("\n", Enumerable.Range(0, screen.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, screen.GetLength(1)).Select(column => screen[row, column].Grapheme))));
        throw new InvalidOperationException($"Expected text '{text}' was not rendered.\n{contents}");
    }

    private static QueryTabsView CreateTabs()
    {
        var gateway = A.Fake<IMongoGateway>();
        var fields = new BsonDocument(Enumerable.Range(0, 12).Select(index => new BsonElement($"field{index:D2}", index)));
        A.CallTo(() => gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [fields] });
        var query = new QueryService(A.Fake<IMongoShellSessionFactory>(), A.Fake<IQueryHistoryRepository>());
        var tabs = new QueryTabsView(query, A.Fake<ILocalizer>(), new AutocompleteService(gateway))
        {
            Width = 70,
            Height = 24,
        };
        tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost:27017"),
            "app", "users", "db.users.find({ fi");
        tabs.EndInit();
        tabs.ActiveEditorView!.MoveEnd();
        return tabs;
    }
}
