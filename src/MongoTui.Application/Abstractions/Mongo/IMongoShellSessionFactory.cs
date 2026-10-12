using MongoTui.Application.Models;

namespace MongoTui.Application.Abstractions.Mongo;

/// <summary>Creates <c>mongosh</c> sessions lazily, one per tab.</summary>
public interface IMongoShellSessionFactory
{
    Task<IMongoShellSession> CreateAsync(MongoConnectionTarget target, CancellationToken cancellationToken);
}
