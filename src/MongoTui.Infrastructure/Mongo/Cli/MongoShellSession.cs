using System.Text.Json;
using Serilog;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>
/// Evaluates scripts with <c>mongosh</c> in one-shot <c>--eval</c> mode, asking it to render the
/// result as Extended JSON (<c>--json=relaxed</c>). The script is passed verbatim so shell
/// semantics (e.g. cursor methods like <c>limit</c>) are preserved; unlike the interactive REPL,
/// this mode does not pollute stdout with prompts or shell-formatted values.
/// </summary>
public sealed class MongoShellSession : IMongoShellSession
{
    private readonly IProcessRunner _runner;
    private readonly MongoToolsLocator _tools;
    private readonly MongoConnectionTarget _target;

    public MongoShellSession(IProcessRunner runner, MongoToolsLocator tools, MongoConnectionTarget target)
    {
        _runner = runner;
        _tools = tools;
        _target = target;
    }

    public bool IsRunning => true;

    public async Task<QueryResult> EvaluateAsync(string script, string? database, CancellationToken cancellationToken)
    {
        Log.Debug("mongosh: evaluating script ({ScriptLength} chars)", script.Length);

        var result = await _runner.RunAsync(CreateRequest(script, database), cancellationToken);

        Log.Debug(
            "mongosh: exit={ExitCode}, stdout={StdoutChars} chars, stderr={StderrChars} chars",
            result.ExitCode,
            result.StandardOutput.Length,
            result.StandardError.Length);

        if (result.ExitCode != 0)
        {
            throw new QueryExecutionException(DescribeError(result));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            Log.Warning("mongosh stderr: {Error}", result.StandardError.Trim());
        }

        return ParseResult(result.StandardOutput.Trim());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ProcessRequest CreateRequest(string script, string? database)
    {
        var arguments = new List<string> { _target.ConnectionString, "--quiet", "--norc", "--json=relaxed" };
        if (database is not null)
            arguments.AddRange(["--eval", $"db = db.getSiblingDB({JsonSerializer.Serialize(database)})"]);
        arguments.AddRange(["--eval", script]);
        return new ProcessRequest(_tools.Resolve("mongosh"), arguments);
    }

    private static string DescribeError(ProcessResult result)
    {
        if (ExtractJsonMessage(result.StandardOutput) is { } message)
        {
            Log.Warning("mongosh query error: {Message}", message);

            return message;
        }

        var error = result.StandardError.Trim();

        if (error.Length > 0)
        {
            Log.Warning("mongosh failed: {Error}", error);

            return $"mongosh failed:{Environment.NewLine}{error}";
        }

        return $"mongosh exited with code {result.ExitCode}.";
    }

    private static string? ExtractJsonMessage(string output)
    {
        var trimmed = output.Trim();

        if (!trimmed.StartsWith('{'))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);

            if (document.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static QueryResult ParseResult(string text) =>
        new()
        {
            Kind = QueryResultKind.Scalar,
            Documents = ParseDocuments(text),
        };

    private static IReadOnlyList<BsonDocument> ParseDocuments(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        if (text.StartsWith('['))
        {
            return BsonSerializer.Deserialize<BsonArray>(text)
                .Select(value => value.AsBsonDocument)
                .ToList();
        }

        if (text.StartsWith('{'))
        {
            return [BsonDocument.Parse(text)];
        }

        return [new BsonDocument("output", text)];
    }
}
