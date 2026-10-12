using MongoTui.Domain.Queries;

namespace MongoTui.Application.Abstractions.Mongo;

/// <summary>An interactive <c>mongosh</c> session bound to a single tab.</summary>
public interface IMongoShellSession : IAsyncDisposable
{
    bool IsRunning { get; }

    Task<QueryResult> EvaluateAsync(string script, CancellationToken cancellationToken);
}
