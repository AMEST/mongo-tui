using MongoTui.Domain.Primitives;

namespace MongoTui.Domain.Users;

/// <summary>A MongoDB role assignment.</summary>
public sealed record MongoRole(string Name, string Database);
