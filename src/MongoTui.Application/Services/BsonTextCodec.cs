using MongoTui.Domain.Primitives;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

/// <summary>The shared Canonical Extended JSON boundary for document data.</summary>
public static class BsonTextCodec
{
    public static string Format(BsonValue value, bool indent = true) => value.ToJson(new JsonWriterSettings
    {
        Indent = indent,
        OutputMode = JsonOutputMode.CanonicalExtendedJson,
    });

    public static string Format(IEnumerable<BsonDocument> documents, bool indent = true) =>
        Format(new BsonArray(documents), indent);

    public static BsonValue ParseValue(string text)
    {
        using var reader = new JsonReader(text);
        var value = BsonSerializer.Deserialize<BsonValue>(reader);
        if (!reader.IsAtEndOfFile()) throw new FormatException("Unexpected content after the JSON value.");
        return value;
    }

    public static BsonDocument ParseDocument(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new DomainException("Document JSON must not be empty.");
        try
        {
            return ParseValue(text) as BsonDocument ?? throw new DomainException("Expected a JSON document.");
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException
            or InvalidOperationException or OverflowException or BsonSerializationException)
        {
            throw new DomainException("Document JSON is invalid.", exception);
        }
    }

    public static BsonDocument IdFilter(BsonDocument document)
    {
        if (!document.TryGetValue(MongoSystemNames.IdField, out var id))
            throw new DomainException("Document must contain an '_id' field.");
        return new BsonDocument(MongoSystemNames.IdField, id);
    }

    /// <summary>Includes field order, numeric BSON types and floating point bits in equality.</summary>
    public static bool ExactlyEqual(BsonDocument left, BsonDocument right) =>
        left.ToBson().AsSpan().SequenceEqual(right.ToBson());

    /// <summary>Refuses ambiguous Extended JSON and values the reader cannot reproduce exactly.</summary>
    public static string ForEditing(BsonDocument document)
    {
        try
        {
            var text = Format(document);
            if (ExactlyEqual(document, ParseDocument(text))) return text;
        }
        catch (Exception exception) when (exception is DomainException or FormatException
            or ArgumentException or InvalidOperationException or NotSupportedException or OverflowException or BsonSerializationException)
        {
            throw UnsafeDocument(exception);
        }
        throw UnsafeDocument();
    }

    private static DocumentEditException UnsafeDocument(Exception? innerException = null)
    {
        const string message = "The document cannot be represented as editable Extended JSON without changing its BSON data.";
        return innerException is null ? new DocumentEditException(message, LocalizationKeys.Documents.UnsafeJson)
            : new DocumentEditException(message, LocalizationKeys.Documents.UnsafeJson, innerException);
    }
}
