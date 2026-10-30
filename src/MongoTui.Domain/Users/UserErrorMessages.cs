namespace MongoTui.Domain.Users;

/// <summary>Canonical diagnostics for MongoDB user operations.</summary>
public static class UserErrorMessages
{
    public const string InsufficientPrivileges = "Insufficient privileges to manage users.";
    public const string AlreadyExists = "User already exists.";
    public const string NotFound = "User not found.";
    public const string RoleGrantFailed = "Failed to grant role.";
    public const string DeleteCurrentForbidden = "Cannot delete the user of the current connection.";
    public const string CommandFailed = "MongoDB command failed.";
    public const string UsernameRequired = "Username must not be empty.";
    public const string PasswordRequired = "Password must not be empty.";
    public const string AccessRequired = "At least one database access or cluster role is required.";
    public const string ConnectionClosed = "Connection is no longer active.";
}
