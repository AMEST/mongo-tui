using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>Transfer channel backed by the MongoDB Database Tools (mongoexport/import, dump/restore).</summary>
public sealed class CliMongoGateway : ICliMongoTransferGateway
{
    private readonly IProcessRunner _runner;
    private readonly MongoToolsLocator _tools;

    public CliMongoGateway(IProcessRunner runner, MongoToolsLocator tools)
    {
        _runner = runner;
        _tools = tools;
    }

    public Task ExportAsync(MongoConnectionTarget target, ExportRequest request, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            $"--uri={target.ConnectionString}",
            $"--db={request.Collection.Database.Value}",
            $"--collection={request.Collection.Collection.Value}",
            $"--out={request.OutputPath}",
        };

        switch (request.Format)
        {
            case ExportFormat.Json:
                arguments.Add("--type=json");
                break;
            case ExportFormat.CanonicalJson:
                arguments.Add("--type=json");
                arguments.Add("--jsonFormat=canonical");
                break;
            case ExportFormat.Csv:
                arguments.Add("--type=csv");
                break;
            default:
                throw new NotSupportedException(
                    $"Export format '{request.Format}' is not served by the CLI channel.");
        }

        if (!string.IsNullOrWhiteSpace(request.FilterJson))
        {
            arguments.Add($"--query={request.FilterJson}");
        }

        if (request.Fields is { Count: > 0 })
        {
            arguments.Add($"--fields={string.Join(',', request.Fields)}");
        }

        if (request.Limit is > 0)
        {
            arguments.Add($"--limit={request.Limit}");
        }

        return RunToolAsync("mongoexport", arguments, cancellationToken);
    }

    public Task ImportAsync(MongoConnectionTarget target, ImportRequest request, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            $"--uri={target.ConnectionString}",
            $"--db={request.Collection.Database.Value}",
            $"--collection={request.Collection.Collection.Value}",
            $"--file={request.InputPath}",
        };

        switch (request.Format)
        {
            case ImportFormat.Json:
                arguments.Add("--type=json");
                break;
            case ImportFormat.Csv:
                arguments.Add("--type=csv");
                break;
            default:
                throw new NotSupportedException(
                    $"Import format '{request.Format}' is not served by the CLI channel.");
        }

        switch (request.Mode)
        {
            case ImportMode.Upsert:
                arguments.Add("--mode=upsert");
                break;
            case ImportMode.Merge:
                arguments.Add("--mode=merge");
                break;
        }

        if (request.DropCollection || request.Mode == ImportMode.Drop)
        {
            arguments.Add("--drop");
        }

        return RunToolAsync("mongoimport", arguments, cancellationToken);
    }

    public Task DumpDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            $"--uri={target.ConnectionString}",
            $"--db={database.Value}",
            $"--out={outputDirectory}",
        };

        return RunToolAsync("mongodump", arguments, cancellationToken);
    }

    public Task RestoreDatabaseAsync(
        MongoConnectionTarget target,
        DatabaseName database,
        string inputDirectory,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            $"--uri={target.ConnectionString}",
            $"--db={database.Value}",
            $"--dir={inputDirectory}",
        };

        return RunToolAsync("mongorestore", arguments, cancellationToken);
    }

    private async Task RunToolAsync(string tool, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var request = new ProcessRequest(_tools.Resolve(tool), arguments);
        var result = await _runner.RunAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            throw new QueryExecutionException(
                $"{tool} failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }
}
