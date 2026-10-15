using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MongoTui.TUI.Editor;

/// <summary>Identifies a database, collection or field name at the query caret.</summary>
internal sealed record QueryCompletionContext(
    string? Database, string? Collection, string Prefix, int Start, int Length, char? Quote, bool CollectionAccess)
{
    public static QueryCompletionContext? Parse(string text, int cursor, string? database)
    {
        var before = text[..cursor];
        var masked = MaskLiterals(before, out var inComment);
        var roots = Regex.Matches(masked, @"\bdb\s*\.");
        if (inComment || roots.Count == 0)
            return null;

        var offset = roots[^1].Index + roots[^1].Length;
        var fragment = before[offset..];
        var sibling = Match(fragment, "^\\s*getSiblingDB\\s*\\(\\s*(['\"])(.*?)\\1\\s*\\)\\s*\\.");
        if (sibling.Success)
        {
            database = sibling.Groups[2].Value;
            offset += sibling.Length;
            fragment = before[offset..];
        }

        var name = Match(fragment, "^\\s*getSiblingDB\\s*\\(\\s*(?<quote>['\"])(?<prefix>[^'\"\\r\\n]*)$");
        if (name.Success)
            return Create(text, cursor, offset, name, null, null);
        if (database is null)
            return null;

        return ParseCollectionOrField(text, cursor, database, masked, offset, fragment);
    }

    private static QueryCompletionContext? ParseCollectionOrField(
        string text, int cursor, string database, string masked, int offset, string fragment)
    {
        var name = Match(fragment, "^\\s*getCollection\\s*\\(\\s*(?<quote>['\"])(?<prefix>[^'\"\\r\\n]*)$");
        if (name.Success)
            return Create(text, cursor, offset, name, database, null);
        name = Match(fragment, @"^\s*(?<prefix>[\p{L}\p{N}_$]*)$");
        if (name.Success)
            return Create(text, cursor, offset, name, database, null) with { CollectionAccess = true };

        var collection = Match(fragment, "^\\s*(?:getCollection\\s*\\(\\s*(['\"])(?<name>.*?)\\1\\s*\\)|(?<name>[\\p{L}\\p{N}_$]+))\\s*\\.");
        if (!collection.Success || masked[offset..].Contains(';'))
            return null;
        var arguments = masked[(offset + collection.Length)..];
        if (arguments.Count(character => character == '(') <= arguments.Count(character => character == ')'))
            return null;

        name = Match(fragment, "[,{]\\s*(?<quote>['\"]?)(?<prefix>[\\p{L}\\p{N}_$.]*)$");
        return name.Success && masked[offset + name.Index] is '{' or ','
            ? Create(text, cursor, offset, name, database, collection.Groups["name"].Value) : null;
    }

    public string Replacement(string candidate)
    {
        var escaped = JsonEncodedText.Encode(candidate, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();
        if (Quote is { } quote)
            return quote == '\'' ? escaped.Replace("'", "\\'", StringComparison.Ordinal) : escaped;
        var quoted = "\"" + escaped + "\"";
        if (CollectionAccess)
            return Match(candidate, @"^[\p{L}_$][\p{L}\p{N}_$]*$").Success
                ? candidate : $"getCollection({quoted})";
        return quoted;
    }

    private static QueryCompletionContext Create(
        string text, int cursor, int offset, Match match, string? database, string? collection)
    {
        var prefix = match.Groups["prefix"];
        var quote = match.Groups["quote"].Value;
        var end = cursor;
        while (end < text.Length && (quote.Length > 0
                   ? text[end] != quote[0] && text[end] is not '\r' and not '\n'
                   : char.IsLetterOrDigit(text[end]) || text[end] is '_' or '$' || collection is not null && text[end] == '.'))
            end++;
        return new(database, collection, prefix.Value, offset + prefix.Index,
            end - offset - prefix.Index, quote.Length == 0 ? null : quote[0], false);
    }

    private static Match Match(string input, string pattern) =>
        Regex.Match(input, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static string MaskLiterals(string text, out bool inComment)
    {
        var masked = text.ToCharArray();
        char? quote = null;
        var lineComment = false;
        var blockComment = false;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            var next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (lineComment && current == '\n') lineComment = false;
            if (quote is null && !lineComment && !blockComment)
            {
                if (current == '/' && next == '/') lineComment = true;
                else if (current == '/' && next == '*') blockComment = true;
                else if (current is '\'' or '"' or '`') quote = current;
                else continue;
                masked[index] = ' ';
                continue;
            }
            masked[index] = ' ';
            if (blockComment && current == '*' && next == '/')
            {
                masked[++index] = ' ';
                blockComment = false;
            }
            else if (quote is not null && current == '\\' && index + 1 < text.Length)
                masked[++index] = ' ';
            else if (quote == current) quote = null;
        }
        inComment = lineComment || blockComment;
        return new string(masked);
    }
}
