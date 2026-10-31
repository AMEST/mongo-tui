namespace MongoTui.TUI.Editor;

internal enum QueryMethodScope { Database, Collection, Cursor }

internal sealed record QueryMethod(string Name, string Arguments, int CaretBack, QueryMethodScope? ResultScope = null)
{
    public string Replacement => $"{Name}({Arguments})";
}

/// <summary>Known shell methods and the types returned by chainable calls.</summary>
internal static class QueryMethodCatalog
{
    public static IReadOnlyList<QueryMethod> Get(QueryMethodScope scope) => scope switch
    {
        QueryMethodScope.Database =>
        [
            new("getSiblingDB", "\"\"", 2, QueryMethodScope.Database),
            new("getCollection", "\"\"", 2, QueryMethodScope.Collection),
        ],
        QueryMethodScope.Collection =>
        [
            new("find", "{}", 2, QueryMethodScope.Cursor), new("findOne", "{}", 2),
            new("countDocuments", "{}", 2), new("estimatedDocumentCount", "", 0),
        ],
        QueryMethodScope.Cursor =>
        [
            new("limit", "", 1, QueryMethodScope.Cursor), new("skip", "", 1, QueryMethodScope.Cursor),
            new("maxTimeMS", "", 1, QueryMethodScope.Cursor),
            new("sort", "{}", 2, QueryMethodScope.Cursor), new("toArray", "", 0), new("count", "", 0),
        ],
        _ => [],
    };
}
