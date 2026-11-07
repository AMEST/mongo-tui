using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Models;
using MongoTui.Application.Queries;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Infrastructure.Mongo.Driver;

namespace MongoTui.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class MongoDiagnosticsTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    [Fact]
    public async Task Should_ApplyReadAndRestoreProfiler_WhenDatabaseIsAvailable()
    {
        using var driver = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var gateway = new DriverMongoDiagnosticsGateway(driver); var target = new MongoConnectionTarget(fixture.ConnectionString);
        var database = "diag_" + Guid.NewGuid().ToString("N");
        var original = await gateway.GetProfilerStateAsync(target, database, CancellationToken.None);
        try
        {
            var actual = await gateway.SetProfilerLevelAsync(target, database, ProfilingLevel.All, new(), ProfilerFilterAction.Clear, CancellationToken.None);

            actual.Level.Should().Be(ProfilingLevel.All);
            (await gateway.GetServerStatusAsync(target, CancellationToken.None)).Version.Should().NotBeNullOrEmpty();
            var context = new QueryExecutionContext { Target = target, ConnectionId = ConnectionId.New(), TabId = Guid.NewGuid(), Database = database, Script = "db.orders.find({})" };
            var request = ExplainQuerySource.TryCreate(context, ExplainVerbosity.ExecutionStats)!;
            var raw = await gateway.ExplainAsync(target, request, CancellationToken.None);
            raw.Contains("queryPlanner").Should().BeTrue();
            var rows = await gateway.ReadProfilerAsync(target, database, null, TimeSpan.Zero, 100, CancellationToken.None);
            rows.Should().NotBeNull();
        }
        finally
        {
            await gateway.RestoreProfilerAsync(target, original, CancellationToken.None);
            await driver.DropDatabaseAsync(target, DatabaseName.Create(database), CancellationToken.None);
        }
    }

    [Fact]
    public async Task Should_KeepServerSlowOptions_WhenApplicationDisconnectsWithoutRestoration()
    {
        using var driver = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
        var gateway = new DriverMongoDiagnosticsGateway(driver); var target = new MongoConnectionTarget(fixture.ConnectionString);
        var database = "slow_" + Guid.NewGuid().ToString("N");
        var original = await gateway.GetProfilerStateAsync(target, database, CancellationToken.None);
        try
        {
            var service = new DiagnosticsService(gateway, TimeProvider.System);
            await service.ApplyProfilerAsync(target, new([database], ProfilingLevel.Slow, new() { SlowMs = 275, SampleRate = .25 },
                RestoreOnExit: false, AutoDisableMinutes: 0), CancellationToken.None);
            await service.DisconnectAsync(target, CancellationToken.None);
            using var reopened = new DriverMongoGateway(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));

            var actual = await new DriverMongoDiagnosticsGateway(reopened).GetProfilerStateAsync(target, database, CancellationToken.None);

            actual.Level.Should().Be(ProfilingLevel.Slow); actual.Options.SlowMs.Should().Be(275); actual.Options.SampleRate.Should().Be(.25);
        }
        finally
        {
            await gateway.RestoreProfilerAsync(target, original, CancellationToken.None);
            await driver.DropDatabaseAsync(target, DatabaseName.Create(database), CancellationToken.None);
        }
    }
}
