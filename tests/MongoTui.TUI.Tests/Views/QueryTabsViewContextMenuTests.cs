using MongoTui.Application.Localization;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView is superseded by the external Editor package.

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryTabsViewContextMenuTests
{
    [Fact]
    public void Should_AddRunItem_ToEditorContextMenu()
    {
        var tabs = CreateTabs();

        var subViews = tabs.ActiveEditorView!.ContextMenu!.Root!.SubViews.ToList();

        subViews[^2].Should().BeOfType<Line>();
        subViews[^1].Should().BeOfType<MenuItem>().Which.Title.Should().Be(LocalizationKeys.Menu.QueryRun);
    }

    [Fact]
    public void Should_AddCustomItems_ToResultContextMenu()
    {
        var tabs = CreateTabs();

        var menuItems = tabs.ActiveResultView!.ContextMenu!.Root!.SubViews
            .OfType<MenuItem>()
            .Select(item => item.Title)
            .ToList();

        menuItems.TakeLast(5).Should().Equal(
            LocalizationKeys.Result.CopyAll,
            LocalizationKeys.Result.CopyDocument,
            LocalizationKeys.Result.Export,
            LocalizationKeys.Menu.DocumentEdit,
            LocalizationKeys.Menu.DocumentDelete);

        tabs.ActiveResultView.ContextMenu!.Root!.SubViews
            .Should().ContainSingle(item => item is Line);
    }

    private static QueryTabsView CreateTabs()
    {
        var localizer = A.Fake<ILocalizer>();
        A.CallTo(() => localizer.Get(A<string>._)).ReturnsLazily((string key) => key);
        var queryService = QueryTestServices.Create(
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