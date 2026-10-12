using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

/// <summary>Document-level CRUD over the Mongo gateway.</summary>
public sealed class DocumentService
{
    private readonly IMongoGateway _gateway;

    public DocumentService(IMongoGateway gateway) => _gateway = gateway;

    public Task InsertAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string documentJson,
        CancellationToken cancellationToken)
    {
        Parse(documentJson);

        return _gateway.InsertOneAsync(target, collection, documentJson, cancellationToken);
    }

    public Task<long> ReplaceAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        string documentJson,
        CancellationToken cancellationToken)
    {
        Parse(documentJson);

        return _gateway.ReplaceOneAsync(target, collection, idFilterJson, documentJson, cancellationToken);
    }

    public Task<long> DeleteAsync(
        MongoConnectionTarget target,
        CollectionRef collection,
        string idFilterJson,
        CancellationToken cancellationToken) =>
        _gateway.DeleteOneAsync(target, collection, idFilterJson, cancellationToken);

    /// <summary>Builds an <c>{ "_id": ... }</c> filter for the given document JSON.</summary>
    public static string BuildIdFilter(string documentJson)
    {
        var document = Parse(documentJson);

        if (!document.TryGetValue("_id", out var id))
        {
            throw new DomainException("Document must contain an '_id' field.");
        }

        return new BsonDocument("_id", id).ToJson();
    }

    /// <summary>Parses document JSON, throwing a domain error when it is empty or malformed.</summary>
    public static BsonDocument Parse(string documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            throw new DomainException("Document JSON must not be empty.");
        }

        try
        {
            return BsonDocument.Parse(documentJson);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new DomainException("Document JSON is invalid.", exception);
        }
    }
}
