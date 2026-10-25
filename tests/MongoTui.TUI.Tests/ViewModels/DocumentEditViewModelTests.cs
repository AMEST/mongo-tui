using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Localization;
using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Tests.ViewModels;

public sealed class DocumentEditViewModelTests
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly DocumentService _documents;

    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly CollectionRef Collection =
        new(DatabaseName.Create("app"), CollectionName.Create("users"));

    public DocumentEditViewModelTests()
    {
        _documents = new DocumentService(_gateway, NullLogger<DocumentService>.Instance);
        A.CallTo(() => _gateway.FindAsync(Target, Collection, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .ReturnsLazily(call => Task.FromResult(new QueryResult { Kind = QueryResultKind.Documents,
                Documents = [BsonTextCodec.ParseDocument(call.GetArgument<string>(2)!)] }));
        A.CallTo(() => _gateway.ReplaceOneAsync(Target, Collection, A<string>._, A<string>._, A<CancellationToken>._)).Returns(1L);
        A.CallTo(() => _gateway.DeleteOneAsync(Target, Collection, A<string>._, A<CancellationToken>._)).Returns(1L);
    }

    [Fact]
    public void Should_BeNew_When_NoExistingJson()
    {
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection);

        viewModel.IsNew.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Insert_When_NewAndValid()
    {
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection) { Json = "{ \"_id\": 1 }" };

        var saved = await viewModel.SaveAsync(CancellationToken.None);

        saved.Should().BeTrue();
        A.CallTo(() => _gateway.InsertOneAsync(Target, Collection, "{ \"_id\": 1 }", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ReturnFalseAndSetError_When_JsonInvalid()
    {
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection) { Json = "oops" };

        var saved = await viewModel.SaveAsync(CancellationToken.None);

        saved.Should().BeFalse();
        viewModel.ValidationError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Replace_When_EditingExisting()
    {
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection, BsonDocument.Parse("{ \"_id\": 1, \"v\": 1 }"));

        await viewModel.InitializeAsync(CancellationToken.None);
        viewModel.Json = "{ \"_id\": 1, \"v\": 2 }";
        var saved = await viewModel.SaveAsync(CancellationToken.None);

        saved.Should().BeTrue();
        A.CallTo(() => _gateway.ReplaceOneAsync(
                Target, Collection, A<string>._, "{ \"_id\": 1, \"v\": 2 }", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_Delete_When_EditingExisting()
    {
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection, BsonDocument.Parse("{ \"_id\": 7 }"));

        await viewModel.InitializeAsync(CancellationToken.None);
        var deleted = await viewModel.DeleteAsync(CancellationToken.None);

        deleted.Should().BeTrue();
        A.CallTo(() => _gateway.DeleteOneAsync(Target, Collection, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_DefaultFilterToIdAndIndentJson_When_EditingExisting()
    {
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection, BsonDocument.Parse("{ \"_id\": 7, \"v\": 1 }"));

        await viewModel.InitializeAsync(CancellationToken.None);

        viewModel.IsNew.Should().BeFalse();
        viewModel.Filter.Should().Contain("_id").And.Contain("7");
        viewModel.Json.Should().Contain("\n");
    }

    [Fact]
    public async Task Should_LoadDocument_When_FilterMatches()
    {
        A.CallTo(() => _gateway.FindAsync(
                Target,
                Collection,
                A<string>._,
                A<QueryExecutionOptions>._,
                A<CancellationToken>._))
            .Returns(new QueryResult
            {
                Kind = QueryResultKind.Documents,
                Documents = [BsonDocument.Parse("{ \"_id\": 9, \"name\": \"z\" }")],
            });

        var viewModel = new DocumentEditViewModel(_documents, Target, Collection) { Filter = "{ \"_id\": 9 }" };

        var loaded = await viewModel.LoadAsync(CancellationToken.None);

        loaded.Should().BeTrue();
        viewModel.Json.Should().Contain("9");
    }

    [Fact]
    public async Task Should_ReturnFalse_When_LoadFindsNothing()
    {
        A.CallTo(() => _gateway.FindAsync(
                Target,
                Collection,
                A<string>._,
                A<QueryExecutionOptions>._,
                A<CancellationToken>._))
            .Returns(QueryResult.Empty(QueryResultKind.Documents));

        var viewModel = new DocumentEditViewModel(_documents, Target, Collection) { Filter = "{ \"_id\": 404 }" };

        var loaded = await viewModel.LoadAsync(CancellationToken.None);

        loaded.Should().BeFalse();
        viewModel.ValidationError.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNot_WriteDocument_When_IdChanged(bool delete)
    {
        var model = new DocumentEditViewModel(_documents, Target, Collection, BsonDocument.Parse("{\"_id\":1}"));
        await model.InitializeAsync(CancellationToken.None);
        model.Json = "{\"_id\":2}";

        var succeeded = await (delete ? model.DeleteAsync(CancellationToken.None) : model.SaveAsync(CancellationToken.None));

        succeeded.Should().BeFalse();
        model.ValidationErrorKey.Should().Be(LocalizationKeys.Documents.IdChanged);
        A.CallTo(() => _gateway.ReplaceOneAsync(Target, Collection, A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _gateway.DeleteOneAsync(Target, Collection, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldNot_ReportSuccess_When_DocumentNoLongerExists(bool delete)
    {
        A.CallTo(() => _gateway.ReplaceOneAsync(Target, Collection, A<string>._, A<string>._, A<CancellationToken>._)).Returns(0L);
        A.CallTo(() => _gateway.DeleteOneAsync(Target, Collection, A<string>._, A<CancellationToken>._)).Returns(0L);
        var model = new DocumentEditViewModel(_documents, Target, Collection, BsonDocument.Parse("{\"_id\":1}"));

        await model.InitializeAsync(CancellationToken.None);
        var succeeded = await (delete ? model.DeleteAsync(CancellationToken.None) : model.SaveAsync(CancellationToken.None));

        succeeded.Should().BeFalse();
        model.ValidationErrorKey.Should().Be(LocalizationKeys.Documents.NotFound);
    }

    [Fact]
    public async Task Should_UpdateOriginalIdentity_When_AnotherDocumentLoaded()
    {
        A.CallTo(() => _gateway.FindAsync(Target, Collection, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [BsonDocument.Parse("{\"_id\":9}")] });
        var model = new DocumentEditViewModel(_documents, Target, Collection, BsonDocument.Parse("{\"_id\":1}"));
        await model.LoadAsync(CancellationToken.None);

        var saved = await model.SaveAsync(CancellationToken.None);

        saved.Should().BeTrue();
        A.CallTo(() => _gateway.ReplaceOneAsync(Target, Collection,
            A<string>.That.Matches(filter => BsonDocument.Parse(filter)["_id"] == 9), A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldNot_Write_When_SelectionHasNotBeenReloaded()
    {
        var model = new DocumentEditViewModel(_documents, Target, Collection, new BsonDocument("_id", 1));

        (await model.SaveAsync(CancellationToken.None)).Should().BeFalse();
        (await model.DeleteAsync(CancellationToken.None)).Should().BeFalse();

        model.CanWrite.Should().BeFalse();
        A.CallTo(() => _gateway.ReplaceOneAsync(Target, Collection, A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _gateway.DeleteOneAsync(Target, Collection, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ShouldNot_SaveOldBuffer_When_SubsequentLoadIsUnsafe()
    {
        var model = new DocumentEditViewModel(_documents, Target, Collection, new BsonDocument("_id", 1));
        await model.InitializeAsync(CancellationToken.None);
        A.CallTo(() => _gateway.FindAsync(Target, Collection, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents,
                Documents = [new BsonDocument { { "_id", 2 }, { "literal", new BsonDocument("$numberLong", "42") } }] });

        var loaded = await model.LoadAsync(CancellationToken.None);

        loaded.Should().BeFalse();
        (await model.SaveAsync(CancellationToken.None)).Should().BeFalse();
        model.ValidationErrorKey.Should().Be(LocalizationKeys.Documents.UnsafeJson);
        A.CallTo(() => _gateway.ReplaceOneAsync(Target, Collection, A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
