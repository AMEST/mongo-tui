using MongoDB.Bson;

namespace MongoTui.Web.Services;

public static class BsonDisplay
{
    private const int MaximumPreviewCharacters = 120;
    public static string Preview(BsonValue value)
    {
        var preview = value switch
        {
            BsonDocument document => $"{{ {document.ElementCount} }}",
            BsonArray array => $"[ {array.Count} ]",
            BsonString text => text.Value,
            BsonNull => "null",
            _ => value.ToString() ?? string.Empty,
        };
        return preview.Length > MaximumPreviewCharacters ? preview[..MaximumPreviewCharacters] + "…" : preview;
    }

    public static string CssType(BsonValue value) => value.BsonType switch
    {
        BsonType.String => "string", BsonType.Boolean => "boolean", BsonType.Null => "null",
        BsonType.Int32 or BsonType.Int64 or BsonType.Double or BsonType.Decimal128 => "number",
        BsonType.ObjectId => "objectid", _ => "complex",
    };
}
