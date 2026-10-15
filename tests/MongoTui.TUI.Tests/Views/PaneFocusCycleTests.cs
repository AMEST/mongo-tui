using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Tests.Views;

public sealed class PaneFocusCycleTests
{
    [Fact]
    public void Should_ReturnEditor_When_AdvancingFromTree()
    {
        var (tree, editor, result, area) = BuildPanes();

        var target = PaneFocusCycle.Resolve(tree, [tree, editor, result], area, forward: true);

        target.Should().BeSameAs(editor);
    }

    [Fact]
    public void Should_WrapToTree_When_AdvancingFromResult()
    {
        var (tree, editor, result, area) = BuildPanes();

        var target = PaneFocusCycle.Resolve(result, [tree, editor, result], area, forward: true);

        target.Should().BeSameAs(tree);
    }

    [Fact]
    public void Should_WrapToResult_When_GoingBackwardFromTree()
    {
        var (tree, editor, result, area) = BuildPanes();

        var target = PaneFocusCycle.Resolve(tree, [tree, editor, result], area, forward: false);

        target.Should().BeSameAs(result);
    }

    [Fact]
    public void Should_ReturnTree_When_AdvancingFromQueryAreaContainer()
    {
        var (tree, editor, result, area) = BuildPanes();

        var target = PaneFocusCycle.Resolve(area, [tree, editor, result], area, forward: true);

        target.Should().BeSameAs(tree);
    }

    [Fact]
    public void Should_ReturnTree_When_AdvancingFromDetachedTabHeader()
    {
        var tabs = CreateTabs();
        var tree = new ListView();
        var header = TabHeader(tabs);

        var target = PaneFocusCycle.Resolve(
            header,
            [tree, tabs.ActiveEditorView!, tabs.ActiveResultView!],
            tabs,
            forward: true);

        target.Should().BeSameAs(tree);
    }

    [Fact]
    public void Should_ReturnNull_When_FocusIsOutsidePanes()
    {
        var (tree, editor, result, area) = BuildPanes();
        var unrelated = new View();

        var target = PaneFocusCycle.Resolve(unrelated, [tree, editor, result], area, forward: true);

        target.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnNull_When_LessThanTwoPanes()
    {
        var tree = new ListView();

        var target = PaneFocusCycle.Resolve(tree, [tree], new View(), forward: true);

        target.Should().BeNull();
    }

    private static (ListView Tree, TextView Editor, TextView Result, View Area) BuildPanes()
    {
        var tree = new ListView();
        var editor = new TextView { Multiline = true };
        var result = new TextView { Multiline = true, ReadOnly = true };
        var area = new View();

        return (tree, editor, result, area);
    }

    private static View TabHeader(QueryTabsView tabs)
    {
        var container = tabs.ActiveEditorView!.SuperView!.SuperView!;
        var border = (BorderView)container.Border.View!;

        return border.TitleView!;
    }

    private static QueryTabsView CreateTabs()
    {
        var localizer = A.Fake<ILocalizer>();
        A.CallTo(() => localizer.Get(A<string>._)).ReturnsLazily((string key) => key);
        var queryService = new QueryService(
            A.Fake<IMongoShellSessionFactory>(),
            A.Fake<IQueryHistoryRepository>());

        var tabs = new QueryTabsView(queryService, localizer, new AutocompleteService(A.Fake<IMongoGateway>()));
        tabs.AddTab(
            ConnectionId.From(Guid.NewGuid()),
            new MongoConnectionTarget("mongodb://localhost:27017"),
            "database",
            "collection");

        tabs.EndInit();

        return tabs;
    }
}
