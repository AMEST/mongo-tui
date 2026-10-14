using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Services;
using MongoTui.TUI.Services;
using MongoTui.TUI.Themes;
using MongoTui.TUI.Views;
using Terminal.Gui.App;

namespace MongoTui.TUI.Bootstrap;

/// <summary>Owns the Terminal.Gui application lifecycle.</summary>
public sealed class AppHost
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AppHost> _logger;
    private readonly SshAuthenticationPrompt _sshPrompt;
    private readonly ConnectionTunnelService _tunnels;

    public AppHost(IServiceProvider services, ILogger<AppHost> logger,
        SshAuthenticationPrompt sshPrompt, ConnectionTunnelService tunnels)
    {
        _services = services;
        _logger = logger;
        _sshPrompt = sshPrompt;
        _tunnels = tunnels;
    }

    /// <summary>Initializes the TUI and blocks until the user quits.</summary>
    public async Task<int> RunAsync()
    {
        _logger.LogInformation("MongoTui starting");

        using var application = Terminal.Gui.App.Application.Create();
        application.Init();
        _sshPrompt.Application = application;

        try
        {
            var window = _services.GetRequiredService<MainWindow>();
            window.Application = application;
            DarkTheme.Apply(window);
            window.Compose();

            application.AddTimeout(
                TimeSpan.FromMilliseconds(150),
                () =>
                {
                    _ = window.OpenStartupAsync();
                    return false;
                });

            application.Run(window, HandleError);

            return 0;
        }
        finally
        {
            await _tunnels.DisposeAsync();
            _sshPrompt.Application = null;
            _logger.LogInformation("MongoTui stopped");
        }
    }

    private bool HandleError(Exception exception)
    {
        _logger.LogError(exception, "Unhandled error in the UI loop");

        return true;
    }
}
