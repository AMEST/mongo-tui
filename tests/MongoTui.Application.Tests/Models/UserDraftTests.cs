using AwesomeAssertions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using MongoTui.Application.Models;

namespace MongoTui.Application.Tests.Models;

public sealed class UserDraftTests
{
    [Fact]
    public void Should_KeepAdditionalRoles_WhenExistingUserIsLoaded()
    {
        var existing = new MongoUser("alice", "admin", [new("readWrite", "orders"), new("clusterMonitor", "admin"), new("custom", "reports")]);

        var draft = new UserDraft(existing);

        draft.Username.Should().Be("alice");
        draft.Password.Should().BeEmpty();
        draft.Databases.Should().Equal(new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.ReadWrite));
        draft.ClusterRoles.Should().Contain(ClusterRole.ClusterMonitor);
        draft.AdditionalRoles.Should().Equal(new MongoRole("custom", "reports"));
    }

    [Fact]
    public void Should_ClearOtherManagedPermissions_WhenAdministratorIsSelected()
    {
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("read", "orders"), new("custom", "reports")]));
        draft.SetClusterRole(ClusterRole.ClusterMonitor, true);

        draft.SetClusterRole(ClusterRole.Administrator, true);

        draft.Databases.Should().BeEmpty();
        draft.ClusterRoles.Should().Equal(ClusterRole.Administrator);
        draft.AdditionalRoles.Should().Equal(new MongoRole("custom", "reports"));
    }

    [Fact]
    public void Should_IgnoreOtherPermissions_WhenAdministratorIsActive()
    {
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("root", "admin"), new("read", "orders")]));

        draft.SetClusterRole(ClusterRole.ClusterMonitor, true);
        draft.SetDatabaseAccess("billing", DatabaseAccessLevel.ReadWrite);

        draft.ClusterRoles.Should().Equal(ClusterRole.Administrator);
        draft.Databases.Should().BeEmpty();
    }

    [Fact]
    public void Should_ReplaceAccessForSameDatabase_WhenLevelChanges()
    {
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("read", "orders")]));

        draft.SetDatabaseAccess(" orders ", DatabaseAccessLevel.DbOwner);

        draft.Databases.Should().Equal(new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.DbOwner));
    }

    [Fact]
    public void Should_AllowNewPermissions_WhenAdministratorIsUnselected()
    {
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("root", "admin"), new("custom", "reports")]));
        draft.SetClusterRole(ClusterRole.Administrator, false);

        draft.SetDatabaseAccess("orders", DatabaseAccessLevel.ReadWrite);

        draft.IsAdministrator.Should().BeFalse();
        draft.Databases.Should().ContainSingle();
        draft.ClusterRoles.Should().BeEmpty();
        draft.AdditionalRoles.Should().Equal(new MongoRole("custom", "reports"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_KeepOnlyAdministrator_WhenExistingRolesArriveInAnyOrder(bool rootFirst)
    {
        MongoRole[] roles = [new("read", "orders"), new("clusterMonitor", "admin"), new("root", "admin"), new("custom", "reports")];
        if (rootFirst) Array.Reverse(roles);

        var draft = new UserDraft(new MongoUser("alice", "admin", roles));

        draft.Databases.Should().BeEmpty();
        draft.ClusterRoles.Should().Equal(ClusterRole.Administrator);
        draft.AdditionalRoles.Should().Equal(new MongoRole("custom", "reports"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_KeepStrongestAccess_WhenDatabaseHasMultipleManagedRoles(bool reverse)
    {
        MongoRole[] roles = [new("read", "orders"), new("dbOwner", "orders"), new("readWrite", "orders")];
        if (reverse) Array.Reverse(roles);

        var draft = new UserDraft(new MongoUser("alice", "admin", roles));

        draft.Databases.Should().Equal(new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.DbOwner));
    }

    [Fact]
    public void Should_KeepExistingRoles_WhenCallerMutatesOriginalList()
    {
        var roles = new List<MongoRole> { new("custom", "reports") };
        var draft = new UserDraft(new MongoUser("alice", "admin", roles));

        roles.Clear();

        draft.AdditionalRoles.Should().Equal(new MongoRole("custom", "reports"));
        draft.Existing!.Roles.Should().Equal(new MongoRole("custom", "reports"));
    }

    [Fact]
    public void Should_KeepSnapshot_WhenDatabaseAndClusterSelectionsChange()
    {
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("read", "orders"), new("clusterMonitor", "admin")]));
        var snapshot = draft.Snapshot();
        draft.SetDatabaseAccess("orders", DatabaseAccessLevel.DbOwner);

        draft.SetClusterRole(ClusterRole.ClusterMonitor, false);

        snapshot.Databases.Should().Equal(new DatabaseAccess(DatabaseName.Create("orders"), DatabaseAccessLevel.Read));
        snapshot.ClusterRoles.Should().Equal(ClusterRole.ClusterMonitor);
    }

    [Fact]
    public void Should_PreserveInputSnapshot_WhenDraftChangesAfterSubmission()
    {
        var draft = new UserDraft(new MongoUser("alice", "admin", [new("read", "orders")]));
        var snapshot = draft.Snapshot();

        draft.SetClusterRole(ClusterRole.Administrator, true);

        snapshot.Databases.Should().ContainSingle();
        snapshot.ClusterRoles.Should().BeEmpty();
    }
}
