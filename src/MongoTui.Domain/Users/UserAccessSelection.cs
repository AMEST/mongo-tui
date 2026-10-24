using System.Collections.Generic;

namespace MongoTui.Domain.Users;

/// <summary>Access selection defined by the user in the UI.</summary>
public sealed record UserAccessSelection(
    IReadOnlyList<DatabaseAccess> Databases,
    IReadOnlySet<ClusterRole> ClusterRoles);
