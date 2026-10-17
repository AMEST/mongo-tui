using AwesomeAssertions;
using MongoTui.TUI.Editor;

namespace MongoTui.TUI.Tests.Editor;

public sealed class DocumentQuerySourceTests
{
    [Theory]
    [InlineData("db.users.find({})", "app", "app", "users")]
    [InlineData("db.orders.find().limit(10).skip(2).sort({date:-1}).toArray();", "app", "app", "orders")]
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
