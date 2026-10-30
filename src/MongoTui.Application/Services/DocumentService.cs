using MongoDB.Bson;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Services;

/// <summary>Document-level CRUD over the Mongo gateway.</summary>
public sealed class DocumentService
{
    private readonly IMongoGateway _gateway;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(IMongoGateway gateway, ILogger<DocumentService> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    /// <summary>Loads the first document matching <paramref name="filterJson"/>, or <see langword="null"/>.</summary>
    public async Task<string?> FindOneAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string filterJson,
        CancellationToken cancellationToken)
    {
        var document = await FindDocumentAsync(target, collection, filterJson, cancellationToken).ConfigureAwait(false);
        return document is null ? null : BsonTextCodec.ForEditing(document);
    }

    /// <summary>Reloads the selected identity through the driver, never re-evaluating the source script.</summary>
    public async Task<DocumentEditContent?> LoadForEditAsync(MongoConnectionTarget target, CollectionRef collection,
        BsonDocument selectedDocument, CancellationToken cancellationToken)
    {
        var filter = BsonTextCodec.ForEditing(BsonTextCodec.IdFilter(selectedDocument));
        var document = await FindDocumentAsync(target, collection, filter, cancellationToken).ConfigureAwait(false);
        return document is null ? null : new DocumentEditContent(BsonTextCodec.ForEditing(document),
            BsonTextCodec.Format(BsonTextCodec.IdFilter(document), false));
    }

    public async Task InsertAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string documentJson,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
            { [LogPropertyNames.Database] = collection.Database.Value, [LogPropertyNames.Collection] = collection.Collection.Value });
        try
        {
            Parse(documentJson);
            await _gateway.InsertOneAsync(target, collection, documentJson, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Document inserted into {Database}.{Collection}", collection.Database.Value, collection.Collection.Value);
        }
        catch (OperationCanceledException) { _logger.LogInformation("Document insertion cancelled"); throw; }
        catch (Exception exception)
        {
            _logger.Log(exception is DomainException ? LogLevel.Warning : LogLevel.Error,
                "Document insertion failed: {Failure}", ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    /// <summary>Replaces a document without changing its identity; returns the matched count.</summary>
    public async Task<long> ReplaceAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        string documentJson,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
            { [LogPropertyNames.Database] = collection.Database.Value, [LogPropertyNames.Collection] = collection.Collection.Value });
        try
        {
            EnsureIdentity(idFilterJson, documentJson);
            var count = await _gateway.ReplaceOneAsync(target, collection, idFilterJson, documentJson, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Document replacement in {Database}.{Collection} matched {MatchedCount}", collection.Database.Value, collection.Collection.Value, count);
            return count;
        }
        catch (OperationCanceledException) { _logger.LogInformation("Document replacement cancelled"); throw; }
        catch (Exception exception)
        {
            _logger.Log(exception is DomainException ? LogLevel.Warning : LogLevel.Error,
                "Document replacement failed: {Failure}", ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    public async Task<long> DeleteAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
            { [LogPropertyNames.Database] = collection.Database.Value, [LogPropertyNames.Collection] = collection.Collection.Value });
        try
        {
            var count = await _gateway.DeleteOneAsync(target, collection, idFilterJson, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Deleted {DeletedCount} documents from {Database}.{Collection}", count, collection.Database.Value, collection.Collection.Value);
            return count;
        }
        catch (OperationCanceledException) { _logger.LogInformation("Document deletion cancelled"); throw; }
        catch (Exception exception)
        {
            _logger.LogError("Document deletion failed: {Failure}", ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }

    /// <summary>Builds an <c>{ "_id": ... }</c> filter for the given document JSON.</summary>
    public static string BuildIdFilter(string documentJson) =>
        BsonTextCodec.Format(BsonTextCodec.IdFilter(Parse(documentJson)), false);

    public static void EnsureIdentity(string idFilterJson, string documentJson)
    {
        if (!BsonTextCodec.ExactlyEqual(Parse(idFilterJson), BsonTextCodec.IdFilter(Parse(documentJson))))
            throw new DocumentEditException("Document '_id' cannot be changed.", LocalizationKeys.Documents.IdChanged);
    }

    /// <summary>Parses document JSON, throwing a domain error when it is empty or malformed.</summary>
    public static BsonDocument Parse(string documentJson) => BsonTextCodec.ParseDocument(documentJson);

    private async Task<BsonDocument?> FindDocumentAsync(MongoConnectionTarget target, CollectionRef collection,
        string filterJson, CancellationToken cancellationToken)
    {
        Parse(filterJson);
        try
        {
            var result = await _gateway.FindAsync(target, collection, filterJson,
                new QueryExecutionOptions { Limit = 1 }, cancellationToken).ConfigureAwait(false);
            return result.Documents.FirstOrDefault();
        }
        catch (OperationCanceledException) { _logger.LogDebug("Document read cancelled"); throw; }
        catch (Exception exception)
        {
            _logger.LogWarning("Document read failed in {Database}.{Collection}: {Failure}",
                collection.Database.Value, collection.Collection.Value, ExceptionDiagnostics.Describe(exception));
            throw;
        }
    }
}
