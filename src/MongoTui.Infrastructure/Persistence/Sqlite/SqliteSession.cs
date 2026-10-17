namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>Disposes owned connections while leaving an enclosing transaction's connection open.</summary>
internal sealed class SqliteSession(MongoTuiDataConnection connection, bool ownsConnection) : IAsyncDisposable
{
    public MongoTuiDataConnection Connection { get; } = connection;

    public ValueTask DisposeAsync() => ownsConnection ? Connection.DisposeAsync() : ValueTask.CompletedTask;
}
