using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoTui.Application.Services;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>
/// Evaluates scripts verbatim with <c>mongosh --eval</c> and streams both output pipes.
/// </summary>
public sealed class MongoShellSession : IMongoShellSession
{
    private const string ScalarValueField = "value";
    private const string ScalarOutputField = "output";
    private readonly IProcessRunner _runner;
    private readonly MongoToolsLocator _tools;
    private readonly MongoConnectionTarget _target;
    private readonly ILogger<MongoShellSession> _logger;

    public MongoShellSession(IProcessRunner runner, MongoToolsLocator tools, MongoConnectionTarget target, ILogger<MongoShellSession> logger)
    {
        _runner = runner;
        _tools = tools;
        _target = target;
        _logger = logger;
    }

    public bool IsRunning => true;

    public Task<QueryResult> EvaluateAsync(string script, string? database, CancellationToken cancellationToken) =>
        EvaluateAsync(script, database, null, cancellationToken);

    public async Task<QueryResult> EvaluateAsync(string script, string? database, ScriptOutput? output, CancellationToken cancellationToken)
    {
        _logger.LogDebug("mongosh: evaluating script ({ScriptLength} chars)", script.Length);

        var result = await _runner.RunAsync(CreateRequest(script, database, output), cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
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
            _logger.LogWarning("mongosh wrote {StderrChars} stderr characters", result.StandardError.Length);
        }

        return new QueryResult { Kind = QueryResultKind.Scalar, Documents = ParseDocuments(result.StandardOutput.Trim()) };
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ProcessRequest CreateRequest(string script, string? database, ScriptOutput? output)
    {
        // Preserve the types still available to the shell. Editors reload through the driver.
        var arguments = new List<string> { _target.ConnectionString, "--quiet", "--norc", "--json=canonical" };
        if (database is not null)
            arguments.AddRange(["--eval", $"db = db.getSiblingDB({JsonSerializer.Serialize(database)})"]);
        arguments.AddRange(["--eval", script]);
        return new ProcessRequest(_tools.Resolve("mongosh"), arguments) { OnOutput = output is null ? null : output.AppendAsync };
    }

    private static string DescribeError(ProcessResult result)
    {
        if (ExtractJsonMessage(result.StandardOutput) is { } message)
        {
            return message;
        }

        var error = result.StandardError.Trim();

        if (error.Length > 0)
        {
            return $"mongosh failed:{Environment.NewLine}{error}";
        }

        return $"mongosh exited with code {result.ExitCode}.";
    }

    private static string? ExtractJsonMessage(string output)
    {
        var text = output.AsSpan().TrimEnd();
        var start = FindTrailingObject(text);
        if (start < 0) return null;

        try
        {
            using var document = JsonDocument.Parse(output.AsMemory(start, text.Length - start));
            var root = document.RootElement;
            if (!text[..start].Trim().IsEmpty && !(root.TryGetProperty("name", out _) && root.TryGetProperty("stack", out _)))
                return null;

            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return string.IsNullOrWhiteSpace(message.GetString()) ? null : message.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    // In canonical mode mongosh appends the error object to stdout, even after a log with no newline.
    private static int FindTrailingObject(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[^1] != '}') return -1;
        var depth = 0;
        var quoted = false;
        for (var i = text.Length - 1; i >= 0; i--)
        {
            var character = text[i];
            if (character == '"' && !IsEscaped(text, i)) quoted = !quoted;
            if (quoted) continue;
            if (character == '}') depth++;
            if (character == '{' && --depth == 0) return i;
        }
        return -1;
    }

    private static bool IsEscaped(ReadOnlySpan<char> text, int index)
    {
        var start = index;
        while (start > 0 && text[start - 1] == '\\') start--;
        return (index - start) % 2 != 0;
    }

    private static IReadOnlyList<BsonDocument> ParseDocuments(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        try
        {
            if (text.StartsWith('['))
                return BsonTextCodec.ParseValue(text).AsBsonArray
                    .Select(value => value as BsonDocument ?? new BsonDocument(ScalarValueField, value))
                    .ToList();

            if (text.StartsWith('{'))
            {
                // Canonical Extended JSON also wraps scalars, e.g. {"$numberInt":"2"}.
                var value = BsonTextCodec.ParseValue(text);
                return [value as BsonDocument ?? new BsonDocument(ScalarOutputField, value.ToString() ?? string.Empty)];
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException
            or OverflowException or BsonSerializationException)
        {
            // Log lines can look like JSON without being a structured return value.
        }

        return [new BsonDocument(ScalarOutputField, text)];
    }
}
