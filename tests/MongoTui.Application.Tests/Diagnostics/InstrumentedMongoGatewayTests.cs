using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class InstrumentedMongoGatewayTests
{
    private readonly MetricsFixture _fixture = new();
    private readonly IMongoGateway _inner = A.Fake<IMongoGateway>();
    private readonly CollectionRef _collection = new(DatabaseName.Create("shop"), CollectionName.Create("orders"));
    private readonly MongoConnectionTarget _target;
    private readonly InstrumentedMongoGateway _gateway;

    public InstrumentedMongoGatewayTests()
    {
        _target = new MongoConnectionTarget("mongodb://user:secret@localhost") { ConnectionId = _fixture.Connection };
        _gateway = new InstrumentedMongoGateway(_inner, _fixture.Recorder);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_RecordReadOnceAndForwardSnapshot_WhenFindOrAggregateRuns(bool aggregate)
    {
        var expected = new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("secret", 42)] };
        var options = new QueryExecutionOptions { Skip = 10, Limit = 20 };
        using var cancellation = new CancellationTokenSource();
        A.CallTo(() => _inner.FindAsync(_target, _collection, "{ secret: 42 }", options, cancellation.Token)).Returns(expected);
        A.CallTo(() => _inner.AggregateAsync(_target, _collection, "[]", options, cancellation.Token)).Returns(expected);

        var result = await (aggregate
            ? _gateway.AggregateAsync(_target, _collection, "[]", options, cancellation.Token)
            : _gateway.FindAsync(_target, _collection, "{ secret: 42 }", options, cancellation.Token));

        result.Should().BeSameAs(expected);
        var observation = _fixture.Store.GetRecent(new(), 10).Should().ContainSingle().Subject;
        observation.Operation.Should().Be(aggregate ? QueryOperationKind.Aggregate : QueryOperationKind.Find);
        observation.ConnectionId.Should().Be(_fixture.Connection);
        observation.ReturnedCount.Should().Be(1);
        observation.AffectedCount.Should().BeNull();
    }

    [Theory]
    [InlineData("insert", QueryOperationKind.Insert, 1L)]
    [InlineData("replace", QueryOperationKind.Update, 3L)]
    [InlineData("delete", QueryOperationKind.Delete, 2L)]
    [InlineData("clear", QueryOperationKind.Delete, null)]
    [InlineData("count", QueryOperationKind.Count, null)]
    public async Task Should_SeparateAffectedFromReturned_WhenDocumentOperationRuns(string operation, QueryOperationKind kind, long? affected)
    {
        A.CallTo(() => _inner.ReplaceOneAsync(_target, _collection, "{}", "{}", A<CancellationToken>._)).Returns(3L);
        A.CallTo(() => _inner.DeleteOneAsync(_target, _collection, "{}", A<CancellationToken>._)).Returns(2L);
        A.CallTo(() => _inner.CountAsync(_target, _collection, "{}", A<CancellationToken>._)).Returns(100L);

        await ExecuteAsync(operation);

        var observation = _fixture.Store.GetRecent(new(), 10).Should().ContainSingle().Subject;
        observation.Operation.Should().Be(kind);
        observation.AffectedCount.Should().Be(affected);
        observation.ReturnedCount.Should().BeNull();
    }

    [Fact]
    public async Task Should_NotRecord_WhenSamplingOrLoadingMetadata()
    {
        var options = new QueryExecutionOptions { Purpose = QueryPurpose.Internal };

        await Task.WhenAll(_gateway.FindAsync(_target, _collection, "{}", options, CancellationToken.None),
            _gateway.ListDatabasesAsync(_target, CancellationToken.None),
            _gateway.GetCollectionStatsAsync(_target, _collection, CancellationToken.None));

        _fixture.Store.GetRecent(new(), 1).Should().BeEmpty();
    }

    private Task ExecuteAsync(string operation) => operation switch
    {
        "insert" => _gateway.InsertOneAsync(_target, _collection, "{}", CancellationToken.None),
        "replace" => _gateway.ReplaceOneAsync(_target, _collection, "{}", "{}", CancellationToken.None),
        "delete" => _gateway.DeleteOneAsync(_target, _collection, "{}", CancellationToken.None),
        "clear" => _gateway.ClearCollectionAsync(_target, _collection, CancellationToken.None),
        _ => _gateway.CountAsync(_target, _collection, "{}", CancellationToken.None),
    };
}
