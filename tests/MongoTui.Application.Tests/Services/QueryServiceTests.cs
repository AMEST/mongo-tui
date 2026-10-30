using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Diagnostics;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Services;

public sealed class QueryServiceTests
{
    private readonly IMongoShellSessionFactory _sessionFactory = A.Fake<IMongoShellSessionFactory>();
    private readonly IQueryHistoryRepository _history = A.Fake<IQueryHistoryRepository>();
    private readonly IMongoShellSession _session = A.Fake<IMongoShellSession>();
    private readonly QueryService _sut;

    public QueryServiceTests()
    {
        A.CallTo(() => _session.IsRunning).Returns(true);
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));
        A.CallTo(() => _sessionFactory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Returns(_session);

        _sut = new QueryService(_sessionFactory, _history, A.Fake<IMongoGateway>(),
            new SettingsService(A.Fake<ISettingsStore>()), NullLogger<QueryService>.Instance);
    }

    [Fact]
    public async Task Should_ReuseSession_When_ExecutingTwiceInSameTab()
    {
        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);
        await _sut.ExecuteAsync(context, CancellationToken.None);

        A.CallTo(() => _sessionFactory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_RecreateSession_When_SessionNotRunning()
    {
        A.CallTo(() => _session.IsRunning).Returns(false);

        await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);
        await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        A.CallTo(() => _sessionFactory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task Should_RecordSuccessfulHistory_When_Executed()
    {
        await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        A.CallTo(() => _history.AddAsync(
                A<QueryHistoryEntry>.That.Matches(entry => entry.Success && entry.Script == "db.users.find()"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_WrapQueryExecutionException_When_SessionThrows()
    {
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("boom"));

        var act = async () => await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        await act.Should().ThrowAsync<QueryExecutionException>();
        A.CallTo(() => _history.AddAsync(
                A<QueryHistoryEntry>.That.Matches(entry => !entry.Success),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_PropagateCancellation_When_Cancelled()
    {
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        var act = async () => await _sut.ExecuteAsync(CreateContext(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_DisposeSession_When_TabClosed()
    {
        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        await _sut.CloseTabAsync(context.TabId, CancellationToken.None);

        A.CallTo(() => _session.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("app")]
    [InlineData("other")]
    [InlineData(null)]
    public async Task Should_PassDatabaseAndOriginalScript_WhenQueryExecuted(string? database)
    {
        var context = CreateContext() with { Database = database };

        await _sut.ExecuteAsync(context, CancellationToken.None);

        A.CallTo(() => _session.EvaluateAsync(context.Script, database, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _history.AddAsync(
            A<QueryHistoryEntry>.That.Matches(entry => entry.Database == database && entry.Script == context.Script),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_UseChangedDatabase_WhenSessionReused()
    {
        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        await _sut.ExecuteAsync(context with { Database = "other" }, CancellationToken.None);

        A.CallTo(() => _sessionFactory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _session.EvaluateAsync(context.Script, "other", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static QueryExecutionContext CreateContext() =>
        new()
        {
            TabId = Guid.NewGuid(),
            ConnectionId = ConnectionId.New(),
            Target = new MongoConnectionTarget("mongodb://localhost:27017"),
            Script = "db.users.find()",
            Database = "app",
            Collection = "users",
        };
}
