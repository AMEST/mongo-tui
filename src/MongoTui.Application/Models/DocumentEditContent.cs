namespace MongoTui.Application.Models;

/// <summary>Checked editor text and the identity of the document read by the driver.</summary>
public sealed record DocumentEditContent(string Json, string IdFilter);
