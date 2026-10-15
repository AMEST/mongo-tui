using AwesomeAssertions;
using MongoTui.TUI.Editor;

namespace MongoTui.TUI.Tests.Editor;

public sealed class QueryMethodContextTests
{
    [Theory]
    [InlineData("db.get", "Database", "get")]
    [InlineData("db.getSiblingDB('app').get", "Database", "get")]
    [InlineData("db.getCollection('items').", "Collection", "")]
    [InlineData("db.items.fi", "Collection", "fi")]
    [InlineData("db.items.find({}).", "Cursor", "")]
    [InlineData("db.items.find({}).limit(5).", "Cursor", "")]
    [InlineData("db.items.find({}).sort({name: 1}).skip(2).toA", "Cursor", "toA")]
    [InlineData("db.items.find({meta: {list: [1, {label: ')'}]}}).", "Cursor", "")]
    [InlineData("db.items.find({ /* )] */ name: 'a' }).", "Cursor", "")]
    [InlineData("db.items.find({\n name: 'a'\n}).limit(2).", "Cursor", "")]
    [InlineData("db.getSiblingDB('app').getCollection('it\\'ems').find({}).", "Cursor", "")]
    public void Should_ResolveMethodScope_WhenCaretFollowsSupportedChain(string script, string scope, string prefix)
    {
        var context = QueryCompletionContext.Parse(script, script.Length, null);

        context.Should().NotBeNull();
        context!.MethodScope.ToString().Should().Be(scope);
        context.Prefix.Should().Be(prefix);
    }

    [Theory]
    [InlineData("db.items.findOne({}).")]
    [InlineData("db.items.countDocuments({}).")]
    [InlineData("db.items.find({}).count().")]
    [InlineData("db.items.find({}).toArray().")]
    [InlineData("db.items.unknown().")]
    [InlineData("db.items.find({name: 1).")]
    [InlineData("db.items.find({name: [1, 2}).")]
    [InlineData("db.items.find({name: 'value.")]
    [InlineData("db.items.find({}) // .")]
    [InlineData("'db.items.find({}).")]
    public void ShouldNot_OfferMethods_WhenChainIsTerminalOrIncomplete(string script)
    {
        var context = QueryCompletionContext.Parse(script, script.Length, null);

        context.Should().BeNull();
    }
}
