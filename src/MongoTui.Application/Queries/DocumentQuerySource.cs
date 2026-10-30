using System.Text.RegularExpressions;
using MongoTui.Application.Services;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;

namespace MongoTui.Application.Queries;

/// <summary>Recognizes single-collection reads without evaluating arbitrary JavaScript.</summary>
public static class DocumentQuerySource
{
    private const string Quoted = "(?:\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*')";
    private const string Identifier = @"[$\p{L}_][$\p{L}\p{N}_]*";
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Verifies document identities against the submitted query snapshot, including empty reads.</summary>
    public static CollectionRef? Resolve(QueryExecutionContext context, QueryResult result) =>
        result.Documents.All(document => document.Contains(MongoSystemNames.IdField))
            ? Resolve(context.Script, context.Database) : null;

    public static CollectionRef? Resolve(string script, string? database)
    {
        try
        {
            return ResolveCore(script, database);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException
            or InvalidOperationException or DomainException or RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static CollectionRef? ResolveCore(string script, string? database)
    {
        var prefix = Match(script, $@"\A\s*db\s*(?:\.\s*getSiblingDB\s*\(\s*(?<db>{Quoted})\s*\)\s*)?
            \.\s*(?:getCollection\s*\(\s*(?<collection>{Quoted})\s*\)|(?<name>{Identifier}))");
        if (!prefix.Success) return null;
        var selectedDatabase = prefix.Groups["db"].Success ? Decode(prefix.Groups["db"].Value) : database;
        var collection = prefix.Groups["collection"].Success
            ? Decode(prefix.Groups["collection"].Value) : prefix.Groups["name"].Value;
        if (selectedDatabase is null || collection is null) return null;

        var query = Match(script[prefix.Length..], @"\A\s*\.\s*(?<method>findOne|find)\s*\(
            \s*(?<filter>.*?)\s*\)\s*(?<tail>(?:\.\s*(?:limit|skip)\s*\(\s*\d+\s*\)\s*
            |\.\s*sort\s*\(\s*(?<sort>\{.*?\})\s*\)\s*)*)
            (?<array>\.\s*toArray\s*\(\s*\)\s*)?;?\s*\z");
        if (!query.Success || !IsDocument(query.Groups["filter"].Value)) return null;
        if (query.Groups["sort"].Captures.Any(capture => !IsDocument(capture.Value))) return null;
        if (query.Groups["method"].Value == "findOne"
            && (query.Groups["tail"].Length > 0 || query.Groups["array"].Success)) return null;
        return new CollectionRef(DatabaseName.Create(selectedDatabase), CollectionName.Create(collection));
    }

    private static Match Match(string text, string pattern) => Regex.Match(text, pattern,
        RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, MatchTimeout);

    private static bool IsDocument(string text) => text.Length == 0 || BsonTextCodec.ParseDocument(text) is not null;

    private static string? Decode(string quoted) => JavaScriptStringLiteral.Decode(quoted[1..^1]);
}
