using System.Text.Encodings.Web;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class DatabaseTreeTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_KeepSavedConnectionsVisibleAndHideDatabases_WhenConnectionOpened(string language)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._)).Returns(new[] { new StoredConnection(fixture.Profile, null) });
        using var services = Services(fixture);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DatabaseTree>(ParameterView.Empty));

        await renderer.Dispatcher.InvokeAsync(() => fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None));

        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        html.Should().Contain(fixture.Profile.Name);
        html.Should().Contain("aria-expanded=\"false\"");
        html.Should().NotContain("tree-row database");
        html.Should().NotContain(Encode(fixture, LocalizationKeys.Tree.NoConnection));
        html.Should().NotContain(Encode(fixture, LocalizationKeys.Connection.Empty));
    }

    [Fact]
    public async Task Should_RenderDatabasesAndKeepCollapsedAfterRefresh_WhenToggled()
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        using var services = Services(fixture);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DatabaseTree>(ParameterView.Empty));
        await renderer.Dispatcher.InvokeAsync(() => fixture.Catalog.ToggleConnection(connection));
        (await renderer.Dispatcher.InvokeAsync(root.ToHtmlString)).Should().Contain("tree-row database");
        await renderer.Dispatcher.InvokeAsync(() => fixture.Catalog.ToggleConnection(connection));

        await renderer.Dispatcher.InvokeAsync(() => fixture.Catalog.RefreshDatabasesAsync(connection, CancellationToken.None));

        (await renderer.Dispatcher.InvokeAsync(root.ToHtmlString)).Should().NotContain("tree-row database");
    }

    private static string Encode(WorkspaceFixture fixture, string key) => HtmlEncoder.Default.Encode(fixture.Text[key]);

    [Fact]
    public async Task Should_ShowRevealedDatabaseAndCollection_WhenSearchIsEmpty()
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, MongoTui.Domain.Primitives.DatabaseName.Create("demo"), A<CancellationToken>._))
            .Returns(new[] { "users" });
        await fixture.Catalog.RevealCollectionAsync(new(fixture.Profile.Id, "demo", "users"), CancellationToken.None);
        using var services = Services(fixture);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DatabaseTree>(ParameterView.Empty));

        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        html.Should().Contain("tree-row database");
        html.Should().Contain("tree-row collection");
        html.Should().Contain("users");
    }

    private static ServiceProvider Services(WorkspaceFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Workspace);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(A.Fake<IJSRuntime>());
        return services.BuildServiceProvider();
    }
}
