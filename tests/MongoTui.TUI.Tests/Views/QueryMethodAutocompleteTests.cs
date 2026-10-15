using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // The query workspace uses Terminal.Gui's TextView editor.

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryMethodAutocompleteTests
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();

    [Theory]
    [InlineData("db.get")]
    [InlineData("db.getSiblingDB('app').get")]
    public async Task Should_ListDatabaseMethodsWithoutIo_WhenPrefixMatches(string script)
    {
        using var tabs = CreateTabs(script);

        var status = await tabs.CompleteActiveAsync(CancellationToken.None);

        status.Should().Be("Autocomplete.Hint");
        tabs.ActiveEditorView!.Autocomplete.Suggestions.Select(item => item.Title).Should().Equal("getSiblingDB", "getCollection");
        Fake.GetCalls(_gateway).Should().BeEmpty();
    }

    [Theory]
    [InlineData("db.getS|", "getSiblingDB", "db.getSiblingDB(\"|\")")]
    [InlineData("db.getC|", "getCollection", "db.getCollection(\"|\")")]
    [InlineData("db.getCollection('items').fi|", "find", "db.getCollection('items').find({|})")]
    [InlineData("db.items.findO|", "findOne", "db.items.findOne({|})")]
    [InlineData("db.items.find({}).li|", "limit", "db.items.find({}).limit(|)")]
    [InlineData("db.items.find({}).sk|", "skip", "db.items.find({}).skip(|)")]
    [InlineData("db.items.find({}).so|", "sort", "db.items.find({}).sort({|})")]
    [InlineData("db.items.find({}).toA|", "toArray", "db.items.find({}).toArray()|")]
    [InlineData("db.items.find({}).co|", "count", "db.items.find({}).count()|")]
    [InlineData("db.items.find({}).limit(5).toA|", "toArray", "db.items.find({}).limit(5).toArray()|")]
    [InlineData("db.getColle|cton('items').find({})", "getCollection", "db.getCollection|('items').find({})")]
    [InlineData("db.items.find({}).li|mt(5).toArray()", "limit", "db.items.find({}).limit|(5).toArray()")]
    public async Task Should_InsertMethodAndPositionCaret_WhenSuggestionAccepted(string marked, string method, string expected)
    {
        using var tabs = CreateTabs(marked.Replace("|", string.Empty, StringComparison.Ordinal), marked.IndexOf('|'));
        await tabs.CompleteActiveAsync(CancellationToken.None);
        var editor = tabs.ActiveEditorView!;
        editor.Autocomplete.SelectedIdx = editor.Autocomplete.Suggestions.ToList().FindIndex(item => item.Title == method);

        var accepted = editor.Autocomplete.ProcessKey(Key.Enter);

        accepted.Should().BeTrue();
        editor.Text.Should().Be(expected.Replace("|", string.Empty, StringComparison.Ordinal));
        editor.CurrentColumn.Should().Be(expected.IndexOf('|'));
        tabs.ActiveTab!.Script.Should().Be(editor.Text);
    }

    [Theory]
    [InlineData("db.ge", 't', "getSiblingDB,getCollection")]
    [InlineData("db.get", 'C', "getCollection")]
    [InlineData("db.getCollection('items')", '.', "find,findOne,countDocuments,estimatedDocumentCount")]
    [InlineData("db.items.find({})", '.', "limit,skip,sort,toArray,count")]
    [InlineData("db.items.find({}).limit(5)", '.', "limit,skip,sort,toArray,count")]
    public void Should_OpenPopupAutomatically_WhenTypingMethodPrefixOrDot(string script, char character, string methods)
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var runnable = new Runnable<bool>();
        var tabs = CreateTabs(script);
        tabs.Width = 70;
        tabs.Height = 20;
        runnable.Add(tabs);
        app.Begin(runnable);
        var editor = tabs.ActiveEditorView!;
        editor.SetFocus();

        editor.NewKeyDownEvent(new Key(character));

        app.LayoutAndDraw();
        editor.Text.Should().Be(script + character);
        editor.Autocomplete.Visible.Should().BeTrue();
        editor.Autocomplete.Suggestions.Select(item => item.Title).Should().Equal(methods.Split(','));
        Fake.GetCalls(_gateway).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReopenCursorMethods_WhenDotTypedAfterAcceptedLimit()
    {
        using IApplication app = Terminal.Gui.App.Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        using var runnable = new Runnable<bool>();
        var tabs = CreateTabs("db.items.find({}).li");
        tabs.Width = 70;
        tabs.Height = 20;
        runnable.Add(tabs);
        app.Begin(runnable);
        var editor = tabs.ActiveEditorView!;
        editor.SetFocus();
        await tabs.CompleteActiveAsync(CancellationToken.None);
        editor.Autocomplete.ProcessKey(Key.Enter);
        editor.InsertText("5");
        editor.MoveEnd();

        editor.NewKeyDownEvent(new Key('.'));

        app.LayoutAndDraw();
        editor.Text.Should().Be("db.items.find({}).limit(5).");
        editor.Autocomplete.Visible.Should().BeTrue();
        editor.Autocomplete.Suggestions.Should().Contain(item => item.Title == "toArray");
    }

    [Theory]
    [InlineData("db.items.find({}).toArray()")]
    [InlineData("db.items.find({}).count()")]
    [InlineData("db.items.find({name: 'value")]
    [InlineData("// db.items.find({})")]
    public void ShouldNot_OpenMethodPopup_WhenTypingDotOutsideSupportedChain(string script)
    {
        using var tabs = CreateTabs(script);
        var editor = tabs.ActiveEditorView!;

        editor.NewKeyDownEvent(new Key('.'));

        editor.Autocomplete.Suggestions.Should().BeEmpty();
        editor.Autocomplete.Visible.Should().BeFalse();
    }

    [Fact]
    public async Task Should_CloseMethodPopupWithoutChangingQuery_WhenEscapePressed()
    {
        const string script = "db.items.find({}).";
        using var tabs = CreateTabs(script);
        await tabs.CompleteActiveAsync(CancellationToken.None);

        tabs.ActiveEditorView!.Autocomplete.ProcessKey(Key.Esc);

        tabs.ActiveEditorView.Text.Should().Be(script);
        tabs.ActiveEditorView.Autocomplete.Visible.Should().BeFalse();
    }

    private QueryTabsView CreateTabs(string script, int? column = null)
    {
        var service = new QueryService(A.Fake<IMongoShellSessionFactory>(), A.Fake<IQueryHistoryRepository>());
        var tabs = new QueryTabsView(service, A.Fake<ILocalizer>(), new AutocompleteService(_gateway));
        tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost:27017"), script: script);
        tabs.EndInit();
        tabs.ActiveEditorView!.MoveEnd();
        if (column is { } position) tabs.ActiveEditorView.InsertionPoint = new System.Drawing.Point(position, 0);
        tabs.ActiveEditorView.SetFocus();
        return tabs;
    }
}
