using MongoTui.Domain.Queries;

namespace MongoTui.Application.Abstractions.Mongo;

/// <summary>An interactive <c>mongosh</c> session bound to a single tab.</summary>
public interface IMongoShellSession : IAsyncDisposable
{
    bool IsRunning { get; }

    /// <summary>Evaluates the script with the selected database; null keeps the connection's default database.</summary>
    Task<QueryResult> EvaluateAsync(string script, string? database, CancellationToken cancellationToken);
}
