using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Databases;
using MongoTui.Domain.Exceptions;
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
    public async Task Should_CreateFirstCollection_WhenDatabaseDoesNotExist()
    {
        A.CallTo(() => _gateway.ListDatabasesAsync(Target, A<CancellationToken>._)).Returns(Array.Empty<DatabaseInfo>());

        await _sut.CreateDatabaseAsync(Target, Collection, CancellationToken.None);

        A.CallTo(() => _gateway.CreateCollectionAsync(Target, Collection, CancellationToken.None)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("app")]
    [InlineData("APP")]
    public async Task ShouldThrow_DomainException_WhenDatabaseAlreadyExists(string name)
    {
        A.CallTo(() => _gateway.ListDatabasesAsync(Target, A<CancellationToken>._)).Returns(new[] { new DatabaseInfo(name, true) });

        var act = () => _sut.CreateDatabaseAsync(Target, Collection, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("A database with this name already exists.");
        A.CallTo(() => _gateway.CreateCollectionAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_WhenFirstCollectionIsReserved()
    {
        var collection = new CollectionRef(Database, CollectionName.Create("system.version"));

        var act = () => _sut.CreateDatabaseAsync(Target, collection, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        A.CallTo(() => _gateway.ListDatabasesAsync(A<MongoConnectionTarget>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _gateway.CreateCollectionAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_PropagateServerError_WhenDatabaseCreationFails()
    {
        A.CallTo(() => _gateway.CreateCollectionAsync(Target, Collection, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Not authorized."));

        var act = () => _sut.CreateDatabaseAsync(Target, Collection, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Not authorized.");
    }

    [Fact]
    public async Task Should_ForwardCancellation_WhenCreatingDatabase()
    {
        using var cancellation = new CancellationTokenSource();

        await _sut.CreateDatabaseAsync(Target, Collection, cancellation.Token);

        A.CallTo(() => _gateway.ListDatabasesAsync(Target, cancellation.Token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _gateway.CreateCollectionAsync(Target, Collection, cancellation.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_CreateCollection_When_OrdinaryNameIsProvided()
    {
        await _sut.CreateCollectionAsync(Target, Collection, CancellationToken.None);

        A.CallTo(() => _gateway.CreateCollectionAsync(Target, Collection, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("system.version")]
    [InlineData("system.sessions")]
    public async Task ShouldThrow_DomainException_When_CreatingCollectionWithReservedName(string name)
    {
        var collection = new CollectionRef(Database, CollectionName.Create(name));

        var act = () => _sut.CreateCollectionAsync(Target, collection, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Collection name must not start with 'system.'.");
        A.CallTo(() => _gateway.CreateCollectionAsync(A<MongoConnectionTarget>._, A<CollectionRef>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

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
