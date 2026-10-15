using AwesomeAssertions;
using MongoTui.TUI.Editor;

namespace MongoTui.TUI.Tests.Editor;

public sealed class QueryCompletionContextTests
{
    [Theory]
    [InlineData("db.ap", null, null, "ap")]
    [InlineData("db.", null, null, "")]
    [InlineData("db.getCollection(\"us", "app", null, "us")]
    [InlineData("db.getSiblingDB(\"ap", null, null, "ap")]
    [InlineData("db.getSiblingDB('ap", null, null, "ap")]
    [InlineData("db.getSiblingDB(\"other\").us", "other", null, "us")]
    [InlineData("db.getSiblingDB(\"other\").", "other", null, "")]
    [InlineData("db.users.find({ na", "app", "users", "na")]
    [InlineData("db.users.find({\n  na", "app", "users", "na")]
    [InlineData("db.users.find({ 'profile.na", "app", "users", "profile.na")]
    [InlineData("db.getCollection('order-items').find({ na", "app", "order-items", "na")]
    [InlineData("db.getCollection('order\\'items').find({ na", "app", "order'items", "na")]
    [InlineData("db.getCollection(\"order\\\"items\").find({ na", "app", "order\"items", "na")]
    [InlineData("db.getCollection('order\\\\items').find({ na", "app", "order\\items", "na")]
    [InlineData("db.getSiblingDB('a\\u0070p').getCollection('order\\x2ditems').find({ na", "app", "order-items", "na")]
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
    [InlineData("db.getCollection('order\\")]
    [InlineData("db.getCollection('order\\u00")]
    [InlineData("db.getCollection('order\\xZZ")]
    [InlineData("db.getCollection('order\\u{110000}")]
    [InlineData("db.getCollection('order\\u{}")]
    [InlineData("db.getCollection('order\\123")]
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
    [InlineData("db.getCollection('ord|\\'items').find({})", "order'items", "db.getCollection('order\\'items').find({})")]
    [InlineData("db.getCollection(\"ord|\\\"items\").find({})", "order\"items", "db.getCollection(\"order\\\"items\").find({})")]
    [InlineData("db.getCollection('ord|\\\\').find({})", "order\\", "db.getCollection('order\\\\').find({})")]
    [InlineData("db.users.find({ 'pro|\\'name': 1 })", "profile'name", "db.users.find({ 'profile\\'name': 1 })")]
    public void Should_ReplaceEntireLiteral_WhenCaretPrecedesEscapedSuffix(string marked, string candidate, string expected)
    {
        var script = marked.Replace("|", string.Empty, StringComparison.Ordinal);
        var cursor = marked.IndexOf('|');
        var context = QueryCompletionContext.Parse(script, cursor, "app")!;

        var completed = script.Remove(context.Start, context.Length).Insert(context.Start, context.Replacement(candidate));

        completed.Should().Be(expected);
    }

    [Theory]
    [InlineData("db.getCollection('order\\'it", "order'it")]
    [InlineData("db.getCollection(\"order'it", "order'it")]
    [InlineData("db.getCollection('order\\\\it", "order\\it")]
    [InlineData("db.getSiblingDB('a\\u0070", "ap")]
    [InlineData("db.getCollection('order\\u{1f600}", "order😀")]
    [InlineData("db.getCollection('order\\ud83d\\ude00", "order😀")]
    [InlineData("db.getCollection('order\\n\\r\\t\\b\\f\\v\\0", "order\n\r\t\b\f\v\0")]
    [InlineData("db.users.find({ 'profile\\u002ena", "profile.na")]
    public void Should_DecodePrefix_WhenQuotedNameContainsEscapes(string script, string expected)
    {
        var context = QueryCompletionContext.Parse(script, script.Length, "app");

        context.Should().NotBeNull();
        context!.Prefix.Should().Be(expected);
    }

    [Theory]
    [InlineData("db.", "app", "getSiblingDB(\"app\")")]
    [InlineData("db.ap", "app", "getSiblingDB(\"app\")")]
    [InlineData("db.getSiblingDB('app').ord", "order-items", "getCollection(\"order-items\")")]
    [InlineData("db.getSiblingDB('app').us", "users", "getCollection(\"users\")")]
    [InlineData("db.getSiblingDB('app').getCollection('getS", "getSiblingDB", "getSiblingDB")]
    [InlineData("db.getSiblingDB('app').st", "stats", "getCollection(\"stats\")")]
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
