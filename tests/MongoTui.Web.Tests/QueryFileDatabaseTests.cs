using AwesomeAssertions;
using System.Reflection;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Components;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class QueryFileDatabaseTests
{
    [Fact]
    public async Task Should_OpenInRequestedDatabase_WhenFileLoadedByWorkbench()
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._))
            .Returns(new[] { new StoredConnection(fixture.Profile, null) });
        await fixture.Catalog.RefreshAsync(CancellationToken.None);
        var store = A.Fake<IQueryFileStore>();
        A.CallTo(() => store.ReadAsync("orders.js", A<CancellationToken>._)).Returns("db.orders.find({})");
        using var workbench = new Workbench();
        var dependencies = new Dictionary<string, object>
        {
            ["Files"] = new QueryFileService(store, fixture.Settings, NullLogger<QueryFileService>.Instance),
            ["Catalog"] = fixture.Catalog, ["Workspace"] = fixture.Workspace, ["L"] = fixture.Text,
        };
        foreach (var (name, value) in dependencies)
            typeof(Workbench).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(workbench, value);

        await (Task)typeof(Workbench).GetMethod("OpenQueryFileAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(workbench, [new QueryFileOpenRequest(fixture.Profile.Id, "orders.js", "shop")])!;

        fixture.Workspace.Selected.Should().NotBeNull();
        fixture.Workspace.Selected!.Database.Should().Be("shop");
        fixture.Workspace.Selected.Script.Should().Be("db.orders.find({})");
        fixture.Workspace.Selected.QueryFilePath.Should().Be("orders.js");
    }

    [Theory]
    [InlineData("shop", true)]
    [InlineData("", false)]
    [InlineData("invalid/name", false)]
    public async Task Should_RequireExplicitDatabase_WhenOpeningQueryFile(string database, bool expectedOpen)
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._))
            .Returns(new[] { new StoredConnection(fixture.Profile, null) });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(new QueryFileService(A.Fake<IQueryFileStore>(), fixture.Settings, NullLogger<QueryFileService>.Instance));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider);
        QueryFileOpenRequest? opened = null;
        await renderer.MountAsync<QueryFilesPanel>(new()
        {
            [nameof(QueryFilesPanel.OnOpen)] = EventCallback.Factory.Create<QueryFileOpenRequest>(this, request => opened = request),
        });
        await renderer.ChangeAsync("mono", "orders.js");
        await renderer.ChangeAsync("#query-file-database", database);

        await renderer.SubmitAsync();

        (opened is not null).Should().Be(expectedOpen);
        if (expectedOpen) opened!.Database.Should().Be(database);
    }
}
