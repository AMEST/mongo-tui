using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Services;
using MongoTui.Web.Components;
using MongoTui.Web.Localization;
using MongoTui.Web.Services;

namespace MongoTui.Web.Bootstrap;

/// <summary>Owns the optional Kestrel host without starting Terminal.Gui.</summary>
public sealed class WebHost
{
    private const int DisconnectedCircuitRetentionSeconds = 30;
    private const int MaximumRetainedCircuits = 5;
    private const long MaximumHubMessageBytes = 2 * 1024 * 1024;
    public WebApplication Build(WebOptions options, Action<IServiceCollection> configure)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [], ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseKestrel(server => server.Listen(IPAddress.Loopback, options.Port));
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();
        configure(builder.Services);
        AddWebServices(builder.Services);
        var app = builder.Build();
        app.UseMiddleware<LocalRequestGuard>();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }

    public async Task<int> RunAsync(WebOptions options, Action<IServiceCollection> configure)
    {
        await using var app = Build(options, configure);
        var logger = app.Services.GetRequiredService<ILogger<WebHost>>();
        await app.StartAsync();
        logger.LogInformation("Web interface started on {Address}", options.Url);
        await Console.Out.WriteLineAsync($"MongoTui · {options.Url}");
        if (options.OpenBrowser)
        {
            try { Process.Start(new ProcessStartInfo(options.Url) { UseShellExecute = true }); }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            { logger.LogWarning("Could not open the browser ({ExceptionType})", exception.GetType().Name); }
        }
        await app.WaitForShutdownAsync();
        logger.LogInformation("Web interface stopped");
        return 0;
    }

    private static void AddWebServices(IServiceCollection services)
    {
        services.AddRazorComponents().AddInteractiveServerComponents(options =>
        {
            options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(DisconnectedCircuitRetentionSeconds);
            options.DisconnectedCircuitMaxRetained = MaximumRetainedCircuits;
        }).AddHubOptions(options => options.MaximumReceiveMessageSize = MaximumHubMessageBytes);
        services.AddScoped<WebText>();
        services.AddScoped<WebSshPrompt>();
        services.AddScoped<ITwoFactorPrompt>(provider => provider.GetRequiredService<WebSshPrompt>());
        services.Replace(ServiceDescriptor.Scoped<ConnectionTunnelService, ConnectionTunnelService>());
        services.Replace(ServiceDescriptor.Scoped<ConnectionService, ConnectionService>());
        services.Replace(ServiceDescriptor.Scoped<SecurityModeService, SecurityModeService>());
        services.Replace(ServiceDescriptor.Scoped<QueryService, QueryService>());
        services.AddScoped<ConnectionCatalog>();
        services.AddScoped<QueryWorkspace>();
        services.AddScoped<ConnectionEditor>();
        services.AddScoped<WebUserManagement>();
        services.AddSingleton<WebQueryLimiter>();
        services.AddScoped<WebTransferService>();
    }
}
