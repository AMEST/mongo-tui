using AwesomeAssertions;
using MongoTui.TUI.Editor;

namespace MongoTui.TUI.Tests.Editor;

public sealed class SyntaxHighlighterTests
{
    [Fact]
    public void Should_ClassifyKeyword_When_KeywordUsed()
    {
        var tokens = SyntaxHighlighter.Tokenize("const x = 1");

        tokens[0].Should().Be(new SyntaxToken("const", SyntaxTokenKind.Keyword));
    }

    [Fact]
    public void Should_ClassifyString_When_QuotedWithEscape()
    {
        var tokens = SyntaxHighlighter.Tokenize("x = \"a\\\"b\"");

        tokens.Should().Contain(new SyntaxToken("\"a\\\"b\"", SyntaxTokenKind.String));
    }

    [Fact]
    public void Should_ClassifyCommentToEndOfLine_When_LineComment()
    {
        var tokens = SyntaxHighlighter.Tokenize("// note");

        tokens.Should().ContainSingle().Which.Kind.Should().Be(SyntaxTokenKind.Comment);
    }

    [Fact]
    public void Should_ClassifyNumber_When_Digits()
    {
        var tokens = SyntaxHighlighter.Tokenize("42");

        tokens.Should().ContainSingle().Which.Kind.Should().Be(SyntaxTokenKind.Number);
    }

    [Fact]
    public void Should_ReconstructLine_When_ConcatenatingTokens()
    {
        var line = "db.users.find({ age: 30 }) // query";

        var tokens = SyntaxHighlighter.Tokenize(line);

        string.Concat(tokens.Select(token => token.Text)).Should().Be(line);
    }

    [Fact]
    public void Should_SplitLines_When_TextIsMultiline()
    {
        var lines = SyntaxHighlighter.TokenizeLines("a\nb\nc");

        lines.Should().HaveCount(3);
    }
}
