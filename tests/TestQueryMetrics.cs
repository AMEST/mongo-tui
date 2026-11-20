using Microsoft.Extensions.Logging.Abstractions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Services;

namespace MongoTui.Testing;

internal static class TestQueryMetrics
{
    public static IQueryMetricsRecorder Create() => new QueryMetricsRecorder(
        A.Fake<IQueryMetricsStore>(), new DiagnosticsSettingsService(A.Fake<ISettingsStore>()),
        new SlowQueryLogger(NullLogger<SlowQueryLogger>.Instance), TimeProvider.System);
}
