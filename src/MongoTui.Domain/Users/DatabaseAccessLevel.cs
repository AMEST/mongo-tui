namespace MongoTui.Domain.Users;

/// <summary>Level of access granted to a database.</summary>
public enum DatabaseAccessLevel
{
    Read,
    ReadWrite,
    DbOwner,
}
