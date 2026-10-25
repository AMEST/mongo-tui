using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class ConnectionCatalogTests
{
    [Fact]
    public async Task Should_ShowAllSavedProfilesWithActiveFirst_WhenConnected()
    {
        await using var fixture = new WorkspaceFixture();
        var alpha = Profile("alpha");
        var beta = Profile("Beta");
        Saved(fixture, fixture.Profile, beta, alpha);
        await fixture.Catalog.RefreshAsync(CancellationToken.None);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);

        await fixture.Catalog.ConnectAsync(beta, CancellationToken.None);

        fixture.Catalog.ExplorerProfiles.Select(profile => profile.Name).Should().Equal("Beta", "Test", "alpha");
        fixture.Catalog.Active.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_DefaultToCollapsed_WhenConnected()
    {
        await using var fixture = new WorkspaceFixture();

        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);

        connection.Expanded.Should().BeFalse();
        connection.Databases.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_KeepSavedProfileAndCollapseOnReconnect_WhenDisconnected()
    {
        await using var fixture = new WorkspaceFixture();
        Saved(fixture, fixture.Profile);
        await fixture.Catalog.RefreshAsync(CancellationToken.None);
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        fixture.Catalog.ToggleConnection(connection);
        await fixture.Catalog.DisconnectAsync(fixture.Profile.Id);
        fixture.Catalog.ExplorerProfiles.Should().ContainSingle().Which.Should().Be(fixture.Profile);

        var reconnected = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);

        reconnected.Expanded.Should().BeFalse();
        fixture.Catalog.ExplorerProfiles.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_KeepConnectionAndTabs_WhenCollapsed()
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "users");
        fixture.Catalog.ToggleConnection(connection);

        fixture.Catalog.ToggleConnection(connection);

        connection.Expanded.Should().BeFalse();
        fixture.Catalog.Active.Should().ContainSingle().Which.Should().Be(connection);
        fixture.Workspace.Tabs.Should().ContainSingle().Which.Should().Be(tab);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PreserveExpansionAndReloadVisibleCollections_WhenRefreshed(bool expanded)
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        if (expanded) fixture.Catalog.ToggleConnection(connection);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new[] { "old" });
        await fixture.Catalog.ExpandAsync(connection, connection.Databases.Single(), CancellationToken.None);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new[] { "new", "added" });

        await fixture.Catalog.RefreshDatabasesAsync(connection, CancellationToken.None);

        connection.Expanded.Should().Be(expanded);
        connection.Databases.Single().Expanded.Should().BeTrue();
        connection.Databases.Single().Collections.Should().Equal("added", "new");
    }

    [Fact]
    public async Task Should_RevealOnlySelectedConnectionAndDatabase_WhenCollectionCreated()
    {
        await using var fixture = new WorkspaceFixture();
        var other = Profile("Other");
        Saved(fixture, fixture.Profile, other);
        var first = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var second = await fixture.Catalog.ConnectAsync(other, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(first.Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new[] { "users" });

        await fixture.Catalog.RevealCollectionAsync(new CollectionSelection(fixture.Profile.Id, "demo", "users"), CancellationToken.None);

        first.Expanded.Should().BeTrue();
        first.Databases.Single().Expanded.Should().BeTrue();
        first.Databases.Single().Collections.Should().Equal("users");
        second.Expanded.Should().BeFalse();
    }

    [Fact]
    public async Task Should_KeepSavedProfiles_WhenConnectionFails()
    {
        await using var fixture = new WorkspaceFixture();
        Saved(fixture, fixture.Profile);
        await fixture.Catalog.RefreshAsync(CancellationToken.None);
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Unavailable."));

        var act = () => fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        fixture.Catalog.Active.Should().BeEmpty();
        fixture.Catalog.ExplorerProfiles.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_KeepAllPreviousCollections_WhenRefreshFailsPartway()
    {
        await using var fixture = new WorkspaceFixture();
        A.CallTo(() => fixture.Gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Returns(new[] { new DatabaseInfo("alpha", false), new DatabaseInfo("beta", false) });
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new[] { "existing" });
        foreach (var database in connection.Databases)
            await fixture.Catalog.ExpandAsync(connection, database, CancellationToken.None);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("alpha"), A<CancellationToken>._))
            .Returns(new[] { "replacement" });
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, DatabaseName.Create("beta"), A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Unavailable."));

        var refresh = () => fixture.Catalog.RefreshDatabasesAsync(connection, CancellationToken.None);

        await refresh.Should().ThrowAsync<InvalidOperationException>();
        foreach (var database in connection.Databases) database.Collections.Should().Equal("existing");
    }

    [Fact]
    public async Task Should_IgnoreLateCollections_WhenExpansionWasCancelled()
    {
        await using var fixture = new WorkspaceFixture();
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var response = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => fixture.Gateway.ListCollectionsAsync(connection.Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(response.Task);
        using var cancellation = new CancellationTokenSource();
        var database = connection.Databases.Single();
        var pending = fixture.Catalog.ExpandAsync(connection, database, cancellation.Token);

        await cancellation.CancelAsync();
        response.SetResult(["stale"]);
        var finish = () => pending;

        await finish.Should().ThrowAsync<OperationCanceledException>();
        database.Expanded.Should().BeFalse();
        database.Collections.Should().BeNull();
    }

    private static ConnectionProfile Profile(string name) => ConnectionProfile.Create(ConnectionId.New(), name, "mongodb://localhost:27017");

    private static void Saved(WorkspaceFixture fixture, params ConnectionProfile[] profiles)
    {
        A.CallTo(() => fixture.Repository.GetAllAsync(A<CancellationToken>._))
            .Returns(profiles.Select(profile => new StoredConnection(profile, null)).ToArray());
        foreach (var profile in profiles)
            A.CallTo(() => fixture.Repository.GetAsync(profile.Id, A<CancellationToken>._)).Returns(new StoredConnection(profile, null));
    }
}
