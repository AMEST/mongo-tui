using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Web.Services;

namespace MongoTui.Web.Tests;

internal sealed class UserManagementFixture : IAsyncDisposable
{
    public WorkspaceFixture Workspace { get; }
    public IMongoUserGateway Gateway { get; } = A.Fake<IMongoUserGateway>();
    public WebUserManagement Users { get; }
    public DatabaseSelection Selection => new(Workspace.Profile.Id, "admin");

    public UserManagementFixture(ILoggerFactory? loggerFactory = null)
    {
        Workspace = new(loggerFactory);
        Users = new(Workspace.Catalog, new UserManagementService(Gateway, new PasswordGenerator()), Workspace.Loggers.CreateLogger<WebUserManagement>());
    }

    public async Task ConnectAsync(string? username = null, string authDatabase = "admin", string? uri = null)
    {
        var profile = Workspace.Profile with
        {
            Uri = uri ?? Workspace.Profile.Uri,
            Auth = new AuthOptions { Kind = username is null ? AuthKind.None : AuthKind.ScramSha256,
                Username = username, AuthDatabase = authDatabase },
        };
        A.CallTo(() => Workspace.Repository.GetAsync(profile.Id, A<CancellationToken>._)).Returns(new StoredConnection(profile, null));
        await Workspace.Catalog.ConnectAsync(profile, CancellationToken.None);
    }

    public ValueTask DisposeAsync() => Workspace.DisposeAsync();
}
