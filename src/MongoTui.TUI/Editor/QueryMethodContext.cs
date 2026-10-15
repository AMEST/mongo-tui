namespace MongoTui.TUI.Editor;

/// <summary>Tracks known method return types through a balanced shell call chain.</summary>
internal static class QueryMethodContext
{
    public static QueryCompletionContext? Parse(string text, int cursor, string masked, int offset, string? database)
    {
        var scope = QueryMethodScope.Database;
        var position = offset;
        while (position <= cursor)
        {
            SkipWhitespace(masked, ref position);
            var start = position;
            while (position < cursor && IsIdentifier(masked[position])) position++;
            var name = text[start..position];
            SkipWhitespace(masked, ref position);
            if (position == cursor)
                return Create(text, cursor, start, name, database, scope);
            if (masked[position] == '(')
            {
                var method = QueryMethodCatalog.Get(scope).FirstOrDefault(candidate => candidate.Name == name);
                if (method?.ResultScope is not { } result || !SkipArguments(masked, ref position)) return null;
                scope = result;
                SkipWhitespace(masked, ref position);
            }
            else if (scope == QueryMethodScope.Database && name.Length > 0
                     && !QueryMethodCatalog.Get(scope).Any(method => method.Name == name))
                scope = QueryMethodScope.Collection;
            else return null;
            if (position >= cursor || masked[position++] != '.') return null;
        }
        return null;
    }

    private static QueryCompletionContext? Create(
        string text, int cursor, int start, string prefix, string? database, QueryMethodScope scope)
    {
        if (scope == QueryMethodScope.Database && (prefix.Length == 0
            || !QueryMethodCatalog.Get(scope).Any(method => method.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            return null;
        var end = cursor;
        while (end < text.Length && IsIdentifier(text[end])) end++;
        var suffix = end;
        SkipWhitespace(text, ref suffix);
        return new(database, null, prefix, start, end - start, null, false, scope, suffix < text.Length && text[suffix] == '(');
    }

    private static bool SkipArguments(string text, ref int position)
    {
        var brackets = new Stack<char>();
        while (position < text.Length)
        {
            var character = text[position++];
            if (character is '(' or '[' or '{') brackets.Push(character);
            else if (character is ')' or ']' or '}')
            {
                var opening = character switch { ')' => '(', ']' => '[', _ => '{' };
                if (!brackets.TryPop(out var previous) || previous != opening) return false;
                if (brackets.Count == 0) return true;
            }
        }
        return false;
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
    }

    private static bool IsIdentifier(char character) => char.IsLetterOrDigit(character) || character is '_' or '$';
}
