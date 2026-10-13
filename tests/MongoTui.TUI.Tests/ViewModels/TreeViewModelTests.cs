using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Mongo;
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
        A.CallTo(() => _localizer.Get("Database.Empty")).Returns("(empty)");
        _sut = new TreeViewModel(_gateway, _localizer);
    }

    [Fact]
    public void Should_IncludeAllDatabases_When_Built()
    {
        var connection = NewConnection("local", "admin", "app", "logs", "config", "sample");

        _sut.AddOrReplace(connection);

        var rows = _sut.BuildRows();
        rows.Should().HaveCount(6);
        rows[0].Kind.Should().Be(TreeRowKind.Connection);
        rows.Skip(1).Should().OnlyContain(row => row.Kind == TreeRowKind.Database);
        rows.Skip(1).Select(row => row.Database).Should().Equal("admin", "app", "logs", "config", "sample");
    }

    [Fact]
    public void Should_MarkEmptyDatabase_When_Built()
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        _sut.AddOrReplace(new ActiveConnection(profile, Target, [new DatabaseInfo("blank", true)]));

        var row = _sut.BuildRows().Single(row => row.Kind == TreeRowKind.Database);

        row.Text.Should().EndWith("(empty)");
        row.Database.Should().Be("blank");
    }

    [Fact]
    public void Should_HideDatabases_When_ConnectionCollapsed()
    {
        var connection = NewConnection("local", "admin", "app");
        _sut.AddOrReplace(connection);

        _sut.ToggleConnection(connection.Profile.Id);

        _sut.BuildRows().Should().ContainSingle().Which.Kind.Should().Be(TreeRowKind.Connection);
    }

    [Fact]
    public async Task Should_ShowCollections_When_DatabaseExpanded()
    {
        var connection = NewConnection("local", "app");
        _sut.AddOrReplace(connection);
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "users", "orders" });

        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        var rows = _sut.BuildRows();
        rows.Where(row => row.Kind == TreeRowKind.Collection).Select(row => row.Collection)
            .Should().Equal("orders", "users");
        rows.Where(row => row.Kind == TreeRowKind.Collection).Should().OnlyContain(row => row.Database == "app");
    }

    [Fact]
    public async Task Should_LoadCollectionsOnce_When_ToggledRepeatedly()
    {
        var connection = NewConnection("local", "app");
        _sut.AddOrReplace(connection);
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "users" });

        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Should_ReplaceConnection_When_SameProfileAdded()
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost:27017");
        _sut.AddOrReplace(new ActiveConnection(profile, Target, [new DatabaseInfo("admin", false)]));

        _sut.AddOrReplace(new ActiveConnection(profile, Target, [new DatabaseInfo("admin", false), new DatabaseInfo("app", false)]));

        _sut.Count.Should().Be(1);
        _sut.BuildRows().Should().HaveCount(3);
    }

    [Fact]
    public async Task Should_ReloadCollections_When_Requested()
    {
        var connection = NewConnection("local", "app");
        _sut.AddOrReplace(connection);
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "users" });

        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "users", "orders" });
        await _sut.ReloadCollectionsAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        _sut.BuildRows().Where(row => row.Kind == TreeRowKind.Collection).Select(row => row.Collection)
            .Should().Equal("orders", "users");
    }

    [Fact]
    public void Should_HideDatabase_When_Removed()
    {
        var connection = NewConnection("local", "app", "logs");
        _sut.AddOrReplace(connection);

        _sut.RemoveDatabase(connection.Profile.Id, "app");

        _sut.BuildRows().Where(row => row.Kind == TreeRowKind.Database).Select(row => row.Database)
            .Should().Equal("logs");
    }

    [Fact]
    public async Task Should_HideCollection_When_Removed()
    {
        var connection = NewConnection("local", "app");
        _sut.AddOrReplace(connection);
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "users", "orders" });
        await _sut.ToggleDatabaseAsync(connection.Profile.Id, Target, "app", CancellationToken.None);

        _sut.RemoveCollection(connection.Profile.Id, "app", "users");

        _sut.BuildRows().Where(row => row.Kind == TreeRowKind.Collection).Select(row => row.Collection)
            .Should().Equal("orders");
    }

    private static ActiveConnection NewConnection(string name, params string[] databases)
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), name, "mongodb://localhost:27017");
        return new ActiveConnection(profile, Target, databases.Select(database => new DatabaseInfo(database, false)).ToList());
    }
}
