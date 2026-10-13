using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Tests.Services;

public sealed class DocumentServiceTests
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly DocumentService _sut;

    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly CollectionRef Collection =
        new(DatabaseName.Create("app"), CollectionName.Create("users"));

    public DocumentServiceTests() => _sut = new DocumentService(_gateway);

    [Fact]
    public void Should_BuildIdFilter_When_IdPresent()
    {
        var filter = DocumentService.BuildIdFilter("{ \"_id\": 1, \"name\": \"a\" }");

        filter.Should().Contain("_id");
    }

    [Fact]
    public void ShouldThrow_DomainException_When_IdMissing()
    {
        var act = () => DocumentService.BuildIdFilter("{ \"name\": \"a\" }");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldThrow_DomainException_When_JsonInvalid()
    {
        var act = () => DocumentService.Parse("{ not json }");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public async Task Should_InsertDocument_When_JsonValid()
    {
        await _sut.InsertAsync(Target, Collection, "{ \"_id\": 1 }", CancellationToken.None);

        A.CallTo(() => _gateway.InsertOneAsync(Target, Collection, "{ \"_id\": 1 }", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_InsertingInvalidJson()
    {
        var act = async () => await _sut.InsertAsync(Target, Collection, "oops", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_ReplaceDocument_When_JsonValid()
    {
        await _sut.ReplaceAsync(Target, Collection, "{ \"_id\": 1 }", "{ \"_id\": 1, \"v\": 2 }", CancellationToken.None);

        A.CallTo(() => _gateway.ReplaceOneAsync(
                Target, Collection, "{ \"_id\": 1 }", "{ \"_id\": 1, \"v\": 2 }", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_DeleteDocument_When_FilterProvided()
    {
        await _sut.DeleteAsync(Target, Collection, "{ \"_id\": 1 }", CancellationToken.None);

        A.CallTo(() => _gateway.DeleteOneAsync(Target, Collection, "{ \"_id\": 1 }", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ReturnIndentedDocument_When_FindOneMatches()
    {
        A.CallTo(() => _gateway.FindAsync(
                Target,
                Collection,
                "{ \"_id\": 1 }",
                A<QueryExecutionOptions>._,
                A<CancellationToken>._))
            .Returns(new QueryResult
            {
                Kind = QueryResultKind.Documents,
                Documents = [BsonDocument.Parse("{ \"_id\": 1 }")],
            });

        var document = await _sut.FindOneAsync(Target, Collection, "{ \"_id\": 1 }", CancellationToken.None);

        document.Should().NotBeNull();
        document.Should().Contain("\n");
    }

    [Fact]
    public async Task Should_ReturnNull_When_FindOneHasNoMatch()
    {
        A.CallTo(() => _gateway.FindAsync(
                Target,
                Collection,
                A<string>._,
                A<QueryExecutionOptions>._,
                A<CancellationToken>._))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));

        var document = await _sut.FindOneAsync(Target, Collection, "{ \"_id\": 404 }", CancellationToken.None);

        document.Should().BeNull();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_FilterInvalid()
    {
        var act = async () => await _sut.FindOneAsync(Target, Collection, "oops", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
