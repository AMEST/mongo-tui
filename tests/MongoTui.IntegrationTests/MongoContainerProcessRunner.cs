using MongoDB.Driver;
using MongoTui.Application.Abstractions.Process;
using Testcontainers.MongoDb;

namespace MongoTui.IntegrationTests;

/// <summary>Runs production MongoDB tool commands inside the test MongoDB container.</summary>
internal sealed class MongoContainerProcessRunner(MongoDbContainer container) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var prefix = request.Arguments[0].StartsWith("--uri=", StringComparison.Ordinal) ? "--uri=" : string.Empty;
        var connection = new MongoUrlBuilder(request.Arguments[0][prefix.Length..])
        {
            Server = new MongoServerAddress("127.0.0.1", 27017),
        };
        var result = await container.ExecAsync(
            [Path.GetFileName(request.FileName), prefix + connection, .. request.Arguments.Skip(1)], cancellationToken).ConfigureAwait(false);
        return new ProcessResult(checked((int)(result.ExitCode ?? -1)), result.Stdout, result.Stderr);
    }
}
