using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Ai;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Services;

/// <summary>
/// Describes a database and its active collection for the AI assistant: the collection list
/// plus the fields observed in a small document sample, with their BSON types.
/// </summary>
public sealed class CollectionSchemaService
{
    private const int SampleLimit = 20;
    private const int MaxDepth = 8;

    private readonly IMongoGateway _gateway;

    public CollectionSchemaService(IMongoGateway gateway) => _gateway = gateway;

    /// <summary>Builds the schema context for <paramref name="database"/> and <paramref name="collection"/>.</summary>
    public async Task<CollectionSchema> DescribeAsync(
        MongoConnectionTarget target,
        string database,
        string collection,
        CancellationToken cancellationToken)
    {
        var databaseName = DatabaseName.Create(database);
        var collectionRef = new CollectionRef(databaseName, CollectionName.Create(collection));

        var collections = await _gateway.ListCollectionsAsync(target, databaseName, cancellationToken);
        var sample = await _gateway.FindAsync(
            target,
            collectionRef,
            "{}",
            new QueryExecutionOptions { Limit = SampleLimit },
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return new CollectionSchema
        {
            Collection = collectionRef,
            Collections = collections,
            Fields = InferFields(sample.Documents),
        };
    }

    private static IReadOnlyList<SchemaField> InferFields(IEnumerable<BsonDocument> documents)
    {
        var types = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            CollectFields(document, string.Empty, 0, types);
        }

        return types
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new SchemaField(pair.Key, FormatTypes(pair.Value)))
            .ToList();
    }

    private static void CollectFields(
        BsonDocument document,
        string prefix,
        int depth,
        Dictionary<string, SortedSet<string>> types)
    {
        if (depth >= MaxDepth)
        {
            return;
        }

        foreach (var element in document)
        {
            var path = prefix + element.Name;

            switch (element.Value)
            {
                case BsonDocument nested:
                    AddType(types, path, "object");
                    CollectFields(nested, path + ".", depth + 1, types);
                    break;
                case BsonArray array:
                    CollectArray(types, path, array, depth);
                    break;
                default:
                    AddType(types, path, TypeName(element.Value));
                    break;
            }
        }
    }

    private static void CollectArray(
        Dictionary<string, SortedSet<string>> types,
        string path,
        BsonArray array,
        int depth)
    {
        var elements = new SortedSet<string>(StringComparer.Ordinal);
        var hasDocuments = false;

        foreach (var item in array)
        {
            if (item is BsonDocument document)
            {
                hasDocuments = true;
                CollectFields(document, path + ".", depth + 1, types);
            }
            else
            {
                elements.Add(TypeName(item));
            }
        }

        if (hasDocuments)
        {
            AddType(types, path, "array<object>");
            return;
        }

        AddType(types, path, elements.Count == 0 ? "array" : $"array<{FormatTypes(elements)}>");
    }

    private static void AddType(Dictionary<string, SortedSet<string>> types, string path, string type)
    {
        if (!types.TryGetValue(path, out var set))
        {
            set = new SortedSet<string>(StringComparer.Ordinal);
            types[path] = set;
        }

        set.Add(type);
    }

    // Keeps "null" last so optional fields read as "string | null".
    private static string FormatTypes(IEnumerable<string> types) =>
        string.Join(" | ", types.OrderBy(type => type == "null" ? 1 : 0).ThenBy(type => type, StringComparer.Ordinal));

    private static string TypeName(BsonValue value) => value.BsonType switch
    {
        BsonType.Double => "double",
        BsonType.String => "string",
        BsonType.Document => "object",
        BsonType.Array => "array",
        BsonType.Binary => "binData",
        BsonType.Undefined => "undefined",
        BsonType.ObjectId => "objectId",
        BsonType.Boolean => "bool",
        BsonType.DateTime => "date",
        BsonType.Null => "null",
        BsonType.RegularExpression => "regex",
        BsonType.JavaScript => "javascript",
        BsonType.Symbol => "symbol",
        BsonType.JavaScriptWithScope => "javascriptWithScope",
        BsonType.Int32 => "int",
        BsonType.Timestamp => "timestamp",
        BsonType.Int64 => "long",
        BsonType.Decimal128 => "decimal",
        BsonType.MinKey => "minKey",
        BsonType.MaxKey => "maxKey",
        _ => value.BsonType.ToString(),
    };
}
