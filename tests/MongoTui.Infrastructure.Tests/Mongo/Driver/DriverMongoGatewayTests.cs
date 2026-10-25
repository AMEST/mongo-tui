using Microsoft.Extensions.Logging.Abstractions;
using AwesomeAssertions;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoTui.Application.Models;
using MongoTui.Infrastructure.Mongo.Driver;

namespace MongoTui.Infrastructure.Tests.Mongo.Driver;

public sealed class DriverMongoGatewayTests : IDisposable
{
    private readonly MongoConnectionTarget _target = new(
        $"mongodb://127.0.0.1:1/?appName=cleanup-{Guid.NewGuid():N}&serverSelectionTimeoutMS=50");
    private readonly DriverMongoGateway _gateway = new(new MongoDatabaseDiscovery(NullLogger<MongoDatabaseDiscovery>.Instance));
    private readonly MongoClient _observer;
    private readonly ICluster _cluster;

    public DriverMongoGatewayTests()
    {
        _observer = new MongoClient(_target.ConnectionString);
        _cluster = _observer.Cluster;
    }

    public void Dispose()
    {
        _gateway.ReleaseClient(_target);
        _gateway.Dispose();
        ClusterRegistry.Instance.UnregisterAndDisposeCluster(_cluster);
        _observer.Dispose();
    }

    [Fact]
    public async Task Should_CloseCluster_WhenTargetReleased()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Record.ExceptionAsync(() => _gateway.ListDatabasesAsync(_target, cancellation.Token));

        _gateway.ReleaseClient(_target);

        var listDatabases = () => _observer.ListDatabasesAsync(CancellationToken.None);
        await listDatabases.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Should_CreateNewClient_WhenReleasedTargetIsUsedAgain()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Record.ExceptionAsync(() => _gateway.ListDatabasesAsync(_target, cancellation.Token));
        _gateway.ReleaseClient(_target);

        var failure = await Record.ExceptionAsync(() => _gateway.ListDatabasesAsync(_target, cancellation.Token));

        failure.Should().BeAssignableTo<OperationCanceledException>();
    }
}
