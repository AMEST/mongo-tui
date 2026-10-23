using System.Collections.Generic;

namespace MongoTui.Domain.Users;

/// <summary>A MongoDB user created in a specific database.</summary>
public sealed record MongoUser(
    string Username,
    string Database,
    IReadOnlyList<MongoRole> Roles);
