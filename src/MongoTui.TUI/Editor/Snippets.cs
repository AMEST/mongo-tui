namespace MongoTui.TUI.Editor;

/// <summary>Built-in JavaScript query snippets for the editor.</summary>
public static class Snippets
{
    public static IReadOnlyList<Snippet> All { get; } =
    [
        new Snippet("find", "db.collection.find({})"),
        new Snippet("find one", "db.collection.findOne({})"),
        new Snippet("count", "db.collection.countDocuments({})"),
        new Snippet("aggregate", "db.collection.aggregate([ { $match: {} } ])"),
        new Snippet("insert one", "db.collection.insertOne({})"),
        new Snippet("update one", "db.collection.updateOne({ _id: 1 }, { $set: {} })"),
        new Snippet("delete one", "db.collection.deleteOne({ _id: 1 })"),
        new Snippet("indexes", "db.collection.getIndexes()"),
    ];
}
