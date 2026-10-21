using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Services;

namespace MongoTui.Application.DependencyInjection;

/// <summary>Registers application-layer services and abstractions.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SecretKeyService>();
        services.AddSingleton<ConnectionService>();
        services.AddSingleton<ConnectionTunnelService>();
        services.AddSingleton<AiSettingsService>();
        services.AddSingleton<SecurityModeDetector>();
        services.AddSingleton<SecurityModeService>();
        services.AddSingleton<QueryService>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<IndexService>();
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<ImportService>();
        services.AddSingleton<AutocompleteService>();
        services.AddSingleton<CollectionSchemaService>();
        services.AddSingleton<TranslateQueryService>();
        services.AddSingleton<SettingsService>();

        return services;
    }
}
