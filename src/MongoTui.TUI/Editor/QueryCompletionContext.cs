using MongoTui.Application.Queries;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MongoTui.TUI.Editor;

/// <summary>Identifies a database, collection, field or shell method at the query caret.</summary>
internal sealed record QueryCompletionContext(
    string? Database, string? Collection, string Prefix, int Start, int Length, char? Quote, bool MemberAccess,
    QueryMethodScope? MethodScope = null, bool HasCallSuffix = false)
{
    private const string QuotedName = """(?<quote>['"])(?<name>(?:\\[^\r\n]|(?!\k<quote>)[^\\\r\n])*)\k<quote>""";
    private const string QuotedPrefix = """(?<quote>['"])(?<prefix>(?:\\[^\r\n]|(?!\k<quote>)[^\\\r\n])*)""";
    private const string MemberPrefix = @"^\s*(?<prefix>[\p{L}\p{N}_$]*)$";

    public static QueryCompletionContext? Parse(string text, int cursor, string? database)
    {
        var before = text[..cursor];
        var masked = MaskLiterals(before, out var inComment);
        var roots = Regex.Matches(masked, @"\bdb\s*\.");
        if (inComment || roots.Count == 0)
            return null;

        var offset = roots[^1].Index + roots[^1].Length;
        var fragment = before[offset..];
        var sibling = Match(fragment, @"^\s*getSiblingDB\s*\(\s*" + QuotedName + @"\s*\)\s*\.");
        if (sibling.Success)
        {
            database = JavaScriptStringLiteral.Decode(sibling.Groups["name"].Value);
            offset += sibling.Length;
            fragment = before[offset..];
        }

        var name = Match(fragment, @"^\s*getSiblingDB\s*\(\s*" + QuotedPrefix + "$");
        if (name.Success)
            return Create(text, cursor, offset, name, null, null);
        if (QueryMethodContext.Parse(text, cursor, masked, offset, database) is { } method)
            return method;
        if (!sibling.Success)
        {
            name = Match(fragment, MemberPrefix);
            if (name.Success)
                return Create(text, cursor, offset, name, null, null, memberAccess: true);
        }
        if (database is null)
            return null;

        return ParseCollectionOrField(text, cursor, database, masked, offset, fragment);
    }

    private static QueryCompletionContext? ParseCollectionOrField(
        string text, int cursor, string database, string masked, int offset, string fragment)
    {
        var name = Match(fragment, @"^\s*getCollection\s*\(\s*" + QuotedPrefix + "$");
        if (name.Success)
            return Create(text, cursor, offset, name, database, null);
        name = Match(fragment, MemberPrefix);
        if (name.Success)
            return Create(text, cursor, offset, name, database, null, memberAccess: true);

        var collection = Match(fragment, @"^\s*(?:getCollection\s*\(\s*" + QuotedName + @"\s*\)|(?<name>[\p{L}\p{N}_$]+))\s*\.");
        if (!collection.Success || masked[offset..].Contains(';'))
            return null;
        var arguments = masked[(offset + collection.Length)..];
        if (arguments.Count(character => character == '(') <= arguments.Count(character => character == ')'))
            return null;

        var collectionName = collection.Groups["quote"].Success
            ? JavaScriptStringLiteral.Decode(collection.Groups["name"].Value) : collection.Groups["name"].Value;
        name = Match(fragment, @"[,{]\s*(?:" + QuotedPrefix + @"|(?<prefix>[\p{L}\p{N}_$.]*))$");
        return collectionName is not null && name.Success && masked[offset + name.Index] is '{' or ','
            ? Create(text, cursor, offset, name, database, collectionName) : null;
    }

    public string Replacement(string candidate)
    {
        if (MethodScope is { } scope)
            return HasCallSuffix ? candidate : QueryMethodCatalog.Get(scope).Single(method => method.Name == candidate).Replacement;
        var escaped = JsonEncodedText.Encode(candidate, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();
        if (Quote is { } quote)
            return quote == '\'' ? escaped.Replace("'", "\\'", StringComparison.Ordinal) : escaped;
        var quoted = "\"" + escaped + "\"";
        var method = Database is null ? "getSiblingDB" : "getCollection";
        return MemberAccess ? $"{method}({quoted})" : quoted;
    }

    private static QueryCompletionContext? Create(
        string text, int cursor, int offset, Match match, string? database, string? collection, bool memberAccess = false)
    {
        var prefix = match.Groups["prefix"];
        var quote = match.Groups["quote"].Value;
        var decoded = quote.Length == 0 ? prefix.Value : JavaScriptStringLiteral.Decode(prefix.Value);
        if (decoded is null) return null;
        var start = offset + prefix.Index;
        var end = quote.Length == 0 ? cursor : JavaScriptStringLiteral.FindEnd(text, start, quote[0]);
        while (quote.Length == 0 && end < text.Length
               && (char.IsLetterOrDigit(text[end]) || text[end] is '_' or '$' || collection is not null && text[end] == '.'))
            end++;
        return new(database, collection, decoded, start, end - start, quote.Length == 0 ? null : quote[0], memberAccess);
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
