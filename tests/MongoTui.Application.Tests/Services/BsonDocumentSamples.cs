using MongoDB.Bson;

namespace MongoTui.Testing;

internal static class BsonDocumentSamples
{
    public static BsonDocument RichDocument() => new()
    {
        { "_id", ObjectId.GenerateNewId() }, { "edit", "before" },
        { "int", 42 }, { "intMin", int.MinValue }, { "intMax", int.MaxValue },
        { "smallLong", new BsonInt64(42) }, { "largeLong", new BsonInt64(9007199254740993) },
        { "longMin", long.MinValue }, { "longMax", long.MaxValue },
        { "wholeDouble", new BsonDouble(42.0) }, { "fraction", 0.1 },
        { "doubleMin", double.MinValue }, { "doubleMax", double.MaxValue }, { "epsilon", double.Epsilon },
        { "nan", double.NaN }, { "infinity", double.PositiveInfinity }, { "negativeInfinity", double.NegativeInfinity },
        { "decimal", Decimal128.Parse("12345678901234567890.12345678901234") },
        { "scaledDecimal", Decimal128.Parse("1.2300") },
        { "date", new BsonDateTime(-1234567890123) },
        { "binary", new BsonBinaryData(new byte[] { 0, 1, 2, 255 }) },
        { "uuid", new BsonBinaryData(Guid.Parse("d58b9c94-9ba2-457b-8753-3f6546293df0"), GuidRepresentation.Standard) },
        { "legacyUuid", new BsonBinaryData(new byte[16], BsonBinarySubType.UuidLegacy) },
        { "timestamp", new BsonTimestamp(123, 456) }, { "regex", new BsonRegularExpression("^a.*", "im") },
        { "javascript", new BsonJavaScript("return 1;") },
        { "scope", new BsonJavaScriptWithScope("return n;", new BsonDocument("n", new BsonInt64(42))) },
        { "symbol", BsonSymbolTable.Lookup("name") }, { "undefined", BsonUndefined.Value },
        { "min", BsonMinKey.Value }, { "max", BsonMaxKey.Value }, { "null", BsonNull.Value },
        { "bool", true }, { "text", "текст\n\"quoted\"" },
        { "nested", new BsonDocument { { "long", new BsonInt64(42) }, { "double", new BsonDouble(42) },
            { "array", new BsonArray { new BsonInt64(42), new BsonDouble(42), BsonNull.Value,
                new BsonDocument("deeper", new BsonInt64(42)), new BsonArray() } } } },
    };
}
