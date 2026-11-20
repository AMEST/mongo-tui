using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Diagnostics;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class QueryMetricsRecorderTests
{
    private readonly MetricsFixture _fixture = new();

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public async Task Should_ClassifySlowUsingStrictThreshold_WhenOperationCompletes(int milliseconds, bool slow)
    {
        var expected = new object();

        var actual = await ObserveAsync(() =>
        {
            _fixture.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
            return Task.FromResult(expected);
        });

        actual.Should().BeSameAs(expected);
        var observation = _fixture.Store.GetRecent(new(), 1).Single();
        observation.Duration.Should().Be(TimeSpan.FromMilliseconds(milliseconds));
        observation.IsSlow.Should().Be(slow);
        observation.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(QueryPurpose.Internal)]
    [InlineData(QueryPurpose.User)]
    public async Task Should_ExcludeInternalCalls_WhenRecording(QueryPurpose purpose)
    {
        var executed = false;

        await ObserveAsync(() => { executed = true; return Task.FromResult(42); }, purpose);

        executed.Should().BeTrue();
        _fixture.Store.Summarize(new()).Total.Should().Be(purpose == QueryPurpose.Internal ? 0 : 1);
    }

    [Theory]
    [InlineData("error", false, false)]
    [InlineData("cancel", true, false)]
    [InlineData("timeout", false, true)]
    public async Task Should_PreserveOriginalException_WhenOperationFails(string kind, bool cancelled, bool timedOut)
    {
        Exception expected = kind switch
        {
            "cancel" => new OperationCanceledException(),
            "timeout" => new QueryTimeoutException(TimeSpan.FromSeconds(1), new OperationCanceledException()),
            _ => new InvalidOperationException("private-filter"),
        };

        var actual = await Record.ExceptionAsync(() => ObserveAsync(() => Task.FromException<int>(expected)));

        actual.Should().BeSameAs(expected);
        var observation = _fixture.Store.GetRecent(new(), 1).Single();
        observation.Success.Should().BeFalse();
        observation.Cancelled.Should().Be(cancelled);
        observation.TimedOut.Should().Be(timedOut);
        observation.ReturnedCount.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PreserveResultAndFailure_WhenMetricsStoreThrows(bool fail)
    {
        var store = A.Fake<IQueryMetricsStore>();
        A.CallTo(() => store.Record(A<QueryObservation>._)).Throws(new InvalidOperationException("storage"));
        var recorder = new QueryMetricsRecorder(store, _fixture.Settings,
            new SlowQueryLogger(NullLogger<SlowQueryLogger>.Instance), _fixture.Clock);
        var expected = new InvalidOperationException("query");
        var result = 0;

        var exception = await Record.ExceptionAsync(async () => result = await recorder.ObserveAsync(
            _fixture.Connection, "db", "col", QueryOperationKind.Find, QuerySource.Driver, QueryPurpose.User,
            () => fail ? Task.FromException<int>(expected) : Task.FromResult(42)));

        exception.Should().BeSameAs(fail ? expected : null);
        result.Should().Be(fail ? 0 : 42);
    }

    [Fact]
    public async Task Should_NotInspectResult_WhenInternalOperationRuns()
    {
        var actual = await _fixture.Recorder.ObserveAsync(_fixture.Connection, "db", "col", QueryOperationKind.Find,
            QuerySource.Driver, QueryPurpose.Internal, () => Task.FromResult(42), _ => throw new Exception());

        actual.Should().Be(42);
        _fixture.Store.Summarize(new()).Total.Should().Be(0);
    }

    [Fact]
    public async Task Should_NotRecord_WhenMetricsDisabled()
    {
        await _fixture.Settings.SaveAsync(new() { MetricsEnabled = false }, CancellationToken.None);

        var result = await ObserveAsync(() => Task.FromResult(42));

        result.Should().Be(42);
        _fixture.Store.GetRecent(new(), 1).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_NotRecord_WhenConnectionHasNoProfileIdentity()
    {
        var result = await _fixture.Recorder.ObserveAsync(null, "db", "col", QueryOperationKind.Find,
            QuerySource.Driver, QueryPurpose.User, () => Task.FromResult(42));

        result.Should().Be(42);
        _fixture.Store.GetRecent(new(), 1).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_AwaitBackgroundCompletion_WhenOperationIsAsynchronous()
    {
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = ObserveAsync(() => pending.Task);
        _fixture.Store.GetRecent(new(), 1).Should().BeEmpty();

        await Task.Run(() => pending.SetResult(42));

        (await operation.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(42);
        _fixture.Store.GetRecent(new(), 1).Should().HaveCount(1);
    }

    private Task<T> ObserveAsync<T>(Func<Task<T>> execute, QueryPurpose purpose = QueryPurpose.User) =>
        _fixture.Recorder.ObserveAsync(_fixture.Connection, "db", "col", QueryOperationKind.Find,
            QuerySource.Driver, purpose, execute);
}
