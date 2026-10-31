using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class QueryTabsViewContextTests : IDisposable
{
    private readonly IApplication _application;
    private readonly Runnable<bool> _window = new();
    private readonly ResxLocalizer _localizer = new();
    private readonly QueryTabsView _tabs;

    public QueryTabsViewContextTests()
    {
        _application = Terminal.Gui.App.Application.Create();
        _application.Init(DriverRegistry.Names.ANSI);
        _localizer.SetLanguage("en");
        _tabs = new QueryTabsView(QueryTestServices.Create(A.Fake<IMongoShellSessionFactory>(),
            A.Fake<IQueryHistoryRepository>()), _localizer, new AutocompleteService(A.Fake<IMongoGateway>()))
        {
            Width = Dim.Fill(), Height = Dim.Fill(),
        };
        _window.Add(_tabs);
    }

    public void Dispose()
    {
        _window.Dispose();
        _application.Dispose();
    }

    [Theory]
    [InlineData("en", "Connection: Production / Database: orders")]
    [InlineData("ru", "Подключение: Production / База: orders")]
    public void Should_ShowOwnConnectionAndDatabase_WhenSwitchingTabs(string language, string expected)
    {
        _localizer.SetLanguage(language);
        var first = _tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost/"),
            "orders", connectionName: "Production");
        var firstView = Host.Value;
        _tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://other/"),
            "catalog", connectionName: "Staging");
        Show();

        Host.Value = firstView;
        _application.LayoutAndDraw();

        _tabs.ActiveTab.Should().BeSameAs(first);
        ScreenText().Should().Contain(expected).And.NotContain("Staging").And.NotContain("catalog");
        Context.Frame.Bottom.Should().BeLessThanOrEqualTo(_tabs.ActiveEditorView!.SuperView!.Frame.Y);
    }

    [Theory]
    [InlineData("orders", "mongodb://alice:secretpass@localhost/catalog?authSource=admin", "orders")]
    [InlineData(null, "mongodb://alice:secretpass@localhost/catalog?authSource=admin", "catalog")]
    [InlineData(null, "mongodb://alice:secretpass@localhost/?authSource=admin", "test")]
    [InlineData(null, "mongodb://localhost:27017", "test")]
    public void Should_ShowEffectiveDatabaseWithoutCredentials_WhenTabIsOpened(string? database, string uri, string expected)
    {
        _tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget(uri), database, connectionName: "Production");

        Show();

        ScreenText().Should().Contain($"Connection: Production / Database: {expected}")
            .And.NotContain("alice").And.NotContain("secretpass").And.NotContain("mongodb://").And.NotContain("admin");
    }

    [Fact]
    public void Should_TranslateContext_WhenLanguageChanges()
    {
        _tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost/"),
            "orders", connectionName: "Production");
        Show();
        _localizer.SetLanguage("ru");

        _tabs.RefreshLocalization();
        _application.LayoutAndDraw();

        ScreenText().Should().Contain("Подключение: Production / База: orders");
    }

    [Fact]
    public void Should_KeepEditorBelowContext_WhenNamesExceedWidth()
    {
        _tabs.AddTab(ConnectionId.New(), new MongoConnectionTarget("mongodb://localhost/"),
            new string('d', 120), connectionName: new string('c', 120));

        Show();

        Context.Frame.Height.Should().Be(1);
        Context.Frame.Right.Should().BeLessThanOrEqualTo(Context.SuperView!.Viewport.Width);
        Context.Frame.Bottom.Should().BeLessThanOrEqualTo(_tabs.ActiveEditorView!.SuperView!.Frame.Y);
        _tabs.ActiveEditorView.Frame.Height.Should().BeGreaterThan(0);
    }

    private Tabs Host => _tabs.SubViews.OfType<Tabs>().Single();
    private Label Context => ((View)Host.Value!).SubViews.OfType<Label>().Single();

    private void Show()
    {
        _application.Begin(_window);
        _application.LayoutAndDraw();
    }

    private string ScreenText()
    {
        var cells = _application.Driver!.Contents!;
        return string.Join("\n", Enumerable.Range(0, cells.GetLength(0)).Select(row =>
            string.Concat(Enumerable.Range(0, cells.GetLength(1)).Select(column => cells[row, column].Grapheme))));
    }
}
