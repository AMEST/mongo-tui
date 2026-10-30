using System.Text.Encodings.Web;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class CollectionPanelTests
{
    [Theory]
    [InlineData("en", "unique")]
    [InlineData("ru", "уникальный")]
    public async Task Should_LocalizeIndexBadgeAndProtectIdIndex_When_CollectionDetailsLoaded(string language, string badge)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.ListIndexesAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<CancellationToken>._))
            .Returns(new[] { new IndexInfo { Name = "_id_", Unique = true, Fields = [new IndexField("_id", IndexDirection.Ascending)] } });
        A.CallTo(() => fixture.Gateway.GetCollectionStatsAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<CancellationToken>._))
            .Returns(new CollectionStats { Name = "items", DocumentCount = 1, IndexCount = 1 });
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton(A.Fake<IJSRuntime>());
        registrations.AddSingleton(fixture.Text);
        registrations.AddSingleton(fixture.Catalog);
        registrations.AddSingleton(new IndexService(fixture.Gateway));
        using var services = registrations.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<CollectionPanel>(
            ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(CollectionPanel.Tab)] = new WebTab(fixture.Profile.Id, "demo", "items") })));

        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        html.Should().Contain(HtmlEncoder.Default.Encode(badge)).And.Contain("_id_");
        html.Should().Contain("class=\"icon-button\" disabled");
        A.CallTo(() => fixture.Gateway.DropIndexAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
