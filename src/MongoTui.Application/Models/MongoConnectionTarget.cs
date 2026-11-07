using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Models;

/// <summary>A resolved MongoDB endpoint (connection string ready to use by a gateway).</summary>
public sealed record MongoConnectionTarget(string ConnectionString)
{
    public ConnectionId? ConnectionId { get; init; }
}
