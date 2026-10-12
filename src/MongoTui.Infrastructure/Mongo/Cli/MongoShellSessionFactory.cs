using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>Starts <c>mongosh</c> processes and wraps them as sessions.</summary>
public sealed class MongoShellSessionFactory : IMongoShellSessionFactory
{
    private readonly IProcessRunner _runner;
    private readonly MongoToolsLocator _tools;

    public MongoShellSessionFactory(IProcessRunner runner, MongoToolsLocator tools)
    {
        _runner = runner;
        _tools = tools;
    }

    public async Task<IMongoShellSession> CreateAsync(MongoConnectionTarget target, CancellationToken cancellationToken)
    {
        var request = new ProcessRequest(
            _tools.Resolve("mongosh"),
            [target.ConnectionString, "--quiet"]);

        var process = await _runner.StartAsync(request, cancellationToken);

        return new MongoShellSession(process);
    }
}
