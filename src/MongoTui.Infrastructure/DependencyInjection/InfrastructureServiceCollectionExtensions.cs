using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoTui.Infrastructure.Diagnostics;
using MongoTui.Application.Abstractions.Ai;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Process;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.QueryAssistant;
using MongoTui.Infrastructure.Ai.OpenAi;
using MongoTui.Infrastructure.FileSystem;
using MongoTui.Infrastructure.Mongo.Cli;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.Infrastructure.Options;
using MongoTui.Infrastructure.Persistence.Sqlite;
using MongoTui.Infrastructure.Process;
using MongoTui.Infrastructure.Security;
using MongoTui.Infrastructure.Ssh;

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
        services.AddSingleton<ILoggingSettings, SerilogLoggingSettings>();
        services.AddSingleton<IHostedService, LoggingSettingsMonitor>();
        services.AddSingleton<IStorageTransaction>(provider => provider.GetRequiredService<SqliteDatabase>());
        services.AddSingleton<IConnectionRepository, SqliteConnectionRepository>();
        services.AddSingleton<IQueryHistoryRepository, SqliteQueryHistoryRepository>();
        services.AddSingleton<IApplicationLogReader>(_ => new FileApplicationLogReader(AppPaths.LogsDirectory));
        services.AddSingleton<ICryptoService, AesGcmCryptoService>();
        services.AddSingleton(Argon2Parameters.Default);
        services.AddSingleton<PasswordKekSource>();
        services.AddSingleton<IKekSource>(provider => provider.GetRequiredService<PasswordKekSource>());
        services.AddSingleton<FileKekFileStore>();
        services.AddSingleton<IKekFileStore>(provider => provider.GetRequiredService<FileKekFileStore>());
        services.AddSingleton<FileKekSource>();
        services.AddSingleton<IKekSource>(provider => provider.GetRequiredService<FileKekSource>());

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IKeyring>(_ => CreateKeyring());
            services.AddSingleton<KeyringKekSource>();
            services.AddSingleton<IKekSource>(provider => provider.GetRequiredService<KeyringKekSource>());
        }
        else
        {
            services.AddSingleton<IKeyring, UnavailableKeyring>();
        }

        services.AddSingleton<MongoToolsOptions>();
        services.AddSingleton<MongoToolsLocator>();
        services.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        services.AddSingleton<SshAskPassFactory>();
        services.AddSingleton<ISshTunnelFactory, SshCliTunnelFactory>();
        services.AddSingleton<IMongoShellSessionFactory, MongoShellSessionFactory>();

        services.AddSingleton<MongoDatabaseDiscovery>();
        services.AddSingleton<IDriverMongoGateway, DriverMongoGateway>();
        services.AddSingleton<ICliMongoTransferGateway, CliMongoGateway>();
        services.AddSingleton<CompositeMongoGateway>();
        services.AddSingleton<IMongoGateway>(provider => provider.GetRequiredService<CompositeMongoGateway>());
        services.AddSingleton<IMongoTransferGateway>(provider => provider.GetRequiredService<CompositeMongoGateway>());
        services.AddSingleton<IMongoUserGateway>(provider => provider.GetRequiredService<CompositeMongoGateway>());

        services.AddSingleton<PromptBuilder>();
        services.AddHttpClient<IAiAssistant, OpenAiCompatibleAssistant>();

        return services;
    }

    private static IKeyring CreateKeyring()
    {
        try
        {
            return LatchkeyKeyring.Create();
        }
        catch (Exception)
        {
            return new UnavailableKeyring();
        }
    }
}
