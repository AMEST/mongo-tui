using MongoDB.Driver;
using MongoTui.Application.Abstractions.Process;
using Testcontainers.MongoDb;

namespace MongoTui.IntegrationTests;

/// <summary>Runs the production shell command inside the test MongoDB container.</summary>
internal sealed class MongoContainerProcessRunner(MongoDbContainer container) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var connection = new MongoUrlBuilder(request.Arguments[0])
        {
            Server = new MongoServerAddress("127.0.0.1", 27017),
        };
        var result = await container.ExecAsync(
            [Path.GetFileName(request.FileName), connection.ToString(), .. request.Arguments.Skip(1)], cancellationToken);
        return new ProcessResult(checked((int)(result.ExitCode ?? -1)), result.Stdout, result.Stderr);
    }
}
