using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Services;

public sealed class DatabaseServiceTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly DatabaseName Database = DatabaseName.Create("app");
    private static readonly CollectionRef Collection = new(Database, CollectionName.Create("users"));

    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly DatabaseService _sut;

    public DatabaseServiceTests() => _sut = new DatabaseService(_gateway);

    [Fact]
    public async Task Should_ReturnStats_When_Requested()
    {
        var stats = new DatabaseStats { Name = "app", CollectionCount = 2 };
        A.CallTo(() => _gateway.GetDatabaseStatsAsync(Target, Database, A<CancellationToken>._)).Returns(stats);

        var result = await _sut.GetStatsAsync(Target, Database, CancellationToken.None);

        result.Should().BeSameAs(stats);
    }

    [Fact]
    public async Task Should_DropDatabase_When_Requested()
    {
        await _sut.DropDatabaseAsync(Target, Database, CancellationToken.None);

        A.CallTo(() => _gateway.DropDatabaseAsync(Target, Database, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_DropCollection_When_Requested()
    {
        await _sut.DropCollectionAsync(Target, Collection, CancellationToken.None);

        A.CallTo(() => _gateway.DropCollectionAsync(Target, Collection, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ClearCollection_When_Requested()
    {
        await _sut.ClearCollectionAsync(Target, Collection, CancellationToken.None);

        A.CallTo(() => _gateway.ClearCollectionAsync(Target, Collection, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
