using AwesomeAssertions;
using MongoTui.TUI.Editor;

namespace MongoTui.TUI.Tests.Editor;

public sealed class QueryCompletionContextTests
{
    [Theory]
    [InlineData("db.us", "app", null, "us")]
    [InlineData("db.", "app", null, "")]
    [InlineData("db.getCollection(\"us", "app", null, "us")]
    [InlineData("db.getSiblingDB(\"ap", null, null, "ap")]
    [InlineData("db.getSiblingDB('ap", null, null, "ap")]
    [InlineData("db.getSiblingDB(\"other\").us", "other", null, "us")]
    [InlineData("db.users.find({ na", "app", "users", "na")]
    [InlineData("db.users.find({\n  na", "app", "users", "na")]
    [InlineData("db.users.find({ 'profile.na", "app", "users", "profile.na")]
    [InlineData("db.getCollection('order-items').find({ na", "app", "order-items", "na")]
    [InlineData("db.getSiblingDB('other').users.find({ na", "other", "users", "na")]
    [InlineData("db.users.find({a: 1}); db.orders.find({ na", "app", "orders", "na")]
    public void Should_ResolveContext_WhenCaretFollowsName(
        string script, string? database, string? collection, string prefix)
    {
        var context = QueryCompletionContext.Parse(script, script.Length, "app");

        context.Should().NotBeNull();
        context!.Database.Should().Be(database);
        context.Collection.Should().Be(collection);
        context.Prefix.Should().Be(prefix);
    }

    [Theory]
    [InlineData("// db.us")]
    [InlineData("/* db.us")]
    [InlineData("const value = 'db.us")]
    [InlineData("db.users.find({ name: 'al")]
    [InlineData("db.users.find({ name: '{ na")]
    [InlineData("db.users.find({});\n{ na")]
    [InlineData("db.users.find({})\n{ na")]
    [InlineData("db.users.find({ // { na")]
    [InlineData("db.users.find({ /* { na")]
    [InlineData("db.users.find({ name: ")]
    public void ShouldNot_OfferNames_WhenCaretIsOutsideSupportedContext(string script)
    {
        var context = QueryCompletionContext.Parse(script, script.Length, "app");

        context.Should().BeNull();
    }

    [Fact]
    public void Should_IncludeSuffix_WhenCaretIsInsideName()
    {
        const string script = "db.uses.find({})";

        var context = QueryCompletionContext.Parse(script, 5, "app");

        context!.Start.Should().Be(3);
        context.Length.Should().Be(4);
        context.Prefix.Should().Be("us");
    }

    [Theory]
    [InlineData("db.ord", "order-items", "getCollection(\"order-items\")")]
    [InlineData("db.us", "users", "users")]
    [InlineData("db.users.find({ pro", "profile.name", "\"profile.name\"")]
    [InlineData("db.getCollection('ord", "order'items", "order\\'items")]
    [InlineData("db.users.find({ 'line", "line\nname", "line\\nname")]
    [InlineData("db.users.find({ им", "имя", "\"имя\"")]
    public void Should_EscapeReplacement_WhenNameNeedsQuoting(string script, string candidate, string expected)
    {
        var context = QueryCompletionContext.Parse(script, script.Length, "app");

        var replacement = context!.Replacement(candidate);

        replacement.Should().Be(expected);
    }
}
