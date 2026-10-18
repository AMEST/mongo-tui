using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Services;

public sealed class IndexServiceTests
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly IndexService _sut;

    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly CollectionRef Collection =
        new(DatabaseName.Create("app"), CollectionName.Create("users"));

    public IndexServiceTests() => _sut = new IndexService(_gateway);

    [Fact]
    public async Task Should_ListIndexes_When_Requested()
    {
        await _sut.ListAsync(Target, Collection, CancellationToken.None);

        A.CallTo(() => _gateway.ListIndexesAsync(Target, Collection, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_GetStats_When_Requested()
    {
        await _sut.GetStatsAsync(Target, Collection, CancellationToken.None);

        A.CallTo(() => _gateway.GetCollectionStatsAsync(Target, Collection, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_CreateIndex_When_Requested()
    {
        await _sut.CreateAsync(Target, Collection, "{ \"a\": 1 }", "a_1", true, true, CancellationToken.None);

        A.CallTo(() => _gateway.CreateIndexAsync(Target, Collection, "{ \"a\": 1 }", "a_1", true, true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_DropIndex_When_Requested()
    {
        await _sut.DropAsync(Target, Collection, "a_1", CancellationToken.None);

        A.CallTo(() => _gateway.DropIndexAsync(Target, Collection, "a_1", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
