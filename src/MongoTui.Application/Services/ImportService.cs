using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Transfer;

namespace MongoTui.Application.Services;

/// <summary>Validates and runs collection imports.</summary>
public sealed class ImportService
{
    private readonly IMongoTransferGateway _transfer;

    public ImportService(IMongoTransferGateway transfer) => _transfer = transfer;

    public Task ImportAsync(MongoConnectionTarget target, ImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            throw new DomainException("Input path is required.");
        }

        return _transfer.ImportAsync(target, request, cancellationToken);
    }
}
