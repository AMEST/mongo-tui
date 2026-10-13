using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.QueryAssistant;
using MongoTui.Infrastructure.Ai.OpenAi;
using MongoTui.Infrastructure.FileSystem;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.Infrastructure.Options;
using MongoTui.Infrastructure.Persistence.Sqlite;
using MongoTui.Infrastructure.Process;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.DependencyInjection;

/// <summary>Registers infrastructure implementations of application abstractions.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ =>
        {
            var database = new SqliteDatabase(AppPaths.DatabaseFile);
            database.Migrate();
            return database;
        });

        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<IConnectionRepository, SqliteConnectionRepository>();
        services.AddSingleton<IQueryHistoryRepository, SqliteQueryHistoryRepository>();
        services.AddSingleton<ICryptoService, AesGcmCryptoService>();
        services.AddSingleton(Argon2Parameters.Default);
        services.AddSingleton<IKeyring, UnavailableKeyring>();
        services.AddSingleton<KeyringKeyProvider>();
        services.AddSingleton<MasterPasswordKeyProvider>();
        services.AddSingleton<IKeyProvider, FallbackKeyProvider>();

        services.AddSingleton<MongoToolsOptions>();
        services.AddSingleton<MongoToolsLocator>();
        services.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        services.AddSingleton<IMongoShellSessionFactory, MongoShellSessionFactory>();

        services.AddSingleton<IDriverMongoGateway, DriverMongoGateway>();
        services.AddSingleton<ICliMongoTransferGateway, CliMongoGateway>();
        services.AddSingleton<CompositeMongoGateway>();
        services.AddSingleton<IMongoGateway>(provider => provider.GetRequiredService<CompositeMongoGateway>());
        services.AddSingleton<IMongoTransferGateway>(provider => provider.GetRequiredService<CompositeMongoGateway>());

        services.AddSingleton<PromptBuilder>();
        services.AddHttpClient<IAiAssistant, OpenAiCompatibleAssistant>();

        return services;
    }
}
