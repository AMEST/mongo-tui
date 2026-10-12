namespace MongoTui.TUI.Editor;

/// <summary>Lexical class of a highlighted token.</summary>
public enum SyntaxTokenKind
{
    Default,
    Keyword,
    Identifier,
    String,
    Number,
    Comment,
    Operator,
    Punctuation,
}

/// <summary>A single highlighted span of source text.</summary>
public sealed record SyntaxToken(string Text, SyntaxTokenKind Kind);
