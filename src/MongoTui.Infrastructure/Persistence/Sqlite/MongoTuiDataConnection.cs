using LinqToDB;
using LinqToDB.Data;

namespace MongoTui.Infrastructure.Persistence.Sqlite;

/// <summary>LINQ to DB data connection exposing the application's SQLite tables.</summary>
public sealed class MongoTuiDataConnection : DataConnection
{
    public MongoTuiDataConnection(string connectionString)
        : base(new DataOptions().UseConnectionString(ProviderName.SQLiteMS, connectionString))
    {
    }

    public ITable<ConnectionRow> Connections => this.GetTable<ConnectionRow>();

    public ITable<QueryHistoryRow> QueryHistory => this.GetTable<QueryHistoryRow>();

    public ITable<SettingRow> Settings => this.GetTable<SettingRow>();
}
