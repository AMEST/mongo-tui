using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Diagnostics;
using System.Globalization;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Services;

public sealed class QueryTimeoutTests : IAsyncLifetime
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly IMongoShellSession _shell = A.Fake<IMongoShellSession>();
    private readonly IQueryHistoryRepository _history = A.Fake<IQueryHistoryRepository>();
    private readonly QueryService _queries;
    private readonly MongoConnectionTarget _target = new("mongodb://localhost:27017");
    private readonly CollectionRef _collection = new(DatabaseName.Create("demo"), CollectionName.Create("orders"));
    private int _seconds = 30;

    public QueryTimeoutTests()
    {
        var store = A.Fake<ISettingsStore>();
        A.CallTo(() => store.GetAsync("query.timeoutSeconds", A<CancellationToken>._))
            .ReturnsLazily(() => _seconds.ToString(CultureInfo.InvariantCulture));
        var factory = A.Fake<IMongoShellSessionFactory>();
        A.CallTo(() => factory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).Returns(_shell);
        A.CallTo(() => _shell.IsRunning).Returns(true);
        _queries = new(factory, _history, _gateway, new SettingsService(store), NullLogger<QueryService>.Instance);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _queries.DisposeAsync().AsTask();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_EnforceSameDeadline_WhenShellOrDriverQueryRuns(bool driver)
    {
        _seconds = 1;
        ArrangeWaitingQueries();

        var act = () => ExecuteAsync(driver, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        var failure = await act.Should().ThrowAsync<QueryTimeoutException>();
        failure.Which.Timeout.Should().Be(TimeSpan.FromSeconds(1));
        if (!driver)
            A.CallTo(() => _history.AddAsync(A<QueryHistoryEntry>.That.Matches(entry => !entry.Success),
                A<CancellationToken>.That.Matches(token => !token.IsCancellationRequested))).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_DisableDeadline_WhenTimeoutIsZero(bool driver)
    {
        _seconds = 0;
        var executionToken = new CancellationToken(true);
        A.CallTo(() => _shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Invokes(call => executionToken = call.GetArgument<CancellationToken>(2))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Invokes(call => executionToken = call.GetArgument<CancellationToken>(4))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));

        await ExecuteAsync(driver, CancellationToken.None);

        executionToken.CanBeCanceled.Should().BeFalse();
        if (driver)
            A.CallTo(() => _gateway.FindAsync(_target, _collection, "{}",
                A<QueryExecutionOptions>.That.Matches(options => options.Timeout == null), CancellationToken.None))
                .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PropagateUserCancellation_WhenCallerCancels(bool driver)
    {
        ArrangeWaitingQueries();
        using var caller = new CancellationTokenSource();
        var running = ExecuteAsync(driver, caller.Token);

        caller.Cancel();

        var act = () => running.WaitAsync(TimeSpan.FromSeconds(5));
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_ApplyUpdatedSetting_WhenNextDriverQueryRuns()
    {
        _seconds = 90;
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(QueryResult.Empty(QueryResultKind.Documents));
        await ExecuteAsync(true, CancellationToken.None);
        _seconds = 0;

        await ExecuteAsync(true, CancellationToken.None);

        A.CallTo(() => _gateway.FindAsync(_target, _collection, "{}",
            A<QueryExecutionOptions>.That.Matches(options => options.Timeout == TimeSpan.FromSeconds(90)),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _gateway.FindAsync(_target, _collection, "{}",
            A<QueryExecutionOptions>.That.Matches(options => options.Timeout == null),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private Task<QueryResult> ExecuteAsync(bool driver, CancellationToken cancellationToken) =>
        driver ? _queries.FindAsync(_target, _collection, "{}", new QueryExecutionOptions(), cancellationToken)
            : _queries.ExecuteAsync(new QueryExecutionContext
            {
                TabId = Guid.NewGuid(), ConnectionId = ConnectionId.New(), Target = _target,
                Database = "demo", Collection = "orders", Script = "db.orders.find().toArray()",
            }, cancellationToken);

    private void ArrangeWaitingQueries()
    {
        A.CallTo(() => _shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .ReturnsLazily((string _, string? _, CancellationToken token) => WaitForCancellationAsync(token));
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._))
            .ReturnsLazily((MongoConnectionTarget _, CollectionRef _, string _, QueryExecutionOptions _,
                CancellationToken token) => WaitForCancellationAsync(token));
    }

    private static async Task<QueryResult> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return QueryResult.Empty(QueryResultKind.Documents);
    }
}
