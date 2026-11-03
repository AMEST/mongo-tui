using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Models;
using MongoTui.Domain.Queries;
using MongoTui.Web.Components.Panels;

namespace MongoTui.Web.Tests;

public sealed class QueryAreaRawTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_ShowNativeOutput_WhenRawSelectedBeforeRunningScript(string language)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string script = "console.log('{not JSON}'); console.log('[finished]');";
        const string output = "  {not JSON}\n[finished]\n\n";
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", script);
        A.CallTo(() => fixture.Shell.EvaluateRawAsync(script, "demo", A<CancellationToken>._))
            .ReturnsLazily(async () =>
            {
                await Task.Delay(30).ConfigureAwait(false);
                return new QueryResult { Kind = QueryResultKind.Raw, Documents = [], RawOutput = output };
            });
        using var services = QueryAreaTests.CreateServices(fixture);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryArea>(new());
        await renderer.ClickAsync("#result-raw");

        await renderer.ClickAsync("primary").WaitAsync(TimeSpan.FromSeconds(5));

        tab.View.Should().Be(ResultViewMode.Raw);
        tab.Error.Should().BeNull();
        tab.Busy.Should().BeFalse();
        tab.HasNext.Should().BeFalse();
        tab.EditableCollection.Should().BeNull();
        (await renderer.TextAsync()).Should().Contain(output);
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_KeepSubmittedOutputMode_WhenViewChangesWhileQueued()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", null, "console.log('done')");
        tab.View = ResultViewMode.Raw;
        A.CallTo(() => fixture.Shell.EvaluateRawAsync(tab.Script, "demo", A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Raw, Documents = [], RawOutput = "done\n" });
        await fixture.Limiter.EnterAsync(CancellationToken.None);
        await fixture.Limiter.EnterAsync(CancellationToken.None);

        var pending = fixture.Workspace.ExecuteScriptAsync(tab, tab.Script);
        tab.View = ResultViewMode.Json;
        fixture.Limiter.Exit();
        try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { fixture.Limiter.Exit(); }

        tab.Error.Should().BeNull();
        tab.Result!.RawOutput.Should().Be("done\n");
        A.CallTo(() => fixture.Shell.EvaluateRawAsync(tab.Script, "demo", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
