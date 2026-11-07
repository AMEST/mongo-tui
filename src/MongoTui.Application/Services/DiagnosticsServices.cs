using MongoTui.Application.Diagnostics;

namespace MongoTui.Application.Services;

/// <summary>Shared diagnostic operations used by both workspaces.</summary>
public sealed record DiagnosticsServices(DiagnosticsService Server, ExplainService Explain,
    QueryInsightsService Insights, DiagnosticsSettingsService Settings, QueryMetricsQueue Queue);
