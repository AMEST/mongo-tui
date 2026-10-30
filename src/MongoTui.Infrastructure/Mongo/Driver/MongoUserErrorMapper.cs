using MongoDB.Driver;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Users;

namespace MongoTui.Infrastructure.Mongo.Driver;

/// <summary>Translates driver diagnostics at the infrastructure boundary.</summary>
internal static class MongoUserErrorMapper
{
    public static UserManagementException Map(MongoCommandException exception) =>
        new(Classify(exception), exception);

    private static UserManagementError Classify(MongoCommandException exception) => exception.Code switch
    {
        13 => UserManagementError.InsufficientPrivileges,
        51003 => UserManagementError.AlreadyExists,
        11 => UserManagementError.NotFound,
        _ => ClassifyMessage(exception.Message),
    };

    // Retain compatibility with server responses that do not use the known codes.
    private static UserManagementError ClassifyMessage(string message)
    {
        if (message.Contains("already exists", StringComparison.OrdinalIgnoreCase)) return UserManagementError.AlreadyExists;
        if (message.Contains("role", StringComparison.OrdinalIgnoreCase) && message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return UserManagementError.RoleGrantFailed;
        return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? UserManagementError.NotFound : UserManagementError.CommandFailed;
    }
}
