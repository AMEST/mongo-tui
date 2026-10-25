using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Tests.ViewModels;

public sealed class TreeViewModelTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ILocalizer _localizer = A.Fake<ILocalizer>();
    private readonly TreeViewModel _sut;

    public TreeViewModelTests()
    {
        A.CallTo(() => _localizer.Get(LocalizationKeys.Database.Empty)).Returns("(empty)");
        _sut = new(_gateway, _localizer);
    }

    [Fact]
    public void Should_HideDatabases_WhenConnectionAdded()
    {
        var connection = NewConnection("local", "admin", "app");

        _sut.AddOrReplace(connection);

        _sut.BuildRows().Should().ContainSingle().Which.Kind.Should().Be(TreeRowKind.Connection);
        _sut.IsExpanded(connection.Profile.Id).Should().BeFalse();
    }

    [Fact]
    public void Should_ShowActiveFirstAndKeepAllSavedProfiles_WhenConnected()
    {
        var alpha = NewConnection("alpha");
        var beta = NewConnection("Beta");
        var zeta = NewConnection("zeta");
        _sut.SetProfiles([zeta.Profile, beta.Profile, alpha.Profile]);
        _sut.AddOrReplace(zeta);

        _sut.AddOrReplace(beta);

        var rows = _sut.BuildRows();
        rows.Select(row => row.Profile.Name).Should().Equal("Beta", "zeta", "alpha");
        rows.Should().HaveCount(3);
        rows.Last().Owner.Should().BeNull();
        _sut.Count.Should().Be(2);
    }

    [Fact]
    public void Should_KeepSavedProfileAndResetExpansion_WhenDisconnected()
    {
        var connection = NewConnection("local", "app");
        _sut.SetProfiles([connection.Profile]);
        AddExpanded(connection);

        _sut.Remove(connection.Profile.Id);

        _sut.BuildRows().Should().ContainSingle().Which.Profile.Should().Be(connection.Profile);
        _sut.BuildRows().Single().Owner.Should().BeNull();
        _sut.IsExpanded(connection.Profile.Id).Should().BeFalse();
        _sut.Count.Should().Be(0);
    }

    [Fact]
    public void Should_IncludeAllDatabases_WhenExpanded()
    {
        var connection = NewConnection("local", "admin", "app", "logs", "config", "sample");
        _sut.AddOrReplace(connection);

        _sut.ToggleConnection(connection.Profile.Id);

        _sut.BuildRows().Skip(1).Select(row => row.Database).Should().Equal("admin", "app", "logs", "config", "sample");
    }

    [Fact]
    public void Should_MarkEmptyDatabase_WhenExpanded()
    {
        var connection = NewConnection("local") with { Databases = [new DatabaseInfo("blank", true)] };

        AddExpanded(connection);

        _sut.BuildRows().Single(row => row.Kind == TreeRowKind.Database).Text.Should().EndWith("(empty)");
    }

    [Fact]
    public void Should_HideDatabasesWithoutDisconnecting_WhenCollapsed()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);

        _sut.ToggleConnection(connection.Profile.Id);

        _sut.BuildRows().Should().ContainSingle();
        _sut.Connections.Should().ContainSingle().Which.Should().Be(connection);
    }

    [Fact]
    public async Task Should_ShowSortedCollections_WhenDatabaseExpanded()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);
        Collections("users", "orders");

        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        CollectionRows().Should().Equal("orders", "users");
    }

    [Fact]
    public async Task Should_LoadCollectionsOnce_WhenToggledRepeatedly()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);
        Collections("users");
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_PreserveExpandedNodes_WhenProfilesAndDatabasesRefreshed()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);
        Collections("users");
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        _sut.SetProfiles([connection.Profile]);

        _sut.UpdateDatabases(connection.Profile.Id, [new DatabaseInfo("app", false), new DatabaseInfo("new", true)]);

        _sut.IsExpanded(connection.Profile.Id).Should().BeTrue();
        _sut.IsExpanded(connection.Profile.Id, "app").Should().BeTrue();
        CollectionRows().Should().Equal("users");
    }

    [Fact]
    public void Should_PreserveCollapsedConnection_WhenDatabasesRefreshed()
    {
        var connection = NewConnection("local", "app");
        _sut.AddOrReplace(connection);

        _sut.UpdateDatabases(connection.Profile.Id, [new DatabaseInfo("new", false)]);

        _sut.BuildRows().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_ClearCachedCollections_WhenDisconnectedWhileDatabaseCollapsed()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);
        Collections("old");
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        _sut.Remove(connection.Profile.Id);
        AddExpanded(connection);
        Collections("new");

        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        CollectionRows().Should().Equal("new");
    }

    [Fact]
    public async Task Should_ReloadCollections_WhenRequested()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);
        Collections("users");
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        Collections("users", "orders");

        await _sut.ReloadCollectionsAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        CollectionRows().Should().Equal("orders", "users");
    }

    [Fact]
    public void Should_ReplaceConnectionWithoutDuplicates_WhenSameProfileAdded()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);

        _sut.AddOrReplace(connection with { Databases = [new DatabaseInfo("new", false)] });

        _sut.Count.Should().Be(1);
        _sut.BuildRows().Last().Database.Should().Be("new");
    }

    [Fact]
    public void Should_HideDatabase_WhenRemoved()
    {
        var connection = NewConnection("local", "app", "logs");
        AddExpanded(connection);

        _sut.RemoveDatabase(connection.Profile.Id, "app");

        _sut.BuildRows().Where(row => row.Kind == TreeRowKind.Database).Select(row => row.Database).Should().Equal("logs");
    }

    [Fact]
    public async Task Should_HideCollection_WhenRemoved()
    {
        var connection = NewConnection("local", "app");
        AddExpanded(connection);
        Collections("users", "orders");
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        _sut.RemoveCollection(connection.Profile.Id, "app", "users");

        CollectionRows().Should().Equal("orders");
    }

    private void AddExpanded(ActiveConnection connection)
    {
        _sut.AddOrReplace(connection);
        _sut.ExpandConnection(connection.Profile.Id);
    }

    private void Collections(params string[] names) =>
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._)).Returns(names);

    private IEnumerable<string?> CollectionRows() =>
        _sut.BuildRows().Where(row => row.Kind == TreeRowKind.Collection).Select(row => row.Collection);

    private static ActiveConnection NewConnection(string name, params string[] databases) => new(
        ConnectionProfile.Create(ConnectionId.New(), name, "mongodb://localhost:27017"), Target,
        databases.Select(database => new DatabaseInfo(database, false)).ToList());
}
