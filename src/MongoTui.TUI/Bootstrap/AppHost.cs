using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoTui.TUI.Themes;
using MongoTui.TUI.Views;
using Terminal.Gui.App;

namespace MongoTui.TUI.Bootstrap;

/// <summary>Owns the Terminal.Gui application lifecycle.</summary>
public sealed class AppHost
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AppHost> _logger;

    public AppHost(IServiceProvider services, ILogger<AppHost> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>Initializes the TUI and blocks until the user quits.</summary>
    public int Run()
    {
        _logger.LogInformation("MongoTui starting");

        using var application = Terminal.Gui.App.Application.Create();
        application.Init();

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
            _logger.LogInformation("MongoTui stopped");
        }
    }

    private bool HandleError(Exception exception)
    {
        _logger.LogError(exception, "Unhandled error in the UI loop");

        return true;
    }
}
