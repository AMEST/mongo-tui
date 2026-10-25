using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Testing;

namespace MongoTui.Application.Tests.Services;

public sealed class BsonTextCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_PreserveEveryBsonByte_When_RichDocumentFormatted(bool indent)
    {
        var original = BsonDocumentSamples.RichDocument();

        var actual = BsonTextCodec.ParseDocument(BsonTextCodec.Format(original, indent));

        actual.ToBson().Should().Equal(original.ToBson());
    }

    [Fact]
    public void Should_AcceptEditing_When_DocumentRoundTripsExactly()
    {
        var original = BsonDocumentSamples.RichDocument();

        var text = BsonTextCodec.ForEditing(original);

        BsonTextCodec.ParseDocument(text).ToBson().Should().Equal(original.ToBson());
    }

    [Theory]
    [InlineData("$date", "2020-01-01T00:00:00Z")]
    [InlineData("$numberLong", "42")]
    [InlineData("$numberInt", "invalid")]
    [InlineData("$regex", "^literal")]
    public void Should_RefuseEditing_When_LiteralDocumentLooksLikeExtendedJson(string key, string value)
    {
        var original = new BsonDocument("nested", new BsonArray { new BsonDocument(key, value) });

        var act = () => BsonTextCodec.ForEditing(original);

        act.Should().Throw<DocumentEditException>().Which.ErrorKey.Should().Be(LocalizationKeys.Documents.UnsafeJson);
    }

    [Fact]
    public void Should_DetectNumericTypeChanges_When_BsonDocumentEqualityDoesNot()
    {
        var integer = new BsonDocument("v", new BsonInt32(42));
        var longer = new BsonDocument("v", new BsonInt64(42));
        integer.Equals(longer).Should().BeTrue();

        var equal = BsonTextCodec.ExactlyEqual(integer, longer);

        equal.Should().BeFalse();
    }

    [Fact]
    public void Should_RefuseEditing_When_JsonParserNormalizesNegativeZero()
    {
        var original = new BsonDocument("v", new BsonDouble(-0.0));

        var act = () => BsonTextCodec.ForEditing(original);

        act.Should().Throw<DocumentEditException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("oops")]
    [InlineData("[]")]
    [InlineData("{\"$numberInt\":\"42\"}")]
    [InlineData("{}, {name:1}")]
    [InlineData("{}).map(x => ({_id:x._id})")]
    [InlineData("{} {}")]
    [InlineData("{}; db.items.find({})")]
    public void Should_RejectInput_When_ItIsNotADocument(string input)
    {
        var act = () => BsonTextCodec.ParseDocument(input);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Should_PreserveIdType_When_FilterBuilt()
    {
        var document = new BsonDocument("_id", new BsonInt64(42));

        var filter = DocumentService.BuildIdFilter(BsonTextCodec.Format(document));

        BsonTextCodec.ParseDocument(filter).ToBson().Should().Equal(document.ToBson());
    }
}
