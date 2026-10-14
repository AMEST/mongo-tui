using System.Diagnostics;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Infrastructure.Ssh;

namespace MongoTui.Infrastructure.Tests.Ssh;

public sealed class SshCliTunnelStartupTests : IDisposable
{
    private readonly string _script = Path.Combine(Path.GetTempPath(), "mt-ssh-" + Guid.NewGuid().ToString("N"));
    private readonly string _pidFile = Path.Combine(Path.GetTempPath(), "mt-pid-" + Guid.NewGuid().ToString("N"));
    private readonly ITwoFactorPrompt _prompt = A.Fake<ITwoFactorPrompt>();
    private readonly SshTunnelOptions _ssh = new() { Enabled = true, Host = "jump" };

    public void Dispose()
    {
        File.Delete(_script);
        File.Delete(_pidFile);
    }

    [UnixFact]
    public async Task Should_KillProcessAndReportTimeout_WhenStartupNeverFinishes()
    {
        var factory = await CreateFactoryAsync(TimeSpan.FromSeconds(2));

        var act = async () => await factory.StartAsync(_ssh, _prompt, CancellationToken.None);

        await act.Should().ThrowAsync<ConnectionFailedException>().WithMessage("*timed out*");
        await AssertProcessStoppedAsync();
    }

    [UnixFact]
    public async Task Should_KillProcessAndPreserveCancellation_WhenCallerCancelsStartup()
    {
        var factory = await CreateFactoryAsync(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var pending = factory.StartAsync(_ssh, _prompt, cancellation.Token);
        using var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await WaitForProcessStartAsync(startupTimeout.Token);

        await cancellation.CancelAsync();

        var act = async () => await pending;
        await act.Should().ThrowAsync<OperationCanceledException>();
        await AssertProcessStoppedAsync();
    }

    private async Task WaitForProcessStartAsync(CancellationToken cancellationToken)
    {
        while (!File.Exists(_pidFile) || new FileInfo(_pidFile).Length == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    private async Task<SshCliTunnelFactory> CreateFactoryAsync(TimeSpan timeout)
    {
        var quotedPidFile = "'" + _pidFile.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
        await File.WriteAllTextAsync(_script, $"#!/bin/sh\nprintf '%s' \"$$\" > {quotedPidFile}\nexec sleep 30\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new SshCliTunnelFactory(new SshCliOptions("unused-helper")
        { SshExecutable = _script, StartupTimeout = timeout }, new SshAskPassFactory());
    }

    private async Task AssertProcessStoppedAsync()
    {
        var pid = int.Parse(await File.ReadAllTextAsync(_pidFile));

        var act = () => System.Diagnostics.Process.GetProcessById(pid);

        act.Should().Throw<ArgumentException>();
    }
}

internal sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "The fake SSH executable requires a POSIX shell.";
        }
    }
}
