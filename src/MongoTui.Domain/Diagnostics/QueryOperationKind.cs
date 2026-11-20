namespace MongoTui.Domain.Diagnostics;

public enum QueryOperationKind
{
    Find, FindOne, Aggregate, Count,
    Insert, Update, Delete,
    Command, Script, Other,
}

