using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Localization;

namespace MongoTui.Web.Tests;

public sealed class ExplainDialogTests
{
    [Theory]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    public async Task Should_RenderPlanOrUnsupportedHint_WhenDialogOpens(string language, bool supported)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var text = new WebText(); text.SetLanguage(language);
        A.CallTo(() => gateway.ExplainAsync(A<MongoConnectionTarget>._, A<ExplainRequest>._, A<CancellationToken>._))
            .Returns(BsonDocument.Parse("{queryPlanner:{winningPlan:{stage:'COLLSCAN'}}}"));
        using var services = Services(gateway, text);
        await using var renderer = new ComponentTestRenderer(services);

        await renderer.MountAsync<ExplainDialog>(new() { [nameof(ExplainDialog.Context)] = Context(supported ? "db.orders.find({})" : "db.orders.deleteMany({})") });

        (await renderer.TextAsync()).Should().Contain(supported ? "COLLSCAN" : text["Explain.Unsupported"]);
        if (!supported) A.CallTo(() => gateway.ExplainAsync(A<MongoConnectionTarget>._, A<ExplainRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
    [Fact]
    public async Task Should_CancelServerRequest_WhenDialogCloses()
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = CancellationToken.None;
        A.CallTo(() => gateway.ExplainAsync(A<MongoConnectionTarget>._, A<ExplainRequest>._, A<CancellationToken>._)).ReturnsLazily(async call =>
        {
            observed = call.GetArgument<CancellationToken>(2); started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, observed); return new BsonDocument();
        });
        using var services = Services(gateway, new WebText());
        await using var renderer = new ComponentTestRenderer(services);
        var mount = renderer.MountAsync<ExplainDialog>(new() { [nameof(ExplainDialog.Context)] = Context("db.orders.find({})") });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await renderer.CloseAsync();

        await mount.WaitAsync(TimeSpan.FromSeconds(3));
        observed.IsCancellationRequested.Should().BeTrue();
    }
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RunEditedQuery_WhenUserCorrectsScriptInModal(string language)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var text = new WebText(); text.SetLanguage(language);
        A.CallTo(() => gateway.ExplainAsync(A<MongoConnectionTarget>._, A<ExplainRequest>._, A<CancellationToken>._))
            .Returns(BsonDocument.Parse("{queryPlanner:{winningPlan:{stage:'IXSCAN',indexName:'active_customers'}}}"));
        using var services = Services(gateway, text); await using var renderer = new ComponentTestRenderer(services);
        var context = Context("const value = 1;");
        await renderer.MountAsync<ExplainDialog>(new() { [nameof(ExplainDialog.Context)] = context });
        await renderer.InputAsync("#explain-query", "// edited query\ndb.customers.find({active:true}).toArray()");

        await renderer.ClickAsync("#explain-run");

        (await renderer.TextAsync()).Should().Contain("IXSCAN").And.Contain("active_customers").And.NotContain(text["Explain.Unsupported"]);
        A.CallTo(() => gateway.ExplainAsync(context.Target, A<ExplainRequest>.That.Matches(request =>
            request.Collection.Collection.Value == "customers" && request.Filter!["active"].AsBoolean), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        context.Script.Should().Be("const value = 1;");
    }
    private static ServiceProvider Services(IMongoDiagnosticsGateway gateway, WebText text)
    {
        var registrations = new ServiceCollection(); registrations.AddLogging(); registrations.AddSingleton(text);
        registrations.AddSingleton(A.Fake<IJSRuntime>()); var settings = A.Fake<ISettingsStore>();
        registrations.AddSingleton(new ExplainService(gateway, new SettingsService(settings), new DiagnosticsSettingsService(settings)));
        return registrations.BuildServiceProvider();
    }
    private static QueryExecutionContext Context(string script) => new()
    { TabId = Guid.NewGuid(), ConnectionId = ConnectionId.New(), Target = new("mongodb://localhost"), Database = "shop", Script = script };
}
