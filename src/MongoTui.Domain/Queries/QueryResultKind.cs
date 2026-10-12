namespace MongoTui.Domain.Queries;

/// <summary>High-level shape of a query result, driving the result pane mode.</summary>
public enum QueryResultKind
{
    Documents,
    Aggregation,
    Scalar,
}
