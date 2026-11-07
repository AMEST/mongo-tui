using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Application.Queries;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class ExplainQuerySourceTests
{
    [Theory]
    [InlineData("db.orders.find({value: ')'}).sort({created:-1}).skip(2).limit(3)", ExplainOperation.Find)]
    [InlineData("db.orders.findOne({_id:ObjectId('507f1f77bcf86cd799439011')})", ExplainOperation.FindOne)]
    [InlineData("db.orders.aggregate([{$match:{x:1}},{$group:{_id:'$x', n:{$sum:1}}}])", ExplainOperation.Aggregate)]
    [InlineData("db.getSiblingDB('other').getCollection('orders').find({}, {name:1})", ExplainOperation.Find)]
    [InlineData("db.orders.find({}).limit(50).toArray()", ExplainOperation.Find)]
    [InlineData("// write your query below this line\ndb.orders.find({}).toArray(); // results", ExplainOperation.Find)]
    [InlineData("/* read */ db.orders.aggregate([{$match:{x:1}}]).toArray()", ExplainOperation.Aggregate)]
    [InlineData("db.orders.find({url:'https://example.test/a/*value*/', text:'//text'}).toArray()", ExplainOperation.Find)]
    [InlineData("db.orders.find({ /* filter */ value: 'it\\\'s // text'}).toArray()", ExplainOperation.Find)]
    public void Should_ParseReadWithoutEvaluation_WhenExpressionIsSupported(string script, ExplainOperation operation)
    {
        var result = ExplainQuerySource.TryCreate(Context(script), ExplainVerbosity.ExecutionStats);

        result.Should().NotBeNull();
        result!.Operation.Should().Be(operation);
        result.Collection.Collection.Value.Should().Be("orders");
    }
    [Theory]
    [InlineData("const x=1; db.orders.find({x})")]
    [InlineData("db.orders.updateMany({},{$set:{x:1}})")]
    [InlineData("db.orders.find({}).forEach(printjson)")]
    [InlineData("db.orders.find({}).hint({x:1})")]
    [InlineData("db.orders.find({}).limit(-1)")]
    [InlineData("db.orders.find({}).limit(2147483648)")]
    [InlineData("db.orders.find({}).skip(1).skip(2)")]
    [InlineData("db.orders.findOne({}).sort({x:1})")]
    [InlineData("db.orders.aggregate([{$out:'other'}])")]
    [InlineData("db.orders.aggregate([{$merge:'other'}])")]
    [InlineData("db.orders.aggregate([], {allowDiskUse:true})")]
    [InlineData("db.orders.find({x: function(){return 1;}})")]
    [InlineData("db.orders.find({")]
    [InlineData("db.orders.find({}).toArray(1)")]
    [InlineData("db.orders.find({}).toArray().limit(2)")]
    [InlineData("db.orders.find({}).toArray(); db.orders.deleteMany({})")]
    [InlineData("db.orders.find({}).toArray(); /*comment*/ db.orders.find({})")]
    [InlineData("db.orders.findOne({}).toArray()")]
    [InlineData("db.orders.find({}) /* unterminated")]
    public void Should_RejectAmbiguousOrWritingExpressions_WhenParsing(string script)
    {
        var result = ExplainQuerySource.TryCreate(Context(script), ExplainVerbosity.ExecutionStats);

        result.Should().BeNull();
    }
    [Theory]
    [InlineData("mongodb://localhost/shop", "shop")]
    [InlineData("mongodb://localhost", "test")]
    public void Should_UseConnectionDatabase_WhenTabHasNoExplicitDatabase(string uri, string expected)
    {
        var context = Context("db.orders.find({}).toArray()") with { Database = null, Target = new(uri) };

        var result = ExplainQuerySource.TryCreate(context, ExplainVerbosity.ExecutionStats);

        result.Should().NotBeNull(); result!.Collection.Database.Value.Should().Be(expected);
    }
    internal static QueryExecutionContext Context(string script) => new()
    {
        TabId = Guid.NewGuid(), ConnectionId = ConnectionId.New(), Target = new("mongodb://localhost"), Database = "shop", Script = script,
    };
}
