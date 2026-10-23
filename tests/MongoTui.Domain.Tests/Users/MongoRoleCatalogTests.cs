using AwesomeAssertions;
using MongoTui.Domain.Users;

namespace MongoTui.Domain.Tests.Users;

public sealed class MongoRoleCatalogTests
{
    [Theory]
    [InlineData("readAnyDatabase")]
    [InlineData("readWriteAnyDatabase")]
    [InlineData("clusterAdmin")]
    [InlineData("clusterManager")]
    [InlineData("clusterMonitor")]
    [InlineData("root")]
    public void Should_MapClusterRole_When_OnAdminDatabase(string name)
    {
        var role = new MongoRole(name, "admin");

        MongoRoleCatalog.TryGetClusterRole(role, out _).Should().BeTrue();
    }

    [Fact]
    public void Should_NotMapClusterRole_When_OnAnotherDatabase()
    {
        var role = new MongoRole("root", "orders");

        MongoRoleCatalog.TryGetClusterRole(role, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("read", DatabaseAccessLevel.Read)]
    [InlineData("readWrite", DatabaseAccessLevel.ReadWrite)]
    [InlineData("dbOwner", DatabaseAccessLevel.DbOwner)]
    public void Should_MapDatabaseAccess_When_ManagedRole(string name, DatabaseAccessLevel level)
    {
        var role = new MongoRole(name, "orders");

        MongoRoleCatalog.TryGetDatabaseAccess(role, out var access).Should().BeTrue();
        access.Database.Value.Should().Be("orders");
        access.Level.Should().Be(level);
    }

    [Fact]
    public void Should_PreserveDatabaseRole_When_DatabaseIsAdmin()
    {
        var role = new MongoRole("read", "admin");

        MongoRoleCatalog.TryGetDatabaseAccess(role, out var access).Should().BeTrue();
        access.Database.Value.Should().Be("admin");
    }

    [Theory]
    [InlineData("read", "admin")]
    [InlineData("dbOwner", "orders")]
    [InlineData("root", "admin")]
    public void Should_ReportManaged_When_KnownRole(string name, string database)
    {
        MongoRoleCatalog.IsManaged(new MongoRole(name, database)).Should().BeTrue();
    }

    [Fact]
    public void Should_NotReportManaged_When_UnknownRole()
    {
        MongoRoleCatalog.IsManaged(new MongoRole("customRole", "orders")).Should().BeFalse();
    }
}
