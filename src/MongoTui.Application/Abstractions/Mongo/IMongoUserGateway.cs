using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;

namespace MongoTui.Application.Abstractions.Mongo;

/// <summary>Manages MongoDB users in a specific database scope.</summary>
public interface IMongoUserGateway
{
    Task<IReadOnlyList<MongoUser>> ListUsersAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        CancellationToken cancellationToken);

    Task CreateUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string password,
        IReadOnlyList<MongoRole> roles,
        CancellationToken cancellationToken);

    Task UpdateUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        string? password,
        IReadOnlyList<MongoRole> roles,
        CancellationToken cancellationToken);

    Task DeleteUserAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string username,
        CancellationToken cancellationToken);
}
