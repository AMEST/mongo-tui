using MongoTui.Domain.Primitives;
namespace MongoTui.Web.Services;
public sealed record DatabaseSelection(ConnectionId ConnectionId, string Database);
public sealed record CollectionSelection(ConnectionId ConnectionId, string Database, string Collection);
