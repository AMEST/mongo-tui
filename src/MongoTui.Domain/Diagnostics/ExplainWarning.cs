namespace MongoTui.Domain.Diagnostics;

public sealed record ExplainWarning(ExplainWarningKind Kind, string? Detail);

