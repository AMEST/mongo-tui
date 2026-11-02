using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class QueryFileDatabaseSelectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_PrefillCurrentDatabaseOnlyWhenConnected_WhenFilesPageOpened(bool connected)
    {
        await using var fixture = new WorkspaceFixture();
        if (connected) await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "shop", null);
        await using var provider = CreateProvider(fixture);
        await using var renderer = new ComponentTestRenderer(provider);

        await renderer.MountAsync<QueryFilesPanel>(new() { [nameof(QueryFilesPanel.CurrentTab)] = tab });

        (await renderer.AttributeAsync("#query-file-database", "value"))?.ToString().Should().Be(connected ? "shop" : "");
        (await renderer.AttributeAsync("#query-file-database", "list")).Should().Be("query-file-databases");
        if (connected) (await renderer.TextAsync()).Should().Contain("demo");
        else (await renderer.TextAsync()).Should().NotContain("demo");
    }

    [Fact]
    public async Task Should_ClearDatabase_WhenSwitchingToAnotherConnection()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var other = ConnectionProfile.Create(ConnectionId.New(), "Other", "mongodb://localhost:27018");
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "shop", null);
        await using var provider = CreateProvider(fixture, other);
        await using var renderer = new ComponentTestRenderer(provider);
        await renderer.MountAsync<QueryFilesPanel>(new() { [nameof(QueryFilesPanel.CurrentTab)] = tab });

        await renderer.ChangeAsync(other.Id.ToString());

        (await renderer.AttributeAsync("#query-file-database", "value"))?.ToString().Should().Be("");
        (await renderer.TextAsync()).Should().NotContain("demo");
    }

    [Fact]
    public async Task Should_KeepManualOverride_WhenOpeningWithCurrentTab()
    {
        await using var fixture = new WorkspaceFixture();
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "shop", null);
        await using var provider = CreateProvider(fixture);
        await using var renderer = new ComponentTestRenderer(provider);
        QueryFileOpenRequest? opened = null;
        await renderer.MountAsync<QueryFilesPanel>(new()
        {
            [nameof(QueryFilesPanel.CurrentTab)] = tab,
            [nameof(QueryFilesPanel.OnOpen)] = EventCallback.Factory.Create<QueryFileOpenRequest>(this, request => opened = request),
        });
        await renderer.ChangeAsync("mono", "orders.js");
        await renderer.ChangeAsync("#query-file-database", "archive");

        await renderer.SubmitAsync();

        opened!.Database.Should().Be("archive");
        opened.ConnectionId.Should().Be(fixture.Profile.Id);
    }

    private static ServiceProvider CreateProvider(WorkspaceFixture fixture, ConnectionProfile? other = null)
    {
        var profiles = new List<StoredConnection> { new(fixture.Profile, null) };
        if (other is not null) profiles.Insert(0, new(other, null));
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._)).Returns(profiles);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Catalog);
        services.AddSingleton(fixture.Text);
        services.AddSingleton(new QueryFileService(A.Fake<IQueryFileStore>(), fixture.Settings, NullLogger<QueryFileService>.Instance));
        return services.BuildServiceProvider();
    }
}
