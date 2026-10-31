using MongoTui.Application.Queries;
using MongoTui.Application.Models;
using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Queries;

public sealed class DocumentQuerySourceTests
{
    [Theory]
    [InlineData("db.orders.find({})", 0, false, true)]
    [InlineData("db.orders.find({})", 2, false, true)]
    [InlineData("db.orders.find({})", 2, true, false)]
    [InlineData("db.orders.find({}, {_id:1})", 2, false, false)]
    [InlineData("db.orders.aggregate([])", 2, false, false)]
    public void Should_VerifySourceAndIdentities_WhenResolvingExecutedResult(string script, int count, bool missingId, bool editable)
    {
        var context = new QueryExecutionContext { TabId = Guid.NewGuid(), ConnectionId = ConnectionId.New(),
            Target = new MongoConnectionTarget("mongodb://localhost"), Database = "app", Collection = "users", Script = script };
        var documents = Enumerable.Range(0, count).Select(index =>
            new BsonDocument(missingId && index == 1 ? "value" : "_id", index)).ToArray();
        var result = new QueryResult { Kind = QueryResultKind.Documents, Documents = documents };

        var source = DocumentQuerySource.Resolve(context, result);

        (source is not null).Should().Be(editable);
        if (editable)
        {
            source!.Database.Value.Should().Be("app");
            source.Collection.Value.Should().Be("orders");
        }
    }

    [Theory]
    [InlineData("db.users.find({})", "app", "app", "users")]
    [InlineData("db.getCollection('system.version').find({})", "admin", "admin", "system.version")]
    [InlineData("db.orders.find().limit(10).skip(2).sort({date:-1}).toArray();", "app", "app", "orders")]
    [InlineData("db.orders.find({}).maxTimeMS(1000).toArray()", "app", "app", "orders")]
    [InlineData("db.orders.find().limit(10).maxTimeMS(0).skip(2).sort({date:-1}).toArray();", "app", "app", "orders")]
    [InlineData("db.getSiblingDB('other').getCollection('items').findOne({_id:1})", "app", "other", "items")]
    [InlineData("db.getSiblingDB(\"other\").getCollection(\"a\\\"b\").find({}).limit(50).toArray()", null, "other", "a\"b")]
    [InlineData("db.getCollection('a\\u0027b').find({name:'a)b'})", "app", "app", "a'b")]
    public void Should_ResolveCollection_When_UnprojectedRead(string script, string? database, string expectedDatabase, string expectedCollection)
    {
        var source = DocumentQuerySource.Resolve(script, database);

        source.Should().NotBeNull();
        source!.Database.Value.Should().Be(expectedDatabase);
        source.Collection.Value.Should().Be(expectedCollection);
    }

    [Theory]
    [InlineData("db.users.find({}, {name:1})")]
    [InlineData("db.users.findOne({}, {_id:1})")]
    [InlineData("db.users.aggregate([])")]
    [InlineData("db.users.find({}).map(x => ({_id:x._id}))")]
    [InlineData("db.users.find({}); db.orders.find({})")]
    [InlineData("db = db.getSiblingDB('other'); db.users.find({})")]
    [InlineData("db.users.find((() => ({}))())")]
    [InlineData("print({_id:1})")]
    [InlineData("db.users.findOne({}).limit(1)")]
    [InlineData("db.users.findOne({}).maxTimeMS(1000)")]
    [InlineData("db.users.find({}).maxTimeMS(getTimeout())")]
    [InlineData("db.users.find({}).maxTimeMS(1000); db.users.deleteMany({})")]
    [InlineData("db.users.find({}).sort(unknown)")]
    [InlineData("db.users.find({}).toArray().reverse()")]
    public void ShouldNot_ResolveCollection_When_SourceAmbiguous(string script)
    {
        var source = DocumentQuerySource.Resolve(script, "app");

        source.Should().BeNull();
    }

    [Fact]
    public void ShouldNot_ResolveCollection_When_DatabaseUnknown()
    {
        var source = DocumentQuerySource.Resolve("db.users.find({})", null);

        source.Should().BeNull();
    }
}
