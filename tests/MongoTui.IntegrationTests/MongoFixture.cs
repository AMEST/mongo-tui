using Testcontainers.MongoDb;

namespace MongoTui.IntegrationTests;

/// <summary>Starts a single MongoDB container shared by the integration tests.</summary>
public sealed class MongoFixture : IAsyncLifetime
{
    public MongoDbContainer Container { get; private set; } = null!;

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        Container = new MongoDbBuilder("mongo:7").Build();
        await Container.StartAsync();
    }

    public async Task DisposeAsync() => await Container.DisposeAsync();
}
