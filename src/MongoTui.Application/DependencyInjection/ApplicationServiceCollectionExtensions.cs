using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoTui.Application.Abstractions.Diagnostics;
using MongoTui.Application.Diagnostics;

namespace MongoTui.Application.DependencyInjection;

/// <summary>Registers application-layer services and abstractions.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DiagnosticsSettingsService>();
        services.AddSingleton<QueryMetricsQueue>();
        services.AddSingleton<QueryInsightsService>();
        services.AddSingleton<ExplainService>();
        services.AddSingleton<DiagnosticsService>();
        services.AddSingleton<DiagnosticsServices>();
        services.AddSingleton<IQueryMetricsStore, QueryMetricsStore>();
        services.AddSingleton<SlowQueryLogger>();
        services.AddSingleton<IQueryMetricsRecorder, QueryMetricsRecorder>();

        services.AddSingleton<SecretKeyService>();
        services.AddSingleton<ConnectionService>();
        services.AddSingleton<ConnectionTunnelService>();
        services.AddSingleton<AiSettingsService>();
        services.AddSingleton<SecurityModeDetector>();
        services.AddSingleton<SecurityModeService>();
        services.AddSingleton<QueryService>();
        services.AddSingleton<QueryFileService>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<IndexService>();
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<ImportService>();
        services.AddSingleton<AutocompleteService>();
        services.AddSingleton<CollectionSchemaService>();
        services.AddSingleton<TranslateQueryService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<PasswordGenerator>();
        services.AddSingleton<UserManagementService>();

        return services;
    }
}
