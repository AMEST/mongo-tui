using AwesomeAssertions;
using FakeItEasy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.DependencyInjection;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Web.Components.Shared;
using MongoTui.Web.Localization;
using MongoTui.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace MongoTui.Web.Tests;

public sealed class SecurityGateTests
{
    private readonly Dictionary<string, string> _values = new();
    private readonly IKekSource _source = A.Fake<IKekSource>();

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RefreshStaleSetup_WhenAnotherCircuitAlreadyInitialized(string language)
    {
        await using var services = CreateServices(language);
        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        await using var firstRenderer = new ComponentTestRenderer(first.ServiceProvider);
        await using var secondRenderer = new ComponentTestRenderer(second.ServiceProvider);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(SecurityGate.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, "workspace")),
        };
        await firstRenderer.MountAsync<SecurityGate>(parameters);
        await secondRenderer.MountAsync<SecurityGate>(parameters);
        await firstRenderer.ChangeAsync("None");
        await secondRenderer.ChangeAsync("None");
        await firstRenderer.SubmitAsync();

        await secondRenderer.SubmitAsync();

        (await secondRenderer.TextAsync()).Should().Be("workspace");
        A.CallTo(() => _source.CreateAsync(null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        first.ServiceProvider.GetRequiredService<SecretKeyService>().Should()
            .BeSameAs(second.ServiceProvider.GetRequiredService<SecretKeyService>());
    }

    private ServiceProvider CreateServices(string language)
    {
        var settings = A.Fake<ISettingsStore>();
        A.CallTo(() => settings.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) => Task.FromResult(_values.GetValueOrDefault(key)));
        A.CallTo(() => settings.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => _values[key] = value).Returns(Task.CompletedTask);
        A.CallTo(() => settings.GetAllAsync(A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_values)));
        _values["ui.language"] = language;
        A.CallTo(() => _source.Mode).Returns(SecurityMode.None);
        A.CallTo(() => _source.IsAvailable).Returns(true);
        A.CallTo(() => _source.HasMaterialAsync(A<CancellationToken>._)).ReturnsLazily(() => _values.ContainsKey("security.kek_mode"));
        A.CallTo(() => _source.CreateAsync(null, A<CancellationToken>._)).Returns(new byte[32]);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(settings);
        services.AddSingleton(_source);
        services.AddSingleton(A.Fake<IConnectionRepository>());
        services.AddSingleton(A.Fake<ICryptoService>());
        services.AddSingleton(A.Fake<IStorageTransaction>());
        services.AddSingleton(A.Fake<IMongoGateway>());
        services.AddSingleton(A.Fake<ISshTunnelFactory>());
        services.AddSingleton(A.Fake<ITwoFactorPrompt>());
        services.AddSingleton(A.Fake<IJSRuntime>());
        services.AddScoped<WebText>();
        services.AddApplication();
        services.AddSingleton<TimeProvider>(new SessionClock());
        services.AddSingleton<WebSessionStore>();
        services.AddSingleton<WebSecurityStamp>();
        services.AddScoped<WebCircuitSession>();
        services.AddScoped<AuthenticationStateProvider>(provider =>
        {
            var authentication = A.Fake<AuthenticationStateProvider>();
            Task<AuthenticationState>? state = null;
            A.CallTo(() => authentication.GetAuthenticationStateAsync()).ReturnsLazily(() => state ??= CreateStateAsync(provider));
            return authentication;
        });
        services.Replace(ServiceDescriptor.Scoped<SecurityModeService, SecurityModeService>());
        services.Replace(ServiceDescriptor.Scoped<ConnectionService, ConnectionService>());
        services.Replace(ServiceDescriptor.Scoped<ConnectionTunnelService, ConnectionTunnelService>());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task<AuthenticationState> CreateStateAsync(IServiceProvider provider)
    {
        var sessions = provider.GetRequiredService<WebSessionStore>();
        var stamp = await provider.GetRequiredService<WebSecurityStamp>().ReadAsync(CancellationToken.None);
        var id = sessions.Authenticate(sessions.Create(stamp.Revision, canInitialize: true), stamp.Revision)!;
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "WebSession")));
    }
}
