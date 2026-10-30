using AwesomeAssertions;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;

namespace MongoTui.Application.Tests.Services;

public sealed class ConnectionProfilePreparationTests
{
    [Theory]
    [InlineData("field-password", "uri-password", "field-password")]
    [InlineData("", "uri-password", "uri-password")]
    [InlineData(null, "", "")]
    [InlineData("", null, "saved-password")]
    public void Should_PreferFieldsThenUriThenSavedSecret_WhenPreparingDraft(string? field, string? uri, string expected)
    {
        var original = Original();
        var draft = Draft(original) with { Password = field,
            Uri = uri is null ? "mongodb://host" : $"mongodb://uri-user:{uri}@host" };

        var prepared = ConnectionProfilePreparation.Prepare(draft, original, "saved-password");

        prepared.Secret.Should().Be(expected);
        prepared.Profile.Uri.Should().Be("mongodb://host");
    }

    [Fact]
    public void Should_PreserveSettingsAndCreationTime_WhenRenamingConnection()
    {
        var original = Original();

        var prepared = ConnectionProfilePreparation.Prepare(Draft(original), original, "saved-password");

        prepared.Profile.Id.Should().Be(original.Id);
        prepared.Profile.Name.Should().Be("renamed");
        prepared.Profile.Auth.Should().Be(original.Auth);
        prepared.Profile.Tls.Should().Be(original.Tls);
        prepared.Profile.Ssh.Should().Be(original.Ssh);
        prepared.Profile.CreatedAt.Should().Be(original.CreatedAt);
    }

    [Fact]
    public void Should_MergeAuthenticationFields_WhenUriAndFieldsArePartial()
    {
        var original = Original();
        var draft = Draft(original) with { Uri = "mongodb://uri-user:pw@host/app?authSource=uri-auth",
            Auth = new AuthOptions { Username = "typed-user", Kind = AuthKind.ScramSha1 } };

        var prepared = ConnectionProfilePreparation.Prepare(draft, original);

        prepared.Profile.Auth.Username.Should().Be("typed-user");
        prepared.Profile.Auth.AuthDatabase.Should().Be("uri-auth");
        prepared.Profile.Auth.Kind.Should().Be(AuthKind.ScramSha1);
    }

    [Fact]
    public void Should_AllowDisablingAuthentication_WhenFieldsExplicitlySelectNone()
    {
        var original = Original();

        var prepared = ConnectionProfilePreparation.Prepare(Draft(original) with { Auth = new AuthOptions() }, original);

        prepared.Profile.Auth.Kind.Should().Be(AuthKind.None);
    }

    [Fact]
    public void Should_ExtractSavedLegacyCredentials_WhenDraftHasNoNewCredentials()
    {
        var original = Original() with { Uri = "mongodb://legacy:old-password@host", Auth = new AuthOptions() };

        var prepared = ConnectionProfilePreparation.Prepare(Draft(original) with { Uri = "mongodb://host" }, original);

        prepared.Profile.Auth.Username.Should().Be("legacy");
        prepared.Secret.Should().Be("old-password");
    }

    [Fact]
    public void Should_RejectEmptyName_WhenPreparingSavedProfile()
    {
        var draft = Draft(Original()) with { Name = " " };

        var act = () => ConnectionProfilePreparation.Prepare(draft);

        act.Should().Throw<DomainException>().WithMessage("Connection name must not be empty.");
    }

    private static ConnectionProfileDraft Draft(ConnectionProfile original) =>
        new() { Id = original.Id, Name = "renamed", Uri = original.Uri };

    private static ConnectionProfile Original() => ConnectionProfile.Create(ConnectionId.New(), "original", "mongodb://host/app",
        new AuthOptions { Kind = AuthKind.ScramSha256, Username = "saved-user", AuthDatabase = "saved-auth" },
        new TlsOptions { Enabled = true }, new SshTunnelOptions { Enabled = true, Host = "jump" },
        DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
}
