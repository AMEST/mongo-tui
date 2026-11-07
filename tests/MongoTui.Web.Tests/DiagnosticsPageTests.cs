using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.Web.Components.Panels;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

public sealed class DiagnosticsPageTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_ExcludeFastProfilerEntries_WhenSlowQueriesAreSelected(string language)
    {
        await using var fixture = new WorkspaceFixture(); fixture.Text.SetLanguage(language);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var gateway = A.Fake<IMongoDiagnosticsGateway>();
        var row = new ProfilerEntry { Timestamp = DateTimeOffset.UtcNow, Database = "demo", Collection = "slowOnly",
            Operation = QueryOperationKind.Find, Duration = TimeSpan.FromMilliseconds(300) };
        A.CallTo(() => gateway.ReadProfilerAsync(A<MongoConnectionTarget>._, "demo", A<DateTimeOffset?>._,
            A<TimeSpan>._, A<int>._, A<CancellationToken>._)).Returns(new[] { row, row with { Collection = "fastOnly", Duration = TimeSpan.FromMilliseconds(20) } });
        using var services = Services(fixture, gateway);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<DiagnosticsPage>([]);
        await renderer.ChangeAsync("#diagnostics-source", "ServerProfiler");

        await renderer.ClickAsync("#diagnostics-SlowQueries");

        var text = await renderer.TextAsync();
        text.Should().Contain("slowOnly").And.NotContain("fastOnly");
    }

    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public async Task Should_PrefillCurrentTabConnectionAndDatabase_WhenProfilerIsOpened(string language, bool otherConnection)
    {
        await using var fixture = new WorkspaceFixture(); fixture.Text.SetLanguage(language);
        var connection = await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        if (otherConnection) connection = await ConnectOtherAsync(fixture);
        var tab = fixture.Workspace.Open(connection.Profile.Id, "shop", "orders");
        var gateway = ProfilerGateway(); using var services = Services(fixture, gateway);
        await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<DiagnosticsPage>(new() { [nameof(DiagnosticsPage.CurrentTab)] = tab });

        await renderer.ClickAsync("#diagnostics-Profiler");

        ((await renderer.AttributeAsync("#diagnostics-connection", "value"))?.ToString()).Should().Be(connection.Profile.Id.ToString());
        ((await renderer.AttributeAsync("#diagnostics-database", "value"))?.ToString()).Should().Be("shop");
        ((await renderer.AttributeAsync("#profiler-scope", "value"))?.ToString()).Should().Be("shop");
        A.CallTo(() => gateway.GetProfilerStateAsync(connection.Target, "shop", A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => gateway.GetProfilerStateAsync(A<MongoConnectionTarget>._, "demo", A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => gateway.SetProfilerLevelAsync(A<MongoConnectionTarget>._, A<string>._, A<ProfilingLevel>._,
            A<ProfilerOptions>._, A<ProfilerFilterAction>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_KeepManualDatabaseAndScope_WhenDiagnosticsRefreshes()
    {
        await using var fixture = new WorkspaceFixture(); await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "shop", null);
        using var services = Services(fixture, ProfilerGateway()); await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<DiagnosticsPage>(new() { [nameof(DiagnosticsPage.CurrentTab)] = tab });
        await renderer.ChangeAsync("#diagnostics-database", "archive"); await renderer.ClickAsync("#diagnostics-Profiler");
        await renderer.ChangeAsync("#profiler-scope", "archive,logs");

        await renderer.ClickAsync("#diagnostics-refresh");

        ((await renderer.AttributeAsync("#diagnostics-database", "value"))?.ToString()).Should().Be("archive");
        ((await renderer.AttributeAsync("#profiler-scope", "value"))?.ToString()).Should().Be("archive,logs");
        tab.Database.Should().Be("shop");
    }

    [Fact]
    public async Task Should_FallBackToActiveConnection_WhenCurrentTabConnectionIsDisconnected()
    {
        await using var fixture = new WorkspaceFixture(); await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = new WebTab(ConnectionId.New(), "disconnected", null);
        using var services = Services(fixture, ProfilerGateway()); await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<DiagnosticsPage>(new() { [nameof(DiagnosticsPage.CurrentTab)] = tab });

        await renderer.ClickAsync("#diagnostics-Profiler");

        ((await renderer.AttributeAsync("#diagnostics-connection", "value"))?.ToString()).Should().Be(fixture.Profile.Id.ToString());
        ((await renderer.AttributeAsync("#profiler-scope", "value"))?.ToString()).Should().Be("demo");
    }

    [Fact]
    public async Task Should_PrefillTabDatabase_WhenReturningToItsConnection()
    {
        await using var fixture = new WorkspaceFixture(); await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var other = await ConnectOtherAsync(fixture); var tab = fixture.Workspace.Open(fixture.Profile.Id, "shop", null);
        using var services = Services(fixture, ProfilerGateway()); await using var renderer = new ComponentTestRenderer(services);
        await renderer.MountAsync<DiagnosticsPage>(new() { [nameof(DiagnosticsPage.CurrentTab)] = tab });
        await renderer.ChangeAsync("#diagnostics-connection", other.Profile.Id.ToString());
        await renderer.ClickAsync("#diagnostics-Profiler");
        ((await renderer.AttributeAsync("#profiler-scope", "value"))?.ToString()).Should().Be("demo");

        await renderer.ChangeAsync("#diagnostics-connection", fixture.Profile.Id.ToString());

        ((await renderer.AttributeAsync("#profiler-scope", "value"))?.ToString()).Should().Be("shop");
    }

    private static async Task<WebConnection> ConnectOtherAsync(WorkspaceFixture fixture)
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "Other", "mongodb://localhost:27018");
        A.CallTo(() => fixture.Repository.GetAsync(profile.Id, A<CancellationToken>._)).Returns(new StoredConnection(profile, null));
        return await fixture.Catalog.ConnectAsync(profile, CancellationToken.None);
    }

    private static IMongoDiagnosticsGateway ProfilerGateway()
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>();
        A.CallTo(() => gateway.GetProfilerStateAsync(A<MongoConnectionTarget>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, string database, CancellationToken _) => new ProfilerState(database, ProfilingLevel.Off, new(), null, true, null));
        return gateway;
    }

    private static ServiceProvider Services(WorkspaceFixture fixture, IMongoDiagnosticsGateway gateway)
    {
        var registrations = new ServiceCollection(); registrations.AddLogging();
        registrations.AddSingleton(fixture.Text); registrations.AddSingleton(fixture.Catalog);
        var settings = new DiagnosticsSettingsService(fixture.Settings);
        var insights = new QueryInsightsService(A.Fake<IQueryMetricsStore>(), A.Fake<IQueryMetricsRepository>(), settings);
        registrations.AddSingleton(insights);
        registrations.AddSingleton(new DiagnosticsServices(new DiagnosticsService(gateway, TimeProvider.System),
            new ExplainService(gateway, new SettingsService(fixture.Settings), settings), insights, settings, new QueryMetricsQueue()));
        return registrations.BuildServiceProvider();
    }
}
