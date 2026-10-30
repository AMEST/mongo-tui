using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FakeItEasy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Application.DependencyInjection;
using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;
using MongoTui.Testing;
using MongoTui.Web.Bootstrap;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

internal sealed class WebHostFixture(WebApplication application, MemorySettingsStore store, HttpClient client, IKekSource password) : IAsyncDisposable
{
    public const string Password = "web-test-master-123";
    public MemorySettingsStore Store { get; } = store;
    public HttpClient Client { get; } = client;
    public IServiceProvider Services => application.Services;
    public IKekSource PasswordSource => password;

    public static async Task<WebHostFixture> StartAsync(SecurityMode mode = SecurityMode.Password, bool firstRun = false, TimeProvider? clock = null)
    {
        var store = new MemorySettingsStore();
        if (!firstRun)
        {
            await store.SetAsync("security.kek_mode", mode.ToString().ToLowerInvariant(), CancellationToken.None);
            if (mode == SecurityMode.Password)
            {
                await store.SetAsync("security.kek_salt", "existing-salt", CancellationToken.None);
                await store.SetAsync("security.kek_check", "existing-check", CancellationToken.None);
            }
        }
        var source = PasswordSourceFor(store);
        var app = new WebHost().Build(new WebOptions(0), services =>
        {
            services.AddApplication();
            services.AddSingleton<ISettingsStore>(store);
            services.AddSingleton(source);
            services.AddSingleton(A.Fake<ICryptoService>());
            services.AddSingleton(A.Fake<IConnectionRepository>());
            services.AddSingleton(A.Fake<IStorageTransaction>());
            services.AddSingleton(A.Fake<IMongoGateway>());
            services.AddSingleton(A.Fake<ISshTunnelFactory>());
            services.AddSingleton(A.Fake<ITwoFactorPrompt>());
            if (clock is not null) services.AddSingleton(clock);
        });
        await app.StartAsync();
        return new WebHostFixture(app, store, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }, source);
    }

    public async Task<string> AuthenticateAsync()
    {
        using var response = await Client.PostAsJsonAsync("/auth/password", new { password = Password });
        response.EnsureSuccessStatusCode();
        var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString()!;
        Authorize(token);
        return token;
    }

    public void Authorize(string token) => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public async Task<string> LaunchAsync()
    {
        var state = await Services.GetRequiredService<WebSecurityStamp>().ReadAsync(CancellationToken.None);
        return Services.GetRequiredService<WebSessionStore>().CreateLaunchToken(state.Revision);
    }

    public async ValueTask DisposeAsync() { Client.Dispose(); await application.DisposeAsync(); }

    private static IKekSource PasswordSourceFor(MemorySettingsStore store)
    {
        var source = A.Fake<IKekSource>();
        A.CallTo(() => source.Mode).Returns(SecurityMode.Password);
        A.CallTo(() => source.IsAvailable).Returns(true);
        A.CallTo(() => source.HasMaterialAsync(A<CancellationToken>._))
            .ReturnsLazily(async (CancellationToken token) => await store.GetAsync("security.kek_salt", token) is not null);
        A.CallTo(() => source.UnlockAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string? value, CancellationToken _) => value == Password
                ? Task.FromResult(new byte[32]) : Task.FromException<byte[]>(new DomainException("Invalid master password.")));
        return source;
    }
}
