using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Web.Tests;

public sealed class DocumentDialogTests
{
    private static readonly CollectionRef Collection = new(DatabaseName.Create("app"), CollectionName.Create("items"));
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();

    [Fact]
    public async Task Should_SaveFreshTypedDocument_When_SelectedShellResultLostTypes()
    {
        var actual = new BsonDocument { { "_id", 1 }, { "double", new BsonDouble(42) },
            { "long", new BsonInt64(42) }, { "text", "fresh" } };
        Returns(actual);
        string? written = null;
        A.CallTo(() => _gateway.ReplaceOneAsync(A<MongoConnectionTarget>._, Collection, A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes(call => written = call.GetArgument<string>(3)).Returns(1L);
        await using var fixture = new DocumentDialogFixture(_gateway);
        await fixture.OpenAsync(new BsonDocument { { "_id", 1 }, { "double", 42 }, { "text", "stale" } }, Collection);

        await fixture.SaveAsync();

        fixture.Saved.Should().BeTrue();
        BsonTextCodec.ParseDocument(written!).ToBson().Should().Equal(actual.ToBson());
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_DisableWritesAndExplain_When_JsonWouldChangeDocument(string language)
    {
        Returns(new BsonDocument { { "_id", 1 }, { "literal", new BsonDocument("$date", "2020-01-01T00:00:00Z") } });
        await using var fixture = new DocumentDialogFixture(_gateway);
        fixture.Workspace.Text.SetLanguage(language);

        await fixture.OpenAsync(new BsonDocument("_id", 1), Collection);
        await fixture.SaveAsync();

        (await fixture.Renderer.AttributeAsync("primary", "disabled")).Should().Be(true);
        (await fixture.Renderer.TextAsync()).Should().Contain(fixture.Workspace.Text[LocalizationKeys.Documents.UnsafeJson]);
        fixture.Saved.Should().BeFalse();
        AssertNoReplacement();
    }

    [Fact]
    public async Task Should_DisableWrites_When_DocumentDisappeared()
    {
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, Collection, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(QueryResult.Empty(QueryResultKind.Documents));
        await using var fixture = new DocumentDialogFixture(_gateway);

        await fixture.OpenAsync(new BsonDocument("_id", 1), Collection);
        await fixture.SaveAsync();

        (await fixture.Renderer.TextAsync()).Should().Contain(fixture.Workspace.Text[LocalizationKeys.Documents.NotFound]);
        fixture.Saved.Should().BeFalse();
        AssertNoReplacement();
    }

    [Fact]
    public async Task Should_RejectSave_When_OnlyIdTypeChanged()
    {
        Returns(new BsonDocument("_id", 42));
        await using var fixture = new DocumentDialogFixture(_gateway);
        await fixture.OpenAsync(new BsonDocument("_id", 42), Collection);
        await fixture.EditAsync(BsonTextCodec.Format(new BsonDocument("_id", new BsonInt64(42))));

        await fixture.SaveAsync();

        (await fixture.Renderer.TextAsync()).Should().Contain(fixture.Workspace.Text[LocalizationKeys.Documents.IdChanged]);
        AssertNoReplacement();
    }

    [Fact]
    public async Task Should_RejectDelete_WhenOnlyIdTypeChanged()
    {
        Returns(new BsonDocument("_id", 42));
        await using var fixture = new DocumentDialogFixture(_gateway);
        await fixture.OpenAsync(new BsonDocument("_id", 42), Collection);
        await fixture.EditAsync(BsonTextCodec.Format(new BsonDocument("_id", new BsonInt64(42))));

        await fixture.DeleteAsync();

        (await fixture.Renderer.TextAsync()).Should().Contain(fixture.Workspace.Text[LocalizationKeys.Documents.IdChanged]);
        A.CallTo(() => _gateway.DeleteOneAsync(A<MongoConnectionTarget>._, Collection, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_DisableWritesAndExplain_WhenDocumentDisappearsBeforeMutation(bool delete)
    {
        Returns(new BsonDocument("_id", 42));
        A.CallTo(() => _gateway.ReplaceOneAsync(A<MongoConnectionTarget>._, Collection, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(0L);
        A.CallTo(() => _gateway.DeleteOneAsync(A<MongoConnectionTarget>._, Collection, A<string>._, A<CancellationToken>._)).Returns(0L);
        await using var fixture = new DocumentDialogFixture(_gateway);
        await fixture.OpenAsync(new BsonDocument("_id", 42), Collection);

        await (delete ? fixture.DeleteAsync() : fixture.SaveAsync());

        fixture.Saved.Should().BeFalse();
        (await fixture.Renderer.TextAsync()).Should().Contain(fixture.Workspace.Text[LocalizationKeys.Documents.NotFound]);
        (await fixture.Renderer.AttributeAsync("primary", "disabled")).Should().Be(true);
    }

    [Fact]
    public async Task Should_KeepOriginalCollection_WhenTabSourceChangesAfterOpening()
    {
        Returns(new BsonDocument("_id", 42));
        A.CallTo(() => _gateway.ReplaceOneAsync(A<MongoConnectionTarget>._, Collection, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(1L);
        await using var fixture = new DocumentDialogFixture(_gateway);
        await fixture.OpenAsync(new BsonDocument("_id", 42), Collection);
        fixture.Tab!.EditableCollection = new CollectionRef(Collection.Database, CollectionName.Create("another"));

        await fixture.SaveAsync();

        fixture.Saved.Should().BeTrue();
        A.CallTo(() => _gateway.ReplaceOneAsync(A<MongoConnectionTarget>._, Collection, A<string>._, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_InsertWithoutReload_WhenNewDocumentIsOpened()
    {
        await using var fixture = new DocumentDialogFixture(_gateway);
        await fixture.OpenAsync(new BsonDocument(), Collection, insert: true);

        await fixture.SaveAsync();

        fixture.Saved.Should().BeTrue();
        A.CallTo(() => _gateway.InsertOneAsync(A<MongoConnectionTarget>._, Collection, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, Collection, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNoReplacement();
    }

    [Fact]
    public async Task Should_NotReloadOrOfferSaving_When_SourceUnknown()
    {
        await using var fixture = new DocumentDialogFixture(_gateway);

        await fixture.OpenAsync(new BsonDocument("_id", 42), Collection, editable: false);

        (await fixture.Renderer.TextAsync()).Should().Contain(fixture.Workspace.Text[LocalizationKeys.Document.ReadOnly]);
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, Collection, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private void Returns(BsonDocument document) =>
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, Collection, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(new QueryResult { Kind = QueryResultKind.Documents, Documents = [document] });

    [Fact]
    public async Task Should_CancelReadAndKeepWritesDisabled_When_ClosedDuringReload()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<QueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = CancellationToken.None;
        A.CallTo(() => _gateway.FindAsync(A<MongoConnectionTarget>._, Collection, A<string>._,
            A<QueryExecutionOptions>._, A<CancellationToken>._)).ReturnsLazily(call =>
            {
                token = call.GetArgument<CancellationToken>(4);
                started.SetResult();
                return response.Task;
            });
        await using var fixture = new DocumentDialogFixture(_gateway);
        var mounting = fixture.OpenAsync(new BsonDocument("_id", 1), Collection);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        (await fixture.Renderer.AttributeAsync("primary", "disabled")).Should().Be(true);

        await fixture.Renderer.CloseAsync();
        response.SetResult(new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("_id", 1)] });
        await mounting.WaitAsync(TimeSpan.FromSeconds(3));

        token.IsCancellationRequested.Should().BeTrue();
        fixture.Saved.Should().BeFalse();
        AssertNoReplacement();
    }

    private void AssertNoReplacement() => A.CallTo(() => _gateway.ReplaceOneAsync(A<MongoConnectionTarget>._,
        Collection, A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
}
