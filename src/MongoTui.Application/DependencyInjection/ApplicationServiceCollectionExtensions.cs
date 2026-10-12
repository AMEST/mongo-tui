using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Services;

namespace MongoTui.Application.DependencyInjection;

/// <summary>Registers application-layer services and abstractions.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ConnectionService>();
        services.AddSingleton<QueryService>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<IndexService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<ImportService>();
        services.AddSingleton<AutocompleteService>();
        services.AddSingleton<TranslateQueryService>();

        return services;
    }
}
