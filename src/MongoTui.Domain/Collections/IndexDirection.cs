namespace MongoTui.Domain.Collections;

/// <summary>Direction/type of a single index key.</summary>
public enum IndexDirection
{
    Ascending,
    Descending,
    Hashed,
    Text,
    Geo2d,
    Geo2dSphere,
}
