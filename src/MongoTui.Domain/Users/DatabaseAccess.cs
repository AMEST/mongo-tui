using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Users;

/// <summary>Access granted to a specific database.</summary>
public sealed record DatabaseAccess(DatabaseName Database, DatabaseAccessLevel Level);
