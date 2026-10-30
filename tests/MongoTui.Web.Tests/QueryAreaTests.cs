using System.Text.Encodings.Web;
using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Queries;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class QueryAreaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RefreshExecutedQuery_WhenDocumentIsSavedOrDeletedAfterDraftChanges(bool delete)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string script = "db.orders.find({}).toArray()";
        const string draft = "db.orders.deleteMany({})";
        var result = new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] };
        var refreshed = new QueryResult { Kind = QueryResultKind.Documents,
            Documents = delete ? [] : [new BsonDocument { { "_id", 1 }, { "saved", true } }] };
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).Returns(result);
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._)).ReturnsNextFromSequence(result, refreshed);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(result);
        A.CallTo(() => fixture.Gateway.ReplaceOneAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<string>._, A<CancellationToken>._)).Returns(1L);
        A.CallTo(() => fixture.Gateway.DeleteOneAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<CancellationToken>._)).Returns(1L);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);
        await fixture.Workspace.ExecuteAsync(tab);
        tab.Script = draft;
        tab.View = ResultViewMode.Tree;
        using var services = CreateServices(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryArea>(new());
        await renderer.ClickAsync<ResultViewer>("text-button");

        if (delete) { await renderer.ClickAsync<DocumentDialog>("danger"); await renderer.ClickAsync<DocumentDialog>("danger"); }
        else await renderer.ClickAsync<DocumentDialog>("primary");

        A.CallTo(() => fixture.Shell.EvaluateAsync(draft, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
        tab.Error.Should().BeNull();
        tab.Script.Should().Be(draft);
        tab.Result!.Documents.Should().Equal(refreshed.Documents);
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

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

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_ShowTabLimitAndPreserveWorkspace_WhenAiSuggestionCannotOpen(string language)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        for (var i = 0; i < 12; i++) fixture.Workspace.Open(fixture.Profile.Id, "demo", null);
        var tabs = fixture.Workspace.Tabs.ToArray();
        var tab = fixture.Workspace.Selected!;
        const string draft = "db.orders.find({status: 'draft'}).toArray()";
        using var services = CreateServices(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryArea>(new());
        await renderer.ChangeAsync("editor-fallback", draft);
        await renderer.ClickAsync("text-button");

        var act = () => renderer.InvokeAsync<AiPanel>(panel => panel.OnUse.InvokeAsync("db.orders.find().toArray()"));

        await act.Should().NotThrowAsync();
        fixture.Workspace.Tabs.Should().Equal(tabs);
        fixture.Workspace.Selected.Should().BeSameAs(tab);
        tab.Script.Should().Be(draft);
        tab.Error.Should().Be(fixture.Text[LocalizationKeys.Error.TabLimit]);
        (await renderer.AttributeAsync("query-error", "role")).Should().Be("alert");
        (await renderer.TextAsync()).Should().Contain(tab.Error).And.NotContain(fixture.Text[LocalizationKeys.Ai.Use]);
    }

    [Fact]
    public async Task Should_OpenAiSuggestionAndPreserveDraft_WhenOneTabSlotRemains()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        for (var i = 0; i < 11; i++) fixture.Workspace.Open(fixture.Profile.Id, "demo", null);
        var tab = fixture.Workspace.Selected!;
        const string draft = "db.orders.find({status: 'draft'}).toArray()";
        const string suggestion = "db.orders.find({status: 'ready'}).toArray()";
        using var services = CreateServices(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryArea>(new());
        await renderer.ChangeAsync("editor-fallback", draft);
        await renderer.ClickAsync("text-button");

        await renderer.InvokeAsync<AiPanel>(panel => panel.OnUse.InvokeAsync(suggestion));

        fixture.Workspace.Tabs.Should().HaveCount(12).And.Contain(tab);
        var opened = fixture.Workspace.Selected!;
        opened.Should().NotBeSameAs(tab);
        opened.ConnectionId.Should().Be(tab.ConnectionId);
        opened.Database.Should().Be(tab.Database);
        opened.Collection.Should().Be(tab.Collection);
        opened.Script.Should().Be(suggestion);
        opened.Browse.Should().BeFalse();
        opened.Error.Should().BeNull();
        tab.Script.Should().Be(draft);
        (await renderer.TextAsync()).Should().NotContain(fixture.Text[LocalizationKeys.Ai.Use]);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private ServiceProvider CreateServices(WorkspaceFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Workspace);
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(fixture.Gateway);
        services.AddSingleton(new AutocompleteService(fixture.Gateway));
        services.AddSingleton(new DocumentService(fixture.Gateway, fixture.Loggers.CreateLogger<DocumentService>()));
        services.AddSingleton(A.Fake<IAiAssistant>());
        services.AddSingleton<CollectionSchemaService>();
        services.AddSingleton<TranslateQueryService>();
        var js = A.Fake<IJSRuntime>();
        A.CallTo(() => js.InvokeAsync<IJSObjectReference>("import", A<object?[]>._))
            .Throws(new JSException("Use the editor fallback in component tests."));
        services.AddSingleton(js);
        return services.BuildServiceProvider();
    }
}
