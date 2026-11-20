using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Queries;

/// <summary>Parses a single read expression without executing JavaScript.</summary>
public static class ExplainQuerySource
{
    public static ExplainRequest? TryCreate(QueryExecutionContext context, ExplainVerbosity verbosity)
    {
        try { return Parse(context.Script, context.Database ?? ConnectionUriParser.Parse(context.Target.ConnectionString).Database
            ?? MongoSystemNames.DefaultDatabase, verbosity); }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return null; }
    }

    private static ExplainRequest Parse(string script, string? database, ExplainVerbosity verbosity)
    {
        var text = ExplainScriptText.WithoutComments(script).Trim().TrimEnd(';').Trim();
        if (!text.StartsWith("db.", StringComparison.Ordinal)) throw new FormatException();
        var offset = 3;
        var name = Identifier(text, ref offset);
        if (name == "getSiblingDB")
        {
            database = StringArgument(Arguments(text, ref offset));
            Dot(text, ref offset); name = Identifier(text, ref offset);
        }
        if (name == "getCollection") name = StringArgument(Arguments(text, ref offset));
        Dot(text, ref offset);
        var method = Identifier(text, ref offset);
        var args = BsonTextCodec.ParseValue("[" + Arguments(text, ref offset) + "]").AsBsonArray;
        var request = Initial(database!, name, method, args, verbosity);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (offset < text.Length)
        {
            Dot(text, ref offset); var modifier = Identifier(text, ref offset);
            if (!seen.Add(modifier)) throw new FormatException();
            var argument = Arguments(text, ref offset);
            if (modifier == "toArray" && (method is "find" or "aggregate") && string.IsNullOrWhiteSpace(argument) && offset == text.Length)
                return request;
            if (method != "find") throw new FormatException();
            request = modifier switch
            {
                "sort" => request with { Sort = BsonTextCodec.ParseDocument(argument) },
                "skip" => request with { Skip = Nonnegative(argument) },
                "limit" => request with { Limit = Nonnegative(argument) },
                "maxTimeMS" => request with { MaxTime = TimeSpan.FromMilliseconds(Nonnegative(argument)) },
                _ => throw new FormatException(),
            };
        }
        return request;
    }

    private static ExplainRequest Initial(string database, string collection, string method, BsonArray args, ExplainVerbosity verbosity)
    {
        var scope = new CollectionRef(DatabaseName.Create(database), CollectionName.Create(collection));
        if (method == "aggregate")
        {
            if (args.Count != 1 || !args[0].IsBsonArray) throw new FormatException();
            var pipeline = args[0].AsBsonArray.Select(stage => stage.AsBsonDocument).ToArray();
            if (pipeline.Any(stage => stage.ElementCount != 1 || stage.Contains("$out") || stage.Contains("$merge")))
                throw new FormatException();
            return new ExplainRequest { Collection = scope, Operation = ExplainOperation.Aggregate, Pipeline = pipeline, Verbosity = verbosity };
        }
        if (method is not ("find" or "findOne") || args.Count > 2) throw new FormatException();
        return new ExplainRequest { Collection = scope, Operation = method == "find" ? ExplainOperation.Find : ExplainOperation.FindOne,
            Filter = args.Count > 0 ? args[0].AsBsonDocument : new(), Projection = args.Count > 1 ? args[1].AsBsonDocument : null,
            Limit = method == "findOne" ? 1 : null, Verbosity = verbosity };
    }

    private static string Identifier(string text, ref int offset)
    {
        Skip(text, ref offset);
        var match = Regex.Match(text[offset..], @"\A[$\p{L}_][$\p{L}\p{N}_]*", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success) throw new FormatException();
        offset += match.Length; Skip(text, ref offset); return match.Value;
    }
    private static void Skip(string text, ref int offset) { while (offset < text.Length && char.IsWhiteSpace(text[offset])) offset++; }
    private static void Dot(string text, ref int offset)
    {
        Skip(text, ref offset);
        if (offset >= text.Length || text[offset++] != '.') throw new FormatException();
    }
    private static int Nonnegative(string value) => int.TryParse(value.Trim(), out var number) && number >= 0 ? number : throw new FormatException();
    private static string StringArgument(string value) => BsonTextCodec.ParseValue(value).AsString;

    private static string Arguments(string text, ref int offset)
    {
        Skip(text, ref offset);
        if (offset >= text.Length || text[offset++] != '(') throw new FormatException();
        var start = offset; var depth = 1; char quote = '\0'; var escaped = false;
        while (offset < text.Length)
        {
            var character = text[offset++];
            if (quote != '\0')
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == quote) quote = '\0';
                continue;
            }
            if (character is '\'' or '"') quote = character;
            else if (character == '(') depth++;
            else if (character == ')' && --depth == 0)
            {
                var value = text[start..(offset - 1)]; Skip(text, ref offset); return value;
            }
        }
        throw new FormatException();
    }
}
