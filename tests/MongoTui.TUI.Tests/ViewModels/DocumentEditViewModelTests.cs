using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Tests.ViewModels;

public sealed class DocumentEditViewModelTests
{
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly DocumentService _documents;

    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly CollectionRef Collection =
        new(DatabaseName.Create("app"), CollectionName.Create("users"));

    public DocumentEditViewModelTests() => _documents = new DocumentService(_gateway);

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
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection, "{ \"_id\": 1, \"v\": 1 }")
        {
            Json = "{ \"_id\": 1, \"v\": 2 }",
        };

        var saved = await viewModel.SaveAsync(CancellationToken.None);

        saved.Should().BeTrue();
        A.CallTo(() => _gateway.ReplaceOneAsync(
                Target, Collection, A<string>._, "{ \"_id\": 1, \"v\": 2 }", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_Delete_When_EditingExisting()
    {
        var viewModel = new DocumentEditViewModel(_documents, Target, Collection, "{ \"_id\": 7 }");

        var deleted = await viewModel.DeleteAsync(CancellationToken.None);

        deleted.Should().BeTrue();
        A.CallTo(() => _gateway.DeleteOneAsync(Target, Collection, A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
