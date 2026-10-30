using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Web.Services;
using MongoTui.Application.Services;

namespace MongoTui.Web.Tests;

public sealed class BsonDisplayTests
{
    [Fact]
    public void Should_PreserveTypesAndPrecision_WhenDocumentEditedAsJson()
    {
        var original = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() }, { "large", new BsonInt64(9007199254740993) },
            { "smallLong", new BsonInt64(42) }, { "decimal", Decimal128.Parse("123456789.123456789") },
            { "date", new BsonDateTime(new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc)) },
            { "binary", new BsonBinaryData(new byte[] { 0, 1, 2 }) }, { "nested", new BsonArray { BsonNull.Value, true } },
        };

        var roundTrip = BsonDocument.Parse(BsonTextCodec.Format(original));

        roundTrip.ToBson().Should().Equal(original.ToBson());
        roundTrip["smallLong"].BsonType.Should().Be(BsonType.Int64);
    }

    [Fact]
    public void Should_RedactCredentials_WhenMongoErrorContainsUri()
    {
        var message = SafeError.Message(new Exception("failed mongodb+srv://user:secret@cluster/db?authSource=admin"));

        message.Should().Be("failed mongodb://[redacted]");
    }
}
