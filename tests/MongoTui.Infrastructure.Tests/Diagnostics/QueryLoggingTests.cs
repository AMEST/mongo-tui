using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Testing;

namespace MongoTui.Infrastructure.Tests.Diagnostics;

public sealed class QueryLoggingTests
{
    private const string Sensitive = "secret-filter-document-value";
    private readonly LoggingTestSink _sink = new();
    private readonly IMongoGateway _gateway = A.Fake<IMongoGateway>();
    private readonly IMongoShellSession _shell = A.Fake<IMongoShellSession>();
    private readonly ISettingsStore _settings = A.Fake<ISettingsStore>();
    private static readonly MongoConnectionTarget Target = new($"mongodb://user:{Sensitive}@localhost");
    private static readonly CollectionRef Collection = new(DatabaseName.Create("demo"), CollectionName.Create("users"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_LogCountsAndContextWithoutPayload_When_QueryCompletes(bool driver)
    {
        using var factory = _sink.CreateFactory();
        var expected = new QueryResult { Kind = QueryResultKind.Documents, Documents = [new BsonDocument("secret", Sensitive)] };
        A.CallTo(() => _gateway.FindAsync(Target, Collection, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Returns(expected);
        A.CallTo(() => _shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).Returns(expected);
        await using var queries = CreateQueries(factory);

        await ExecuteAsync(queries, driver);

        _sink.Entries.Should().HaveCount(2);
        var completed = _sink.Entries.Single(entry => entry.Level == LogLevel.Information);
        completed.Category.Should().Be(typeof(QueryService).FullName);
        completed.Properties["ResultCount"].Should().Be(1);
        completed.Properties["Database"].Should().Be("demo");
        _sink.Text.Should().NotContain(Sensitive).And.NotContain("mongodb://");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_LogTimeoutOnce_When_DeadlineExpires(bool driver)
    {
        using var factory = _sink.CreateFactory();
        A.CallTo(() => _settings.GetAsync("query.timeoutSeconds", A<CancellationToken>._)).Returns("1");
        A.CallTo(() => _shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._))
            .ReturnsLazily(call => WaitAsync(call.GetArgument<CancellationToken>(2)));
        A.CallTo(() => _gateway.FindAsync(Target, Collection, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .ReturnsLazily(call => WaitAsync(call.GetArgument<CancellationToken>(4)));
        await using var queries = CreateQueries(factory);

        var act = () => ExecuteAsync(queries, driver);

        await act.Should().ThrowAsync<QueryTimeoutException>();
        var failure = _sink.Entries.Single(entry => entry.Level >= LogLevel.Information);
        failure.Level.Should().Be(LogLevel.Warning);
        failure.Properties["TimeoutSeconds"].Should().Be(1);
        failure.Message.Should().Contain("timed out");
    }

    [Fact]
    public async Task Should_LogSafeCauseOnce_When_ShellFailureContainsSecrets()
    {
        using var factory = _sink.CreateFactory();
        var error = new QueryExecutionException(Sensitive, new InvalidOperationException("mongodb://user:secret@host " + Sensitive));
        A.CallTo(() => _shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).ThrowsAsync(error);
        await using var queries = CreateQueries(factory);

        var act = () => ExecuteAsync(queries, false);

        await act.Should().ThrowAsync<QueryExecutionException>();
        _sink.Entries.Count(entry => entry.Level >= LogLevel.Warning).Should().Be(1);
        _sink.Text.Should().Contain(nameof(QueryExecutionException)).And.Contain(nameof(InvalidOperationException))
            .And.NotContain(Sensitive).And.NotContain("mongodb://");
        _sink.Entries.Should().OnlyContain(entry => entry.Exception == null);
    }

    [Fact]
    public async Task Should_LogDocumentWriteWithoutPayload_When_ServiceInserts()
    {
        using var factory = _sink.CreateFactory();
        var documents = new DocumentService(_gateway, factory.CreateLogger<DocumentService>());

        await documents.InsertAsync(Target, Collection, $"{{password:'{Sensitive}'}}", CancellationToken.None);

        var entry = _sink.Entries.Should().ContainSingle().Subject;
        entry.Properties["Database"].Should().Be("demo");
        entry.Properties["Collection"].Should().Be("users");
        _sink.Text.Should().NotContain(Sensitive).And.NotContain("mongodb://");
    }

    private QueryService CreateQueries(ILoggerFactory loggerFactory)
    {
        var sessions = A.Fake<IMongoShellSessionFactory>();
        A.CallTo(() => sessions.CreateAsync(Target, A<CancellationToken>._)).Returns(_shell);
        return new(sessions, A.Fake<IQueryHistoryRepository>(), _gateway, new SettingsService(_settings), loggerFactory.CreateLogger<QueryService>());
    }

    private static Task<QueryResult> ExecuteAsync(QueryService queries, bool driver) => driver
        ? queries.FindAsync(Target, Collection, $"{{secret:'{Sensitive}'}}", new QueryExecutionOptions { Skip = 50, Limit = 51 }, CancellationToken.None)
        : queries.ExecuteAsync(new() { ConnectionId = ConnectionId.New(), TabId = Guid.NewGuid(), Target = Target,
            Database = "demo", Collection = "users", Script = $"db.users.find({{secret:'{Sensitive}'}})" }, CancellationToken.None);

    private static async Task<QueryResult> WaitAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return QueryResult.Empty(QueryResultKind.Documents);
    }
}
