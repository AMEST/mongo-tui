using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class QueryServiceMetricsTests
{
    private readonly MetricsFixture _fixture = new();
    private readonly IMongoGateway _inner = A.Fake<IMongoGateway>();
    private readonly IMongoShellSessionFactory _factory = A.Fake<IMongoShellSessionFactory>();
    private readonly IMongoShellSession _shell = A.Fake<IMongoShellSession>();
    private readonly CollectionRef _collection = new(DatabaseName.Create("shop"), CollectionName.Create("orders"));
    private readonly MongoConnectionTarget _target;

    public QueryServiceMetricsTests()
    {
        _target = new MongoConnectionTarget("mongodb://localhost") { ConnectionId = _fixture.Connection };
        A.CallTo(() => _factory.CreateAsync(_target, A<CancellationToken>._)).Returns(_shell);
        A.CallTo(() => _shell.IsRunning).Returns(true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RecordExactlyOnce_WhenGatewayAndQueryServiceAreInstrumented(bool driver)
    {
        var expected = QueryResult.Empty(QueryResultKind.Documents);
        A.CallTo(() => _inner.FindAsync(_target, _collection, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(expected);
        A.CallTo(() => _shell.EvaluateAsync("db.orders.find({})", "shop", A<CancellationToken>._)).Returns(expected);
        await using var service = CreateService();

        await ExecuteAsync(service, driver);

        var observation = _fixture.Store.GetRecent(new(), 10).Should().ContainSingle().Subject;
        observation.Source.Should().Be(driver ? QuerySource.Driver : QuerySource.Shell);
        observation.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RecordTimeoutInsteadOfCancellation_WhenSharedDeadlineExpires(bool driver)
    {
        A.CallTo(() => _fixture.Preferences.GetAsync("query.timeoutSeconds", A<CancellationToken>._)).Returns("1");
        A.CallTo(() => _inner.FindAsync(_target, _collection, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .ReturnsLazily(call => WaitAsync(call.GetArgument<CancellationToken>(4)));
        A.CallTo(() => _shell.EvaluateAsync("db.orders.find({})", "shop", A<CancellationToken>._))
            .ReturnsLazily(call => WaitAsync(call.GetArgument<CancellationToken>(2)));
        await using var service = CreateService();

        var act = () => ExecuteAsync(service, driver).WaitAsync(TimeSpan.FromSeconds(5));

        await act.Should().ThrowAsync<QueryTimeoutException>();
        var observation = _fixture.Store.GetRecent(new(), 10).Should().ContainSingle().Subject;
        observation.TimedOut.Should().BeTrue();
        observation.Cancelled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_RecordFailure_WhenShellSessionCannotStart()
    {
        A.CallTo(() => _factory.CreateAsync(_target, A<CancellationToken>._)).ThrowsAsync(new IOException("private"));
        await using var service = CreateService();

        var act = () => ExecuteAsync(service, false);

        await act.Should().ThrowAsync<IOException>();
        _fixture.Store.GetRecent(new(), 10).Should().ContainSingle().Which.Success.Should().BeFalse();
    }

    private QueryService CreateService() => new(_factory, A.Fake<IQueryHistoryRepository>(),
        new InstrumentedMongoGateway(_inner, _fixture.Recorder), new SettingsService(_fixture.Preferences),
        NullLogger<QueryService>.Instance, _fixture.Recorder);

    private Task<QueryResult> ExecuteAsync(QueryService service, bool driver) => driver
        ? service.FindAsync(_target, _collection, "{}", new(), CancellationToken.None)
        : service.ExecuteAsync(new QueryExecutionContext
        {
            ConnectionId = _fixture.Connection, Target = _target, TabId = Guid.NewGuid(),
            Database = "shop", Collection = "orders", Script = "db.orders.find({})",
        }, CancellationToken.None);

    private static async Task<QueryResult> WaitAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return QueryResult.Empty(QueryResultKind.Documents);
    }
}
