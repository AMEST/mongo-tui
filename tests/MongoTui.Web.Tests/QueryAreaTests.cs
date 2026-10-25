using System.Text.Encodings.Web;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MongoTui.Application.Localization;
using MongoTui.Application.Services;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class QueryAreaTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_ShowCollectionPrompt_WhenConnectionOpensAfterPanelRendered(string language)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        using var services = CreateServices(fixture);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<QueryArea>(ParameterView.Empty));

        await renderer.Dispatcher.InvokeAsync(() => fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None));

        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        html.Should().Contain(HtmlEncoder.Default.Encode(fixture.Text[LocalizationKeys.Workspace.SelectCollection]));
        html.Should().Contain(HtmlEncoder.Default.Encode(fixture.Text[LocalizationKeys.Workspace.SelectHint]));
        html.Should().NotContain(HtmlEncoder.Default.Encode(fixture.Text[LocalizationKeys.Workspace.Connect]));
        html.Should().NotContain("<button");
        fixture.Workspace.Selected.Should().BeNull();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_ShowConnectPrompt_WhenLastConnectionCloses(string language)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        using var services = CreateServices(fixture);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<QueryArea>(ParameterView.Empty));

        await renderer.Dispatcher.InvokeAsync(() => fixture.Catalog.DisconnectAsync(fixture.Profile.Id));

        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        html.Should().Contain(HtmlEncoder.Default.Encode(fixture.Text[LocalizationKeys.Workspace.Connect]));
        html.Should().Contain(HtmlEncoder.Default.Encode(fixture.Text[LocalizationKeys.Connection.Add]));
        html.Should().NotContain(HtmlEncoder.Default.Encode(fixture.Text[LocalizationKeys.Workspace.SelectCollection]));
    }

    private ServiceProvider CreateServices(WorkspaceFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Workspace);
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(new AutocompleteService(fixture.Gateway));
        services.AddSingleton(A.Fake<IJSRuntime>());
        return services.BuildServiceProvider();
    }
}
