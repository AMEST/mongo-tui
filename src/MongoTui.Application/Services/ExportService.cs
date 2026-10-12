using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Transfer;

namespace MongoTui.Application.Services;

/// <summary>Validates and runs collection exports.</summary>
public sealed class ExportService
{
    private readonly IMongoTransferGateway _transfer;

    public ExportService(IMongoTransferGateway transfer) => _transfer = transfer;

    public Task ExportAsync(MongoConnectionTarget target, ExportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new DomainException("Output path is required.");
        }

        return _transfer.ExportAsync(target, request, cancellationToken);
    }
}
