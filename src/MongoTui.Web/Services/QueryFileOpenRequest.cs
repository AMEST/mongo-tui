using MongoTui.Domain.Primitives;

namespace MongoTui.Web.Services;

/// <summary>Request to open a query file under a connection.</summary>
public sealed record QueryFileOpenRequest(ConnectionId ConnectionId, string Path);
