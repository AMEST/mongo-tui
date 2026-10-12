using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Services;

public sealed class AutocompleteServiceTests
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly AutocompleteService _sut;

    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");

    public AutocompleteServiceTests() => _sut = new AutocompleteService(_gateway);

    [Fact]
    public async Task Should_SuggestDatabases_When_NoDatabaseGiven()
    {
        A.CallTo(() => _gateway.ListDatabasesAsync(Target, A<CancellationToken>._))
            .Returns(new List<DatabaseInfo>
            {
                new("admin", false),
                new("app", false),
                new("logs", false),
            });

        var suggestions = await _sut.GetSuggestionsAsync(Target, null, null, "a", CancellationToken.None);

        suggestions.Should().Equal("admin", "app");
    }

    [Fact]
    public async Task Should_SuggestCollections_When_DatabaseGiven()
    {
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns(new List<string> { "orders", "users" });

        var suggestions = await _sut.GetSuggestionsAsync(Target, "app", null, "u", CancellationToken.None);

        suggestions.Should().Equal("users");
    }

    [Fact]
    public async Task Should_SuggestFields_When_CollectionGiven()
    {
        A.CallTo(() => _gateway.FindAsync(Target, A<CollectionRef>._, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult
            {
                Kind = QueryResultKind.Documents,
                Documents = [BsonDocument.Parse("{ \"name\": \"a\", \"age\": 1 }")],
            });

        var suggestions = await _sut.GetSuggestionsAsync(Target, "app", "users", "n", CancellationToken.None);

        suggestions.Should().Equal("name");
    }

    [Fact]
    public async Task Should_CacheFields_When_RequestedTwice()
    {
        A.CallTo(() => _gateway.FindAsync(Target, A<CollectionRef>._, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult
            {
                Kind = QueryResultKind.Documents,
                Documents = [BsonDocument.Parse("{ \"name\": \"a\" }")],
            });

        await _sut.GetSuggestionsAsync(Target, "app", "users", string.Empty, CancellationToken.None);
        await _sut.GetSuggestionsAsync(Target, "app", "users", string.Empty, CancellationToken.None);

        A.CallTo(() => _gateway.FindAsync(Target, A<CollectionRef>._, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
