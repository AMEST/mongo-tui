namespace MongoTui.Domain.Primitives;

/// <summary>MongoDB system identifiers shared by the driver and both interfaces.</summary>
public static class MongoSystemNames
{
    public const string IdField = "_id";
    public const string IdIndex = "_id_";
    public const string AdminDatabase = "admin";
    public const string DefaultDatabase = "test";
}
