namespace MongoTui.Application.Models;

/// <summary>Identifies a retained log and detects updates without reading unchanged contents.</summary>
public sealed record ApplicationLogFile(string Name, long Length, DateTimeOffset LastWriteTimeUtc);
