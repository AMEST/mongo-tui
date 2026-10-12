using MongoTui.Application.Models;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;

namespace MongoTui.Application.Abstractions.Mongo;

/// <summary>Bulk transfer operations served by the MongoDB Database Tools.</summary>
public interface IMongoTransferGateway
{
    Task ExportAsync(MongoConnectionTarget target, ExportRequest request, CancellationToken cancellationToken);

    Task ImportAsync(MongoConnectionTarget target, ImportRequest request, CancellationToken cancellationToken);

    Task DumpDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string outputDirectory,
        CancellationToken cancellationToken);

    Task RestoreDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string inputDirectory,
        CancellationToken cancellationToken);
}
