using MongoTui.Application.Models;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Abstractions.Mongo;

/// <summary>A <c>mongosh</c> evaluator bound to a single tab.</summary>
public interface IMongoShellSession : IAsyncDisposable
{
    bool IsRunning { get; }

    /// <summary>Evaluates the script and streams its output to the run's transcript, when provided.</summary>
    Task<QueryResult> EvaluateAsync(string script, string? database, ScriptOutput? output, CancellationToken cancellationToken);
}
