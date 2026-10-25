using Microsoft.Extensions.Logging;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;

namespace MongoTui.Web.Services;

/// <summary>Runs user commands against the circuit's active connection, with a deadline and metadata-only logging.</summary>
public sealed class WebUserManagement(ConnectionCatalog catalog, UserManagementService users, ILogger<WebUserManagement> logger)
{
    private const int TimeoutSeconds = WebOperationTimeouts.CommandSeconds;
    private const string ListUsersAction = "list_users";
    private const string CreateUserAction = "create_user";
    private const string UpdateUserAction = "update_user";
    private const string DeleteUserAction = "delete_user";

    public async Task<IReadOnlyList<MongoUser>> ListAsync(DatabaseSelection selection, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
            { [LogPropertyNames.ConnectionId] = selection.ConnectionId.Value, [LogPropertyNames.Database] = selection.Database, [LogPropertyNames.Action] = ListUsersAction });
        var connection = Connection(selection);
        var database = DatabaseName.Create(selection.Database);
        var result = await WithDeadlineAsync(token => users.ListAsync(connection.Target, database, token), cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Listed {ResultCount} MongoDB users", result.Count);
        return result;
    }

    public async Task<UserSaveResult> SaveAsync(DatabaseSelection selection, UserDraft draft, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
            { [LogPropertyNames.ConnectionId] = selection.ConnectionId.Value, [LogPropertyNames.Database] = selection.Database,
                [LogPropertyNames.Action] = draft.Existing is null ? CreateUserAction : UpdateUserAction });
        var connection = Connection(selection);
        var database = DatabaseName.Create(selection.Database);
        var existing = draft.Existing;
        var username = existing?.Username ?? draft.Username.Trim();
        var password = draft.Password;
        var access = draft.Snapshot();
        var credentials = password.Length == 0 ? null : new UserCredentials(username, password,
            users.BuildConnectionString(connection.Profile, username, password, database.Value), connection.Profile.Ssh.Enabled);
        var reconnect = existing is not null && credentials is not null && IsCurrentUser(selection, existing);
        await WithDeadlineAsync(async token =>
            {
                if (existing is null)
                    await users.CreateAsync(connection.Target, database, username, password, access, token).ConfigureAwait(false);
                else
                    await users.UpdateAsync(connection.Target, database, existing, password, access, token).ConfigureAwait(false);
                return true;
            }, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("MongoDB user saved");
        return new UserSaveResult(credentials, reconnect, existing is not null);
    }

    public async Task DeleteAsync(DatabaseSelection selection, MongoUser user, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
            { [LogPropertyNames.ConnectionId] = selection.ConnectionId.Value, [LogPropertyNames.Database] = selection.Database, [LogPropertyNames.Action] = DeleteUserAction });
        var connection = Connection(selection);
        var current = ConnectionUriParser.Parse(connection.Target.ConnectionString);
        await WithDeadlineAsync(async token =>
        {
            await users.DeleteAsync(connection.Target, DatabaseName.Create(selection.Database), user.Username,
                current.Username, current.AuthDatabase ?? MongoRoleCatalog.AdminDatabase, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("MongoDB user deleted");
    }

    public bool IsCurrentUser(DatabaseSelection selection, MongoUser user)
    {
        var connection = catalog.Active.FirstOrDefault(connection => connection.Profile.Id == selection.ConnectionId);
        if (connection is null) return false;
        var current = ConnectionUriParser.Parse(connection.Target.ConnectionString);
        return user.Username == current.Username && user.Database == (current.AuthDatabase ?? MongoRoleCatalog.AdminDatabase);
    }

    public string GeneratePassword() => users.GeneratePassword();

    private WebConnection Connection(DatabaseSelection selection) =>
        catalog.Active.FirstOrDefault(connection => connection.Profile.Id == selection.ConnectionId)
        ?? throw new DomainException("Connection is no longer active.");

    private async Task<T> WithDeadlineAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        try { return await action(operation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && operation.IsCancellationRequested)
        {
            logger.LogWarning("User management operation timed out after {TimeoutSeconds} seconds", TimeoutSeconds);
            throw new TimeoutException("User management operation timed out.", exception);
        }
        catch (OperationCanceledException) { logger.LogInformation("User management operation cancelled"); throw; }
        catch (Exception exception)
        {
            logger.Log(exception is DomainException or TimeoutException ? LogLevel.Warning : LogLevel.Error,
                "User management operation failed: {Failure}", ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }
}

/// <summary>Temporary credentials shown only after a successful command; never persist or log this value.</summary>
public sealed record UserCredentials(string Username, string Password, string ConnectionString, bool SshEnabled)
{
    public override string ToString() => nameof(UserCredentials);
}
public sealed record UserSaveResult(UserCredentials? Credentials, bool ReconnectRequired, bool Updated);
