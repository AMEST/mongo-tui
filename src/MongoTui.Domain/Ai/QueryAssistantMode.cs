namespace MongoTui.Domain.Ai;

/// <summary>Kind of MongoDB operation the assistant should produce.</summary>
public enum QueryAssistantMode
{
    Find,
    Aggregate,
}
