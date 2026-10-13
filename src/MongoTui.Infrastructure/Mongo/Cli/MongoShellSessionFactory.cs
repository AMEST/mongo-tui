using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Models;

namespace MongoTui.Infrastructure.Mongo.Cli;

/// <summary>Creates <c>mongosh</c> sessions, one per tab.</summary>
public sealed class MongoShellSessionFactory : IMongoShellSessionFactory
{
    private readonly IProcessRunner _runner;
    private readonly MongoToolsLocator _tools;

    public MongoShellSessionFactory(IProcessRunner runner, MongoToolsLocator tools)
    {
        _runner = runner;
        _tools = tools;
    }

    public Task<IMongoShellSession> CreateAsync(MongoConnectionTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IMongoShellSession>(new MongoShellSession(_runner, _tools, target));
    }
}
