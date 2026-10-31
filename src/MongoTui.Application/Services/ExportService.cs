using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Transfer;

namespace MongoTui.Application.Services;

/// <summary>Validates and runs collection exports.</summary>
public sealed class ExportService
{
    private readonly IMongoTransferGateway _transfer;
    private readonly ILogger<ExportService> _logger;

    public ExportService(IMongoTransferGateway transfer, ILogger<ExportService> logger)
    {
        _transfer = transfer;
        _logger = logger;
    }

    public async Task ExportAsync(MongoConnectionTarget target, ExportRequest request, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            [LogPropertyNames.Database] = request.Collection.Database.Value,
            [LogPropertyNames.Collection] = request.Collection.Collection.Value,
            [LogPropertyNames.Action] = "export",
        });
        try
        {
            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                throw new DomainException("Output path is required.");
            }

            await _transfer.ExportAsync(target, request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Collection export cancelled");
            throw;
        }
        catch (Exception exception)
        {
            _logger.Log(exception is DomainException ? LogLevel.Warning : LogLevel.Error,
                "Collection export failed ({Format}): {Failure}", request.Format, ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }
}
