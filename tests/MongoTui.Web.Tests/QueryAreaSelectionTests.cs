using AwesomeAssertions;
using FakeItEasy;
using Microsoft.JSInterop;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Queries;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Components.Shared;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class QueryAreaSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_ExecuteAndRecordOnlySelection_WhenRunButtonOrShortcutUsed(bool shortcut)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        const string selected = "db.getSiblingDB('other').customers.find({}).toArray()";
        var execution = new EditorExecution("db.orders.deleteMany({});\n" + selected, selected);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", "stale draft");
        A.CallTo(() => fixture.Shell.EvaluateAsync(selected, "demo", A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] });
        using var services = QueryAreaTests.CreateServices(fixture, Runtime(execution));
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryArea>(new());

        if (shortcut) await renderer.InvokeAsync<CodeEditor>(editor => editor.RunAsync(execution));
        else await renderer.ClickAsync("primary");

        tab.Script.Should().Be(execution.Script);
        tab.EditableCollection!.Database.Value.Should().Be("other");
        tab.EditableCollection.Collection.Value.Should().Be("customers");
        A.CallTo(() => fixture.Shell.EvaluateAsync(selected, "demo", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>.That.Contains("deleteMany"), A<string?>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => fixture.History.AddAsync(A<QueryHistoryEntry>.That.Matches(entry => entry.Script == selected),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Should_UseCapturedDocumentOrWhitespaceSelection_WhenRunClicked(string? selected)
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var execution = new EditorExecution("db.orders.find({}).toArray()", selected);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", "stale draft");
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));
        using var services = QueryAreaTests.CreateServices(fixture, Runtime(execution));
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<QueryArea>(new());

        await renderer.ClickAsync("primary");

        tab.Script.Should().Be(execution.Script);
        A.CallTo(() => fixture.Shell.EvaluateAsync(selected ?? execution.Script, "demo", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static IJSRuntime Runtime(EditorExecution execution)
    {
        var runtime = A.Fake<IJSRuntime>();
        var module = A.Fake<IJSObjectReference>();
        A.CallTo(() => runtime.InvokeAsync<IJSObjectReference>(JavaScriptCalls.Editor.Import, A<object?[]>._))
            .Returns(new ValueTask<IJSObjectReference>(module));
        A.CallTo(() => module.InvokeAsync<EditorExecution>(JavaScriptCalls.Editor.GetExecution, A<object?[]>._))
            .Returns(new ValueTask<EditorExecution>(execution));
        return runtime;
    }
}
