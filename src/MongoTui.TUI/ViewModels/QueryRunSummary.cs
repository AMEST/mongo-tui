namespace MongoTui.TUI.ViewModels;

/// <summary>Outcome of running the active query, for the status bar.</summary>
public sealed record QueryRunSummary(int DocumentCount, TimeSpan Elapsed);
