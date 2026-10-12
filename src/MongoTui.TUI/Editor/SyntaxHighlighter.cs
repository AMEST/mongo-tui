namespace MongoTui.TUI.Editor;

/// <summary>Lightweight JavaScript tokenizer used for editor syntax highlighting.</summary>
public static class SyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "var", "let", "const", "function", "return", "if", "else", "for", "while",
        "new", "this", "true", "false", "null", "undefined", "await", "async",
        "try", "catch", "finally", "throw", "use", "db",
    };

    private const string Operators = "+-*/%=<>!&|?:";

    public static IReadOnlyList<IReadOnlyList<SyntaxToken>> TokenizeLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(Tokenize)
            .ToList();

    public static IReadOnlyList<SyntaxToken> Tokenize(string line)
    {
        var tokens = new List<SyntaxToken>();
        var index = 0;

        while (index < line.Length)
        {
            var current = line[index];

            if (char.IsWhiteSpace(current))
            {
                var start = index;
                while (index < line.Length && char.IsWhiteSpace(line[index]))
                {
                    index++;
                }

                tokens.Add(new SyntaxToken(line[start..index], SyntaxTokenKind.Default));
            }
            else if (current == '/' && index + 1 < line.Length && line[index + 1] == '/')
            {
                tokens.Add(new SyntaxToken(line[index..], SyntaxTokenKind.Comment));
                index = line.Length;
            }
            else if (current is '"' or '\'' or '`')
            {
                tokens.Add(ReadString(line, ref index));
            }
            else if (char.IsDigit(current))
            {
                var start = index;
                while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] == '.'))
                {
                    index++;
                }

                tokens.Add(new SyntaxToken(line[start..index], SyntaxTokenKind.Number));
            }
            else if (char.IsLetter(current) || current is '_' or '$')
            {
                var start = index;
                while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] is '_' or '$'))
                {
                    index++;
                }

                var word = line[start..index];
                var kind = Keywords.Contains(word) ? SyntaxTokenKind.Keyword : SyntaxTokenKind.Identifier;
                tokens.Add(new SyntaxToken(word, kind));
            }
            else if (Operators.Contains(current, StringComparison.Ordinal))
            {
                tokens.Add(new SyntaxToken(current.ToString(), SyntaxTokenKind.Operator));
                index++;
            }
            else
            {
                tokens.Add(new SyntaxToken(current.ToString(), SyntaxTokenKind.Punctuation));
                index++;
            }
        }

        return tokens;
    }

    private static SyntaxToken ReadString(string line, ref int index)
    {
        var start = index;
        var quote = line[index];
        index++;

        while (index < line.Length && line[index] != quote)
        {
            if (line[index] == '\\' && index + 1 < line.Length)
            {
                index += 2;
                continue;
            }

            index++;
        }

        if (index < line.Length)
        {
            index++;
        }

        return new SyntaxToken(line[start..index], SyntaxTokenKind.String);
    }
}
