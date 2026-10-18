using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using MongoTui.Infrastructure.Mongo.Driver;

namespace MongoTui.Infrastructure.Tests.Mongo.Driver;

public sealed class MongoDatabaseDiscoveryTests
{
    private readonly IMongoClient _client = A.Fake<IMongoClient>();
    private readonly IMongoDatabase _admin = A.Fake<IMongoDatabase>();
    private readonly IAsyncCursor<BsonDocument> _cursor = A.Fake<IAsyncCursor<BsonDocument>>();
    private readonly MongoDatabaseDiscovery _sut = new();

    public MongoDatabaseDiscoveryTests()
    {
        A.CallTo(() => _client.Settings).Returns(new MongoClientSettings
        {
            Credential = MongoCredential.CreateCredential("admin", "user", "password")
        });
        A.CallTo(() => _client.GetDatabase("admin", A<MongoDatabaseSettings>._)).Returns(_admin);
        A.CallTo(() => _client.ListDatabasesAsync(A<CancellationToken>._)).Returns(_cursor);
        StatusCommand().Returns(new BsonDocument());
    }

    [Fact]
    public async Task Should_IncludeEmptyDatabases_WhenPrivilegesNameDatabaseOrCollection()
    {
        StatusCommand().Returns(BsonDocument.Parse("""
            { "authInfo": { "authenticatedUserPrivileges": [
                { "resource": { "db": "empty", "collection": "" } },
                { "resource": { "db": "empty", "collection": "users" } },
                { "resource": { "db": "scoped", "collection": "users" } },
                { "resource": { "db": "", "collection": "" } },
                { "resource": { "cluster": true } },
                { "resource": { "anyResource": true } }
            ] } }
            """));

        var databases = await _sut.ListAsync(_client, null, CancellationToken.None);

        databases.Select(database => database.Name).Should().BeEquivalentTo("admin", "empty", "scoped");
        databases.Should().OnlyContain(database => database.IsEmpty);
    }

    [Fact]
    public async Task Should_PreserveStoredDatabaseMetadata_WhenAlsoGrantedAndSpecifiedInUri()
    {
        A.CallTo(() => _cursor.MoveNextAsync(A<CancellationToken>._)).ReturnsNextFromSequence(true, false);
        A.CallTo(() => _cursor.Current).Returns([new BsonDocument { { "name", "app" }, { "empty", false } }]);
        StatusCommand().Returns(BsonDocument.Parse("""
            { "authInfo": { "authenticatedUserPrivileges": [
                { "resource": { "db": "app", "collection": "" } }
            ] } }
            """));

        var databases = await _sut.ListAsync(_client, "app", CancellationToken.None);

        databases.Should().ContainSingle(database => database.Name == "app" && !database.IsEmpty);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(59)]
    public async Task Should_ReturnListedDatabases_WhenPrivilegeCommandDeniedOrUnsupported(int code)
    {
        A.CallTo(() => _cursor.MoveNextAsync(A<CancellationToken>._)).ReturnsNextFromSequence(true, false);
        A.CallTo(() => _cursor.Current).Returns([new BsonDocument { { "name", "app" }, { "empty", false } }]);
        var error = new BsonDocument { { "ok", 0 }, { "code", code }, { "errmsg", "Unavailable" } };
        StatusCommand().Throws(new MongoCommandException(new ConnectionId(new ServerId(new ClusterId(),
            new System.Net.DnsEndPoint("localhost", 27017))), "Unavailable", new BsonDocument(), error));

        var databases = await _sut.ListAsync(_client, null, CancellationToken.None);

        databases.Should().ContainSingle(database => database.Name == "app" && !database.IsEmpty);
    }

    [Fact]
    public async Task Should_PropagateCancellation_WhenPrivilegeDiscoveryCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        StatusCommand().Throws(new OperationCanceledException(cancellation.Token));

        var failure = await Record.ExceptionAsync(() => _sut.ListAsync(_client, null, cancellation.Token));

        failure.Should().BeOfType<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_SkipPrivilegesAndIncludeDefaultDatabase_WhenConnectionUnauthenticated()
    {
        A.CallTo(() => _client.Settings).Returns(new MongoClientSettings());

        var databases = await _sut.ListAsync(_client, "empty", CancellationToken.None);

        databases.Should().ContainSingle(database => database.Name == "empty" && database.IsEmpty);
        StatusCommand().MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_IncludeAuthenticationDatabase_WhenNotListedByServerOrPrivileges()
    {
        A.CallTo(() => _client.Settings).Returns(new MongoClientSettings
        {
            Credential = MongoCredential.CreateCredential("empty", "user", "password")
        });

        var databases = await _sut.ListAsync(_client, null, CancellationToken.None);

        databases.Should().ContainSingle(database => database.Name == "empty" && database.IsEmpty);
    }

    [Fact]
    public async Task Should_NotIncludeExternalAuthenticationSource_WhenCertificateAuthenticationUsed()
    {
        A.CallTo(() => _client.Settings).Returns(new MongoClientSettings
        {
            Credential = MongoCredential.CreateMongoX509Credential("certificate user")
        });

        var databases = await _sut.ListAsync(_client, null, CancellationToken.None);

        databases.Should().BeEmpty();
    }

    private FakeItEasy.Configuration.IReturnValueArgumentValidationConfiguration<Task<BsonDocument>> StatusCommand() =>
        A.CallTo(() => _admin.RunCommandAsync(A<Command<BsonDocument>>._, A<ReadPreference>._, A<CancellationToken>._));
}
