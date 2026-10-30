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
/// Evaluates scripts with <c>mongosh</c> in one-shot <c>--eval</c> mode, asking it to render the
/// result as canonical Extended JSON. The script is passed verbatim so shell
/// semantics (e.g. cursor methods like <c>limit</c>) are preserved; unlike the interactive REPL,
/// this mode does not pollute stdout with prompts or shell-formatted values.
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

    public async Task<QueryResult> EvaluateAsync(string script, string? database, CancellationToken cancellationToken)
    {
        _logger.LogDebug("mongosh: evaluating script ({ScriptLength} chars)", script.Length);

        var result = await _runner.RunAsync(CreateRequest(script, database), cancellationToken).ConfigureAwait(false);

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

        return ParseResult(result.StandardOutput);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ProcessRequest CreateRequest(string script, string? database)
    {
        // Preserve the types still available to the shell. Editors reload through the driver.
        var arguments = new List<string> { _target.ConnectionString, "--quiet", "--norc", "--json=canonical" };
        if (database is not null)
            arguments.AddRange(["--eval", $"db = db.getSiblingDB({JsonSerializer.Serialize(database)})"]);
        arguments.AddRange(["--eval", script]);
        return new ProcessRequest(_tools.Resolve("mongosh"), arguments);
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
            RawOutput = text,
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
