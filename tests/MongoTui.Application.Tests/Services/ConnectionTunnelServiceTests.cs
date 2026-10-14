using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Services;

public sealed class ConnectionTunnelServiceTests
{
    private readonly ISshTunnelFactory _factory = A.Fake<ISshTunnelFactory>();
    private readonly ITwoFactorPrompt _prompt = A.Fake<ITwoFactorPrompt>();
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly ISshTunnelSession _session = A.Fake<ISshTunnelSession>();
    private readonly ConnectionTunnelService _sut;
    private readonly ConnectionProfile _profile = ConnectionProfile.Create(ConnectionId.New(), "tpam",
        "mongodb://node1:27017,node2:27017/?replicaSet=rs",
        ssh: new SshTunnelOptions { Enabled = true, Host = "jump" });

    public ConnectionTunnelServiceTests()
    {
        _sut = new ConnectionTunnelService(_factory, _prompt, _gateway);
        A.CallTo(() => _session.LocalPort).Returns(1080);
        A.CallTo(() => _session.DisposeAsync()).Returns(ValueTask.CompletedTask);
        A.CallTo(() => _factory.StartAsync(A<SshTunnelOptions>._, _prompt, A<CancellationToken>._)).Returns(_session);
    }

    [Fact]
    public async Task Should_PreserveTargetAndSkipSsh_WhenDisabled()
    {
        var target = new MongoConnectionTarget("mongodb://host?proxyHost=external&proxyPort=42");

        var connection = await _sut.OpenAsync(_profile with { Ssh = new SshTunnelOptions() }, target, CancellationToken.None);

        connection.Target.Should().BeSameAs(target);
        connection.Tunnel.Should().BeNull();
        A.CallTo(() => _factory.StartAsync(A<SshTunnelOptions>._, A<ITwoFactorPrompt>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_StartTunnelAndReplaceProxy_WhenEnabled()
    {
        var target = new MongoConnectionTarget(_profile.Uri);

        var connection = await _sut.OpenAsync(_profile, target, CancellationToken.None);

        connection.Target.ConnectionString.Should().Be(_profile.Uri + "&proxyHost=127.0.0.1&proxyPort=1080");
        _profile.Uri.Should().NotContain("proxyHost");
        A.CallTo(() => _factory.StartAsync(_profile.Ssh, _prompt, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_DisposeSessionOnce_WhenConnectionAndApplicationClose()
    {
        var connection = await _sut.OpenAsync(_profile, new MongoConnectionTarget(_profile.Uri), CancellationToken.None);

        await connection.DisposeAsync();

        await _sut.DisposeAsync();
        A.CallTo(() => _session.DisposeAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _gateway.ReleaseClient(connection.Target)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_CloseActiveSession_WhenApplicationExits()
    {
        var connection = await _sut.OpenAsync(_profile, new MongoConnectionTarget(_profile.Uri), CancellationToken.None);

        await _sut.DisposeAsync();

        A.CallTo(() => _session.DisposeAsync()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _gateway.ReleaseClient(connection.Target)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_ReleaseClient_WhenTunnelCleanupFails()
    {
        var connection = await _sut.OpenAsync(_profile, new MongoConnectionTarget(_profile.Uri), CancellationToken.None);
        A.CallTo(() => _session.DisposeAsync()).Throws(new IOException("SSH cleanup failed."));

        var failure = await Record.ExceptionAsync(() => connection.DisposeAsync().AsTask());

        failure.Should().BeOfType<IOException>();
        A.CallTo(() => _gateway.ReleaseClient(connection.Target)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_CancelPendingAuthentication_WhenApplicationExits()
    {
        A.CallTo(() => _factory.StartAsync(A<SshTunnelOptions>._, _prompt, A<CancellationToken>._))
            .ReturnsLazily((SshTunnelOptions _, ITwoFactorPrompt _, CancellationToken token) => WaitForCancellationAsync(token));
        var pending = _sut.OpenAsync(_profile, new MongoConnectionTarget(_profile.Uri), CancellationToken.None);

        await _sut.DisposeAsync();

        var act = async () => await pending;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_StartTunnelAndReplaceProxy_WhenSrvUriProvided()
    {
        var profile = _profile with { Uri = "mongodb+srv://cluster.example.com/app?proxyHost=old&proxyPort=42&authSource=admin" };
        var target = new MongoConnectionTarget(profile.Uri);

        await using var connection = await _sut.OpenAsync(profile, target, CancellationToken.None);

        connection.Target.ConnectionString.Should().Be(
            "mongodb+srv://cluster.example.com/app?authSource=admin&proxyHost=127.0.0.1&proxyPort=1080");
        profile.Uri.Should().Be(target.ConnectionString);
        A.CallTo(() => _factory.StartAsync(profile.Ssh, _prompt, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_DomainExceptionWithoutStartingSsh_WhenNonMongoUriProvided()
    {
        var target = new MongoConnectionTarget("https://cluster.example.com");

        var act = async () => await _sut.OpenAsync(_profile, target, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        A.CallTo(() => _factory.StartAsync(A<SshTunnelOptions>._, A<ITwoFactorPrompt>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static async Task<ISshTunnelSession> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Unreachable.");
    }
}
