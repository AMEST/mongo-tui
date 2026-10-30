using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.RateLimiting;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Services;
using MongoTui.Application.Localization;
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
        builder.Logging.ClearProviders();
        configure(builder.Services);
        AddWebServices(builder.Services);
        var app = builder.Build();
        app.UseMiddleware<LocalRequestGuard>();
        app.UseWebSockets();
        app.UseMiddleware<WebSessionAuthenticationMiddleware>();
        app.UseRateLimiter();
        app.UseAntiforgery();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new ManifestEmbeddedFileProvider(typeof(WebHost).Assembly, "wwwroot"),
            RequestPath = "/_content/MongoTui.Web",
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new ManifestEmbeddedFileProvider(typeof(WebHost).Assembly, "framework"),
            RequestPath = "/_framework",
        });
        WebAuthenticationEndpoints.Map(app);
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        return app;
    }

    public async Task<int> RunAsync(WebOptions options, Action<IServiceCollection> configure)
    {
        await using var app = Build(options, configure);
        var logger = app.Services.GetRequiredService<ILogger<WebHost>>();
        await app.Services.GetRequiredService<ISettingsStore>().DeleteAsync("web.basicAuth", CancellationToken.None);
        await app.StartAsync();
        logger.LogInformation("Web interface started on {Address}", options.Url);
        var launchUrl = await PrintLaunchAsync(app, options);
        if (options.OpenBrowser)
        {
            try { Process.Start(new ProcessStartInfo(launchUrl) { UseShellExecute = true }); }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            { logger.LogWarning("Could not open the browser ({ExceptionType})", exception.GetType().Name); }
        }
        await app.WaitForShutdownAsync();
        logger.LogInformation("Web interface stopped");
        return 0;
    }

    // Launch capabilities are printed only to the owner's terminal, never to ILogger.
    private static async Task<string> PrintLaunchAsync(WebApplication app, WebOptions options)
    {
        var state = await app.Services.GetRequiredService<WebSecurityStamp>().ReadAsync(CancellationToken.None);
        var token = app.Services.GetRequiredService<WebSessionStore>().CreateLaunchToken(state.Revision);
        var url = options.Url + "/#launch=" + token;
        using var scope = app.Services.CreateScope();
        var text = scope.ServiceProvider.GetRequiredService<WebText>();
        text.SetLanguage(await app.Services.GetRequiredService<SettingsService>().GetLanguageAsync(CancellationToken.None));
        await Console.Out.WriteLineAsync(text.Format(LocalizationKeys.WebAuth.StartupLink, url));
        await Console.Out.WriteLineAsync(text[LocalizationKeys.WebAuth.StartupHint]);
        return url;
    }

    private static void AddWebServices(IServiceCollection services)
    {
        services.AddRazorComponents().AddInteractiveServerComponents(options =>
        {
            options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(DisconnectedCircuitRetentionSeconds);
            options.DisconnectedCircuitMaxRetained = MaximumRetainedCircuits;
        }).AddHubOptions(options =>
        {
            options.MaximumReceiveMessageSize = MaximumHubMessageBytes;
            options.AddFilter<WebSessionHubFilter>();
        });
        services.AddSingleton<WebSessionHubFilter>();
        AddSessionServices(services);
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

    private static void AddSessionServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<WebSessionStore>();
        services.AddSingleton<WebSecurityStamp>();
        services.AddScoped<WebSignInService>();
        services.AddScoped<WebCircuitSession>();
        services.AddHttpContextAccessor();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddFixedWindowLimiter(WebAuthenticationEndpoints.SignInPolicy, limiter =>
            {
                limiter.PermitLimit = 5; limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0; limiter.AutoReplenishment = true;
            });
        });
    }

}
