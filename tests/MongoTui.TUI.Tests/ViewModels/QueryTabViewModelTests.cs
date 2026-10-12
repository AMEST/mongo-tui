using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Tests.ViewModels;

public sealed class QueryTabViewModelTests
{
    private readonly IMongoShellSessionFactory _sessionFactory = A.Fake<IMongoShellSessionFactory>();
    private readonly IQueryHistoryRepository _history = A.Fake<IQueryHistoryRepository>();
    private readonly IMongoShellSession _session = A.Fake<IMongoShellSession>();
    private readonly QueryService _queryService;

    public QueryTabViewModelTests()
    {
        A.CallTo(() => _session.IsRunning).Returns(true);
        A.CallTo(() => _sessionFactory.CreateAsync(A<MongoConnectionTarget>._, A<CancellationToken>._))
            .Returns(_session);
        _queryService = new QueryService(_sessionFactory, _history);
    }

    [Fact]
    public async Task Should_LoadResult_When_ExecuteSucceeds()
    {
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<CancellationToken>._))
            .Returns(new QueryResult
            {
                Kind = QueryResultKind.Scalar,
                Documents = [BsonDocument.Parse("{ \"a\": 1 }")],
            });
        var tab = CreateTab();

        await tab.ExecuteAsync(CancellationToken.None);

        tab.Result.HasError.Should().BeFalse();
        tab.Result.Documents.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_LoadError_When_ExecutionThrows()
    {
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("boom"));
        var tab = CreateTab();

        await tab.ExecuteAsync(CancellationToken.None);

        tab.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ResetIsExecuting_When_Finished()
    {
        A.CallTo(() => _session.EvaluateAsync(A<string>._, A<CancellationToken>._))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));
        var tab = CreateTab();

        await tab.ExecuteAsync(CancellationToken.None);

        tab.IsExecuting.Should().BeFalse();
    }

    private QueryTabViewModel CreateTab() =>
        new(
            _queryService,
            ConnectionId.New(),
            new MongoConnectionTarget("mongodb://localhost:27017"),
            "Query 1",
            "app",
            "users",
            "db.users.find()");
}
