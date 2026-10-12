namespace MongoTui.Application.Models;

/// <summary>A database on the server, with whether it currently holds any data.</summary>
public sealed record DatabaseInfo(string Name, bool IsEmpty);
