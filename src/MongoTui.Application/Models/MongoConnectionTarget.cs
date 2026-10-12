namespace MongoTui.Application.Models;

/// <summary>A resolved MongoDB endpoint (connection string ready to use by a gateway).</summary>
public sealed record MongoConnectionTarget(string ConnectionString);
