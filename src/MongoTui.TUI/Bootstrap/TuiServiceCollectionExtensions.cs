using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Services;
using MongoTui.TUI.Views;

namespace MongoTui.TUI.Bootstrap;

/// <summary>Registers TUI views and the application host.</summary>
public static class TuiServiceCollectionExtensions
{
    public static IServiceCollection AddTui(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ILocalizer, ResxLocalizer>();
        services.AddSingleton<SshAuthenticationPrompt>();
        services.AddSingleton<ITwoFactorPrompt>(provider => provider.GetRequiredService<SshAuthenticationPrompt>());
        services.AddSingleton<AppHost>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
