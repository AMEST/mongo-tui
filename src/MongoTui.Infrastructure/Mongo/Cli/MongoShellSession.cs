using System.Collections.Concurrent;
using Serilog;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Queries;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>
/// A persistent <c>mongosh</c> session that evaluates scripts over stdin/stdout.
/// Each command is terminated by a unique marker line, which delimits its output.
/// </summary>
public sealed class MongoShellSession : IMongoShellSession
{
    private static readonly TimeSpan StandardErrorSettleDelay = TimeSpan.FromMilliseconds(250);

    private readonly IProcessSession _process;
    private readonly ConcurrentQueue<string> _standardError = new();
    private int _commandCounter;
    private Task? _standardErrorPump;

    public MongoShellSession(IProcessSession process) => _process = process;

    public bool IsRunning => !_process.HasExited;

    public async Task<QueryResult> EvaluateAsync(string script, CancellationToken cancellationToken)
    {
        StartStandardErrorPump();
        DrainStandardError();

        var marker = $"__MONGOTUI_END_{Interlocked.Increment(ref _commandCounter)}__";
        Log.Debug("mongosh: sending script ({ScriptLength} chars)", script.Length);

        await _process.StandardInput.WriteLineAsync(script.AsMemory(), cancellationToken);
        await _process.StandardInput.WriteLineAsync($"print(\"{marker}\")".AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);

        var lines = new List<string>();
        while (true)
        {
            var line = await _process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Contains(marker, StringComparison.Ordinal))
            {
                break;
            }

            lines.Add(line);
        }

        Log.Debug(
            "mongosh: received {LineCount} stdout lines ({CharCount} chars, processExited={Exited})",
            lines.Count,
            lines.Sum(line => line.Length),
            _process.HasExited);

        if (lines.Count == 0 && await FailureAsync(cancellationToken) is { } failure)
        {
            throw failure;
        }

        return ParseResult(lines);
    }

    private async Task<QueryExecutionException?> FailureAsync(CancellationToken cancellationToken)
    {
        // Give the stderr pump a moment to surface the reason (it is a separate pipe).
        if (_standardErrorPump is not null)
        {
            await Task.WhenAny(_standardErrorPump, Task.Delay(StandardErrorSettleDelay, cancellationToken));
        }

        var error = string.Join(Environment.NewLine, _standardError).Trim();

        if (error.Length > 0)
        {
            Log.Warning("mongosh returned no stdout: {Error}", error);

            return new QueryExecutionException($"mongosh failed:{Environment.NewLine}{error}");
        }

        if (_process.HasExited)
        {
            Log.Warning("mongosh exited before producing any output");

            return new QueryExecutionException("mongosh exited before producing any output.");
        }

        Log.Debug("mongosh produced no output for a silent script");
        return null;
    }

    private void DrainStandardError()
    {
        while (_standardError.TryDequeue(out _))
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    // mongosh reports query errors on stderr and keeps running, so we must drain and log
    // it; otherwise failures are invisible to the caller.
    private void StartStandardErrorPump()
    {
        if (_standardErrorPump is not null || _process.StandardError is not { } error)
        {
            return;
        }

        _standardErrorPump = Task.Run(async () =>
        {
            try
            {
                while (await error.ReadLineAsync() is { } line)
                {
                    _standardError.Enqueue(line);
                    Log.Warning("mongosh stderr: {Line}", line);
                }
            }
            catch (Exception exception)
            {
                Log.Debug(exception, "mongosh stderr pump stopped");
            }
        });
    }

    private static QueryResult ParseResult(IReadOnlyList<string> lines) =>
        new()
        {
            Kind = QueryResultKind.Scalar,
            Documents = ParseDocuments(string.Join('\n', lines).Trim()),
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
