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
/// Evaluates scripts verbatim with <c>mongosh --eval</c>, using canonical Extended JSON
/// for document views or native shell output for the raw view.
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
        EvaluateAsync(script, database, false, cancellationToken);

    public Task<QueryResult> EvaluateRawAsync(string script, string? database, CancellationToken cancellationToken) =>
        EvaluateAsync(script, database, true, cancellationToken);

    private async Task<QueryResult> EvaluateAsync(string script, string? database, bool rawOutput, CancellationToken cancellationToken)
    {
        _logger.LogDebug("mongosh: evaluating script ({ScriptLength} chars)", script.Length);

        var result = await _runner.RunAsync(CreateRequest(script, database, rawOutput), cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "mongosh: exit={ExitCode}, stdout={StdoutChars} chars, stderr={StderrChars} chars",
            result.ExitCode,
            result.StandardOutput.Length,
            result.StandardError.Length);

        if (result.ExitCode != 0)
        {
            throw new QueryExecutionException(rawOutput ? DescribeRawError(result) : DescribeError(result));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            _logger.LogWarning("mongosh wrote {StderrChars} stderr characters", result.StandardError.Length);
        }

        return rawOutput
            ? new QueryResult { Kind = QueryResultKind.Raw, Documents = [], RawOutput = result.StandardOutput }
            : ParseResult(result.StandardOutput);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ProcessRequest CreateRequest(string script, string? database, bool rawOutput)
    {
        // Preserve the types still available to the shell. Editors reload through the driver.
        var arguments = new List<string> { _target.ConnectionString, "--quiet", "--norc" };
        if (!rawOutput) arguments.Add("--json=canonical");
        if (database is not null)
            arguments.AddRange(["--eval", $"db = db.getSiblingDB({JsonSerializer.Serialize(database)})"]);
        arguments.AddRange(["--eval", script]);
        return new ProcessRequest(_tools.Resolve("mongosh"), arguments);
    }

    private static string DescribeRawError(ProcessResult result) =>
        result.StandardOutput.Length + result.StandardError.Length == 0
            ? $"mongosh exited with code {result.ExitCode}."
            : result.StandardOutput + result.StandardError;

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

    private static QueryResult ParseResult(string raw)
    {
        var text = raw.Trim();

        return new()
        {
            Kind = QueryResultKind.Scalar,
            Documents = ParseDocuments(text),
            RawOutput = raw,
        };
    }

    private static IReadOnlyList<BsonDocument> ParseDocuments(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        if (text.StartsWith('['))
        {
            return BsonTextCodec.ParseValue(text).AsBsonArray
                .Select(value => value as BsonDocument ?? new BsonDocument(ScalarValueField, value))
                .ToList();
        }

        if (text.StartsWith('{'))
        {
            // Canonical Extended JSON also wraps scalars, e.g. {"$numberInt":"2"}.
            var value = BsonTextCodec.ParseValue(text);
            return [value as BsonDocument ?? new BsonDocument(ScalarOutputField, value.ToString() ?? string.Empty)];
        }

        return [new BsonDocument(ScalarOutputField, text)];
    }
}
