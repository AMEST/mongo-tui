namespace MongoTui.Domain.Users;

/// <summary>Semantic reasons for user-management failures, independent of diagnostic text.</summary>
public enum UserManagementError
{
    Unknown,
    InsufficientPrivileges,
    AlreadyExists,
    NotFound,
    RoleGrantFailed,
    DeleteCurrentForbidden,
    CommandFailed,
    UsernameRequired,
    PasswordRequired,
    AccessRequired,
    ConnectionClosed,
}
