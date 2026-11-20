using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class QueryAreaRawTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", true)]
    public async Task Should_ShowLiveOutputAndAllowSwitching_WhenScriptRunsOnce(string language, bool fails)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string script = "db.orders.find({}).toArray()";
        const string log = "  live-progress\n";
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);
        var started = new TaskCompletionSource<ScriptOutput>(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<QueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new QueryResult { Kind = QueryResultKind.Documents,
            Documents = [new BsonDocument { { "_id", 1 }, { "name", "result-document" } }] };
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<ScriptOutput?>._, A<CancellationToken>._))
            .ReturnsLazily(call =>
            {
                started.SetResult(call.GetArgument<ScriptOutput>(2)!);
                return response.Task.WaitAsync(call.GetArgument<CancellationToken>(3));
            });
        using var services = QueryAreaTests.CreateServices(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryArea>(new());

        var pending = renderer.ClickAsync("primary");
        try
        {
            var output = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await renderer.ClickAsync("#result-raw");
            await output.AppendAsync(new(log, false));
            await WaitForOutputAsync(renderer, log);
            tab.Busy.Should().BeTrue();
            await renderer.ClickAsync("#result-table");
            if (fails) response.SetException(new QueryExecutionException("migration failed"));
        }
        finally
        {
            response.TrySetResult(result);
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }

        tab.View.Should().Be(ResultViewMode.Table);
        if (fails)
        {
            (await renderer.TextAsync()).Should().Contain("migration failed");
            await renderer.ClickAsync("#open-query-output");
            (await renderer.AttributeAsync("#export-query-results", "disabled")).Should().BeNull();
        }
        else
        {
            (await renderer.TextAsync()).Should().Contain("result-document");
            await renderer.ClickAsync("#result-raw");
        }
        (await renderer.TextAsync()).Should().Contain(log);
        A.CallTo(() => fixture.Shell.EvaluateAsync(script, "demo", A<ScriptOutput?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static async Task WaitForOutputAsync(ComponentTestRenderer renderer, string output)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!(await renderer.TextAsync()).Contains(output, StringComparison.Ordinal))
            await Task.Delay(20, timeout.Token);
    }
}
