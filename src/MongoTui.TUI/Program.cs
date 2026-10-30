using MongoTui.Application.Localization;
using MongoTui.Application.DependencyInjection;
using MongoTui.Infrastructure.DependencyInjection;
using MongoTui.Infrastructure.FileSystem;
using MongoTui.Infrastructure.Ssh;
using MongoTui.TUI.Bootstrap;
using MongoTui.TUI.Localization;
using MongoTui.Web.Bootstrap;
using MongoTui.Web.Localization;
using MongoTui.Infrastructure.Diagnostics;
using Microsoft.Extensions.Hosting;
using Serilog;

if (Environment.GetEnvironmentVariable(SshAskPassEnvironment.Pipe) is { Length: > 0 } pipeName)
{
    return await new SshAskPassClient().RunAsync(pipeName, args.FirstOrDefault() ?? string.Empty, CancellationToken.None);
}

if (args.Contains("--version", StringComparer.Ordinal))
{
    var localizer = new ResxLocalizer();
    await Console.Out.WriteLineAsync(ApplicationVersion.GetInformationalVersion());
    await Console.Out.WriteLineAsync(localizer.Get(LocalizationKeys.About.License));
    await Console.Out.WriteLineAsync(localizer.Get(LocalizationKeys.About.Copyright));
    await Console.Out.WriteLineAsync(localizer.Get(LocalizationKeys.About.CoAuthor));
    return 0;
}

WebOptions? webOptions = null;
if (args.FirstOrDefault() == "web")
{
    try { webOptions = WebOptions.Parse(args[1..]); }
    catch (ArgumentException)
    {
        await Console.Error.WriteLineAsync(new WebText()[LocalizationKeys.Cli.Usage]);
        return 2;
    }
    if (webOptions.Help)
    {
        await Console.Out.WriteLineAsync(new WebText()[LocalizationKeys.Cli.Usage]);
        return 0;
    }
}

Directory.CreateDirectory(AppPaths.LogsDirectory);

var logLevels = new SerilogLevelControl(Environment.GetEnvironmentVariable(SerilogLevelControl.EnvironmentVariable));

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(logLevels.Application)
    .MinimumLevel.Override("Microsoft", logLevels.Framework)
    .MinimumLevel.Override("System", logLevels.Framework)
    .MinimumLevel.Override("MongoDB", logLevels.Framework)
    .Enrich.FromLogContext()
    .WriteTo.File(
        AppPaths.LogFilePattern,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

if (logLevels.InvalidEnvironmentOverride)
    Log.Warning("Ignoring invalid logging level in the environment");

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    Log.Fatal("Unhandled exception ({ExceptionType})", args.ExceptionObject.GetType().Name);

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Log.Error("Unobserved task exception ({ExceptionType})", args.Exception.GetType().Name);
    args.SetObserved();
};

try
{
    if (webOptions is not null)
        return await new MongoTui.Web.Bootstrap.WebHost().RunAsync(webOptions, AddCoreServices);

    await using var provider = BuildServiceProvider();
    var monitor = provider.GetServices<IHostedService>().OfType<LoggingSettingsMonitor>().Single();
    await monitor.StartAsync(CancellationToken.None);
    try { return await provider.GetRequiredService<AppHost>().RunAsync(); }
    finally { await monitor.StopAsync(CancellationToken.None); }
}
catch (Exception ex)
{
    Log.Fatal("Application terminated unexpectedly ({ExceptionType})", ex.GetType().Name);
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

ServiceProvider BuildServiceProvider()
{
    var services = new ServiceCollection();
    AddCoreServices(services);
    services.AddTui();
    // The terminal mode owns its only container; the web mode uses WebApplication instead.
#pragma warning disable ASP0000
    return services.BuildServiceProvider();
#pragma warning restore ASP0000
}

void AddCoreServices(IServiceCollection services)
{
    services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));
    services.AddSingleton(logLevels);
    services.AddApplication();
    services.AddInfrastructure();
    var executable = Environment.ProcessPath!;
    if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
    {
        executable = Path.Combine(AppContext.BaseDirectory, "mongo-tui" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
    }
    services.AddSingleton(new SshCliOptions(executable));
}
