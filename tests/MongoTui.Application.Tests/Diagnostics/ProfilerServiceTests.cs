using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class ProfilerServiceTests
{
    private readonly IMongoDiagnosticsGateway _gateway = A.Fake<IMongoDiagnosticsGateway>();
    private readonly MongoConnectionTarget _target = new("mongodb://localhost");
    private readonly ProfilerState _off = new("shop", ProfilingLevel.Off, new(), null, true, null);
    [Fact]
    public async Task ShouldThrow_BeforeChangingServer_WhenAllWasNotConfirmed()
    {
        var service = new DiagnosticsService(_gateway, TimeProvider.System);

        var act = () => service.ApplyProfilerAsync(_target, new(["shop"], ProfilingLevel.All, new()), CancellationToken.None);

        await act.Should().ThrowAsync<DiagnosticsException>();
        A.CallTo(() => _gateway.SetProfilerLevelAsync(A<MongoConnectionTarget>._, A<string>._, A<ProfilingLevel>._,
            A<ProfilerOptions>._, A<ProfilerFilterAction>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
    [Fact]
    public async Task Should_KeepSuccessfulDatabase_WhenAnotherDatabaseFails()
    {
        var service = new DiagnosticsService(_gateway, TimeProvider.System);
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "shop", A<CancellationToken>._)).Returns(_off);
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "denied", A<CancellationToken>._)).ThrowsAsync(new DiagnosticsException("Diagnostics.Error.Unauthorized"));
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, "shop", ProfilingLevel.Slow, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).Returns(_off with { Level = ProfilingLevel.Slow });

        var results = await service.ApplyProfilerAsync(_target, new(["shop", "denied"], ProfilingLevel.Slow, new()), CancellationToken.None);

        results[0].Actual!.Level.Should().Be(ProfilingLevel.Slow);
        results[1].Error.Should().Be("Diagnostics.Error.Unauthorized");
        service.Leases.Should().ContainSingle();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RestoreOnlyOwnedState_WhenLeaseExpires(bool changedElsewhere)
    {
        var service = new DiagnosticsService(_gateway, TimeProvider.System);
        var applied = _off with { Level = ProfilingLevel.All };
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "shop", A<CancellationToken>._)).Returns(_off);
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, "shop", ProfilingLevel.All, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).Returns(applied);
        await service.ApplyProfilerAsync(_target, new(["shop"], ProfilingLevel.All, new(), ConfirmAll: true), CancellationToken.None);
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "shop", A<CancellationToken>._)).Returns(changedElsewhere ? _off : applied);

        await service.RestoreAsync(service.Leases.Single(), CancellationToken.None);

        var call = A.CallTo(() => _gateway.RestoreProfilerAsync(_target, _off, A<CancellationToken>._));
        if (changedElsewhere) call.MustNotHaveHappened(); else call.MustHaveHappenedOnceExactly();
        service.Leases.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_RefreshActualState_WhenProfilerChangesExternally()
    {
        var service = new DiagnosticsService(_gateway, TimeProvider.System);
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "shop", A<CancellationToken>._)).Returns(_off);
        await service.GetProfilerStateAsync(_target, "shop", CancellationToken.None);
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "shop", A<CancellationToken>._)).Returns(_off with { Level = ProfilingLevel.All });

        await service.RefreshKnownProfilerStatesAsync(CancellationToken.None);

        service.States(_target).Should().ContainSingle().Which.Level.Should().Be(ProfilingLevel.All);
        service.Leases.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_AttemptEveryRestore_WhenOneDatabaseFailsDuringDisconnect()
    {
        var service = new DiagnosticsService(_gateway, TimeProvider.System);
        var states = new Dictionary<string, ProfilerState> { ["shop"] = _off, ["orders"] = _off with { Database = "orders" } };
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, string database, CancellationToken _) => states[database]);
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, A<string>._, ProfilingLevel.All, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).ReturnsLazily((MongoConnectionTarget _, string database,
                ProfilingLevel _, ProfilerOptions _, ProfilerFilterAction _, CancellationToken _) => states[database] = states[database] with { Level = ProfilingLevel.All });
        await service.ApplyProfilerAsync(_target, new(["shop", "orders"], ProfilingLevel.All, new(), ConfirmAll: true), CancellationToken.None);
        A.CallTo(() => _gateway.RestoreProfilerAsync(_target, A<ProfilerState>.That.Matches(state => state.Database == "shop"), A<CancellationToken>._))
            .ThrowsAsync(new DiagnosticsException("Diagnostics.Error.Unauthorized"));

        var act = () => service.DisconnectAsync(_target, CancellationToken.None);

        await act.Should().ThrowAsync<DiagnosticsException>();
        A.CallTo(() => _gateway.RestoreProfilerAsync(_target, A<ProfilerState>.That.Matches(state => state.Database == "orders"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        service.Leases.Should().BeEmpty(); service.States(_target).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_RestoreEveryDatabase_WhenAnotherOwnedScopeChangedGlobalThreshold()
    {
        var service = new DiagnosticsService(_gateway, TimeProvider.System);
        var global = new ProfilerOptions();
        var states = new Dictionary<string, ProfilerState> { ["shop"] = _off, ["orders"] = _off with { Database = "orders" } };
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, string database, CancellationToken _) => states[database] with { Options = global });
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, A<string>._, A<ProfilingLevel>._, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).ReturnsLazily((MongoConnectionTarget _, string database,
                ProfilingLevel level, ProfilerOptions options, ProfilerFilterAction _, CancellationToken _) =>
                { global = options; return states[database] = states[database] with { Level = level, Options = options }; });
        A.CallTo(() => _gateway.RestoreProfilerAsync(_target, A<ProfilerState>._, A<CancellationToken>._))
            .Invokes((MongoConnectionTarget _, ProfilerState state, CancellationToken _) => { global = state.Options; states[state.Database] = state; });
        await service.ApplyProfilerAsync(_target, new(["shop"], ProfilingLevel.Slow, new() { SlowMs = 250 }), CancellationToken.None);
        await service.ApplyProfilerAsync(_target, new(["orders"], ProfilingLevel.Slow, new() { SlowMs = 500 }), CancellationToken.None);

        await service.DisconnectAsync(_target, CancellationToken.None);

        states.Values.Should().OnlyContain(state => state.Level == ProfilingLevel.Off);
        global.SlowMs.Should().Be(100); service.Leases.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_KeepServerSlowProfiler_WhenDisconnectRestorationIsDisabled()
    {
        var service = new DiagnosticsService(_gateway, TimeProvider.System);
        A.CallTo(() => _gateway.GetProfilerStateAsync(_target, "shop", A<CancellationToken>._)).Returns(_off);
        A.CallTo(() => _gateway.SetProfilerLevelAsync(_target, "shop", ProfilingLevel.Slow, A<ProfilerOptions>._,
            A<ProfilerFilterAction>._, A<CancellationToken>._)).Returns(_off with { Level = ProfilingLevel.Slow });
        await service.ApplyProfilerAsync(_target, new(["shop"], ProfilingLevel.Slow, new() { SlowMs = 250 },
            RestoreOnExit: false, AutoDisableMinutes: 0), CancellationToken.None);
        service.Leases.Single().ExpiresAt.Should().BeNull();

        await service.DisconnectAsync(_target, CancellationToken.None);

        A.CallTo(() => _gateway.RestoreProfilerAsync(_target, A<ProfilerState>._, A<CancellationToken>._)).MustNotHaveHappened();
        service.Leases.Should().BeEmpty();
    }
}
