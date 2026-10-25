using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;

namespace MongoTui.Application.Services;

/// <summary>Orchestrates MongoDB user management operations.</summary>
public sealed class UserManagementService
{
    private readonly IMongoUserGateway _gateway;
    private readonly PasswordGenerator _passwordGenerator;

    public UserManagementService(IMongoUserGateway gateway, PasswordGenerator passwordGenerator)
    {
        _gateway = gateway;
        _passwordGenerator = passwordGenerator;
    }

    public Task<IReadOnlyList<MongoUser>> ListAsync(MongoConnectionTarget target, DatabaseName database, CancellationToken cancellationToken) =>
        _gateway.ListUsersAsync(target, database, cancellationToken);

    public Task CreateAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string password,
        UserAccessSelection access,
        CancellationToken cancellationToken)
    {
        ValidateUsername(username);
        ValidatePassword(password);
        ValidateAccess(access);
        var roles = MapAccessToRoles(access);
        return _gateway.CreateUserAsync(target, database, username, password, roles, cancellationToken);
    }

    public Task UpdateAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        MongoUser existingUser,
        string? password,
        UserAccessSelection access,
        CancellationToken cancellationToken)
    {
        var managed = MapAccessToRoles(access);
        var unmanaged = GetUnmanagedRoles(existingUser.Roles);
        var finalRoles = managed.Concat(unmanaged)
            .GroupBy(r => (r.Name, r.Database))
            .Select(g => g.First())
            .ToList();

        return _gateway.UpdateUserAsync(target, database, existingUser.Username, string.IsNullOrEmpty(password) ? null : password, finalRoles, cancellationToken);
    }

    public Task DeleteAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string? currentUsername,
        CancellationToken cancellationToken) =>
        DeleteAsync(target, database, username, currentUsername, currentAuthDatabase: null, cancellationToken);

    public Task DeleteAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string? currentUsername,
        string? currentAuthDatabase,
        CancellationToken cancellationToken)
    {
        var sameUser = !string.IsNullOrEmpty(currentUsername)
            && string.Equals(username, currentUsername, StringComparison.Ordinal);
        var sameDatabase = currentAuthDatabase is null
            || string.Equals(database.Value, currentAuthDatabase, StringComparison.Ordinal);

        if (sameUser && sameDatabase)
        {
            throw new DomainException("Cannot delete the user of the current connection.");
        }

        return _gateway.DeleteUserAsync(target, database, username, cancellationToken);
    }

    public string GeneratePassword() => _passwordGenerator.Generate();

    public string BuildConnectionString(ConnectionProfile profile, string username, string password, string authDatabase) =>
        UserConnectionStringBuilder.Build(profile, username, password, authDatabase);

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username must not be empty.");
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new DomainException("Password must not be empty.");
        }
    }

    private static void ValidateAccess(UserAccessSelection access)
    {
        if (access.ClusterRoles.Contains(ClusterRole.Administrator))
        {
            return;
        }

        if (access.Databases.Count == 0 && access.ClusterRoles.Count == 0)
        {
            throw new DomainException("At least one database access or cluster role is required.");
        }
    }

    private static List<MongoRole> MapAccessToRoles(UserAccessSelection access)
    {
        if (access.ClusterRoles.Contains(ClusterRole.Administrator))
        {
            return new List<MongoRole> { new(MongoRoleNames.Root, MongoSystemNames.AdminDatabase) };
        }

        var roles = new List<MongoRole>();
        foreach (var dbAccess in access.Databases)
        {
            var roleName = dbAccess.Level switch
            {
                DatabaseAccessLevel.Read => MongoRoleNames.Read,
                DatabaseAccessLevel.ReadWrite => MongoRoleNames.ReadWrite,
                DatabaseAccessLevel.DbOwner => MongoRoleNames.DbOwner,
                _ => throw new ArgumentOutOfRangeException()
            };
            roles.Add(new MongoRole(roleName, dbAccess.Database.Value));
        }

        foreach (var cluster in access.ClusterRoles)
        {
            var roleName = cluster switch
            {
                ClusterRole.ReadAnyDatabase => MongoRoleNames.ReadAnyDatabase,
                ClusterRole.ReadWriteAnyDatabase => MongoRoleNames.ReadWriteAnyDatabase,
                ClusterRole.ClusterAdmin => MongoRoleNames.ClusterAdmin,
                ClusterRole.ClusterManager => MongoRoleNames.ClusterManager,
                ClusterRole.ClusterMonitor => MongoRoleNames.ClusterMonitor,
                ClusterRole.Administrator => MongoRoleNames.Root,
                _ => throw new ArgumentOutOfRangeException()
            };
            roles.Add(new MongoRole(roleName, MongoSystemNames.AdminDatabase));
        }

        return roles
            .GroupBy(r => (r.Name, r.Database))
            .Select(g => g.First())
            .ToList();
    }

    private static List<MongoRole> GetUnmanagedRoles(IReadOnlyList<MongoRole> existingRoles) =>
        existingRoles.Where(role => !MongoRoleCatalog.IsManaged(role)).ToList();
}
