using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Localization;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class ResultViewerModeTests
{
    [Theory]
    [InlineData("en", ResultViewMode.Table, "data-table")]
    [InlineData("ru", ResultViewMode.Table, "data-table")]
    [InlineData("en", ResultViewMode.Tree, "tree-results")]
    [InlineData("ru", ResultViewMode.Tree, "tree-results")]
    [InlineData("en", ResultViewMode.Json, "json-result")]
    [InlineData("ru", ResultViewMode.Json, "json-result")]
    public async Task Should_RenderSelectedModeWithOriginalBsonTypes_When_ResultDisplayed(string language, ResultViewMode mode, string expectedClass)
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        var text = new WebText();
        text.SetLanguage(language);
        registrations.AddSingleton(text);
        using var services = registrations.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var tab = new WebTab(ConnectionId.New(), "demo", "items")
        {
            View = mode,
            Result = new QueryResult { Kind = QueryResultKind.Documents,
                Documents = [new BsonDocument { { "_id", new BsonInt64(42) }, { "name", "alpha" } }] },
        };

        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<ResultViewer>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ResultViewer.Tab)] = tab })));

        var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        html.Should().Contain(expectedClass).And.Contain("alpha");
        if (mode == ResultViewMode.Json) html.Should().Contain("$numberLong");
    }
}
