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

public sealed class CollectionSchemaServiceTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");

    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly CollectionSchemaService _sut;

    public CollectionSchemaServiceTests() => _sut = new CollectionSchemaService(_gateway);

    [Fact]
    public async Task Should_DescribeDatabaseCollectionsAndFieldTypes_When_Sampled()
    {
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns<IReadOnlyList<string>>(["orders", "users"]);
        var documents = new[]
        {
            new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "name", "Ann" },
                { "age", 30 },
                { "active", true },
                { "score", 1.5 },
                { "tags", new BsonArray { "a", "b" } },
                { "address", new BsonDocument { { "city", "X" }, { "zip", "123" } } },
                { "items", new BsonArray { new BsonDocument { { "sku", "s1" } } } },
                { "note", BsonNull.Value },
            },
            new BsonDocument { { "name", BsonNull.Value }, { "age", 31L } },
        };
        A.CallTo(() => _gateway.FindAsync(
                Target, A<CollectionRef>._, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = documents });

        var schema = await _sut.DescribeAsync(Target, "app", "users", CancellationToken.None);

        schema.Collection.Database.Value.Should().Be("app");
        schema.Collection.Collection.Value.Should().Be("users");
        schema.Collections.Should().Equal("orders", "users");

        var fields = schema.Fields.ToDictionary(field => field.Path, field => field.Type);
        fields["_id"].Should().Be("objectId");
        fields["name"].Should().Be("string | null");
        fields["age"].Should().Be("int | long");
        fields["active"].Should().Be("bool");
        fields["score"].Should().Be("double");
        fields["tags"].Should().Be("array<string>");
        fields["address"].Should().Be("object");
        fields["address.city"].Should().Be("string");
        fields["address.zip"].Should().Be("string");
        fields["items"].Should().Be("array<object>");
        fields["items.sku"].Should().Be("string");
        fields["note"].Should().Be("null");
    }

    [Fact]
    public async Task Should_RequestBoundedSample_When_Describing()
    {
        A.CallTo(() => _gateway.ListCollectionsAsync(Target, A<DatabaseName>._, A<CancellationToken>._))
            .Returns<IReadOnlyList<string>>([]);
        A.CallTo(() => _gateway.FindAsync(
                Target, A<CollectionRef>._, "{}", A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));

        var schema = await _sut.DescribeAsync(Target, "app", "users", CancellationToken.None);

        schema.Fields.Should().BeEmpty();
        A.CallTo(() => _gateway.FindAsync(
            Target,
            A<CollectionRef>._,
            "{}",
            A<QueryExecutionOptions>.That.Matches(options => options.Limit == 20),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
