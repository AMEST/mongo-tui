namespace MongoTui.Application.Abstractions.Persistence;

/// <summary>Commits settings and connection changes together, rolling back on failure or cancellation.</summary>
public interface IStorageTransaction
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
