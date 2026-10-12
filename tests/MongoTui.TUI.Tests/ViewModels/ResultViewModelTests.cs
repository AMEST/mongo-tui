using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Domain.Queries;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Tests.ViewModels;

public sealed class ResultViewModelTests
{
    private readonly ResultViewModel _sut = new();

    [Fact]
    public void Should_BuildColumnsAndRows_When_Loaded()
    {
        var result = new QueryResult
        {
            Kind = QueryResultKind.Documents,
            Documents = [BsonDocument.Parse("{ \"a\": 1, \"b\": \"x\" }")],
            Elapsed = TimeSpan.FromMilliseconds(5),
        };

        _sut.Load(result);

        _sut.Columns.Should().Equal("a", "b");
        _sut.Rows.Should().ContainSingle();
        _sut.Rows[0].Should().Equal("1", "x");
        _sut.Elapsed.Should().Be(TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public void Should_ProduceIndentedJson_When_Loaded()
    {
        var result = new QueryResult
        {
            Kind = QueryResultKind.Documents,
            Documents = [BsonDocument.Parse("{ \"a\": 1 }")],
        };

        _sut.Load(result);

        _sut.Json.Should().Contain("\"a\"");
        _sut.Json.Should().Contain("\n");
    }

    [Fact]
    public void Should_LeaveBlankForMissingField_When_DocumentsDiffer()
    {
        var result = new QueryResult
        {
            Kind = QueryResultKind.Documents,
            Documents =
            [
                BsonDocument.Parse("{ \"a\": 1 }"),
                BsonDocument.Parse("{ \"b\": 2 }"),
            ],
        };

        _sut.Load(result);

        _sut.Rows[0][1].Should().BeEmpty();
        _sut.Rows[1][0].Should().BeEmpty();
    }

    [Fact]
    public void Should_SetErrorAndClearRows_When_LoadError()
    {
        _sut.Load(new QueryResult { Kind = QueryResultKind.Documents, Documents = [BsonDocument.Parse("{ \"a\": 1 }")] });

        _sut.LoadError("boom");

        _sut.HasError.Should().BeTrue();
        _sut.Error.Should().Be("boom");
        _sut.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Should_RenderTableText_When_TableMode()
    {
        _sut.Load(new QueryResult
        {
            Kind = QueryResultKind.Documents,
            Documents = [BsonDocument.Parse("{ \"a\": 1, \"b\": 2 }")],
        });

        var text = _sut.Render();

        text.Should().Contain("a | b");
        text.Should().Contain("1 | 2");
    }

    [Fact]
    public void Should_RenderJson_When_JsonMode()
    {
        _sut.Load(new QueryResult { Kind = QueryResultKind.Documents, Documents = [BsonDocument.Parse("{ \"a\": 1 }")] });
        _sut.Mode = ResultViewMode.Json;

        _sut.Render().Should().Be(_sut.Json);
    }

    [Fact]
    public void Should_RenderError_When_HasError()
    {
        _sut.LoadError("boom");

        _sut.Render().Should().Be("boom");
    }
}
