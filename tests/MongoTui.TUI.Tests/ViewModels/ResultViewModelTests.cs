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
    public void Should_RenderRawOutput_When_RawMode()
    {
        _sut.Load(new QueryResult
        {
            Kind = QueryResultKind.Scalar,
            Documents = [],
            RawOutput = "migration complete\n42 documents updated",
        });
        _sut.Mode = ResultViewMode.Raw;

        _sut.Render().Should().Be("migration complete\n42 documents updated");
        _sut.DocumentIndexAtRow(0).Should().BeNull();
    }

    [Fact]
    public void Should_RenderError_When_HasError()
    {
        _sut.LoadError("boom");

        _sut.Render().Should().Be("boom");
    }

    [Fact]
    public void Should_ResolveDocument_When_TableRowIsOnDocument()
    {
        LoadTwoDocuments();
        _sut.Mode = ResultViewMode.Table;

        _sut.DocumentIndexAtRow(2).Should().Be(0);
        _sut.DocumentIndexAtRow(3).Should().Be(1);
    }

    [Fact]
    public void Should_ReturnNull_When_TableRowIsHeaderOrOutOfRange()
    {
        LoadTwoDocuments();
        _sut.Mode = ResultViewMode.Table;

        _sut.DocumentIndexAtRow(0).Should().BeNull();
        _sut.DocumentIndexAtRow(1).Should().BeNull();
        _sut.DocumentIndexAtRow(4).Should().BeNull();
    }

    [Fact]
    public void Should_ResolveDocument_When_TreeRowIsInsideBlock()
    {
        LoadTwoDocuments();
        _sut.Mode = ResultViewMode.Tree;

        var lines = _sut.Render().Split('\n');

        _sut.DocumentIndexAtRow(RowContaining(lines, "\"_id\" : 1")).Should().Be(0);
        _sut.DocumentIndexAtRow(RowContaining(lines, "\"_id\" : 2")).Should().Be(1);
    }

    [Fact]
    public void Should_ResolveDocument_When_JsonRowIsInsideArrayElement()
    {
        LoadTwoDocuments();
        _sut.Mode = ResultViewMode.Json;

        var lines = _sut.Render().Split('\n');

        _sut.DocumentIndexAtRow(RowContaining(lines, "\"_id\" : 1")).Should().Be(0);
        _sut.DocumentIndexAtRow(RowContaining(lines, "\"_id\" : 2")).Should().Be(1);
    }

    [Fact]
    public void Should_ReturnNull_When_NoDocuments()
    {
        _sut.Load(new QueryResult { Kind = QueryResultKind.Scalar, Documents = [] });

        _sut.DocumentIndexAtRow(-1).Should().BeNull();
        _sut.DocumentIndexAtRow(0).Should().BeNull();
    }

    private void LoadTwoDocuments() =>
        _sut.Load(new QueryResult
        {
            Kind = QueryResultKind.Documents,
            Documents =
            [
                BsonDocument.Parse("{ \"_id\": 1, \"name\": \"a\" }"),
                BsonDocument.Parse("{ \"_id\": 2, \"name\": \"b\" }"),
            ],
        });

    private static int RowContaining(string[] lines, string text) =>
        Array.FindIndex(lines, line => line.Contains(text, StringComparison.Ordinal));
}
