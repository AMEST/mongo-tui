using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Services;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Tests.Services;

public sealed class QueryFileServiceTests
{
    private readonly IQueryFileStore _store = A.Fake<IQueryFileStore>();
    private readonly ISettingsStore _settings = A.Fake<ISettingsStore>();
    private readonly Dictionary<string, string> _values = [];
    private readonly QueryFileService _sut;

    public QueryFileServiceTests()
    {
        A.CallTo(() => _settings.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) => Task.FromResult(_values.TryGetValue(key, out var value) ? value : null));
        A.CallTo(() => _settings.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => _values[key] = value)
            .Returns(Task.CompletedTask);
        A.CallTo(() => _store.ReadAsync(A<string>._, A<CancellationToken>._)).Returns("db.users.find({})");
        _sut = new QueryFileService(_store, _settings, NullLogger<QueryFileService>.Instance);
    }

    [Fact]
    public async Task Should_AppendExtension_When_SavingWithoutOne()
    {
        var saved = await _sut.SaveAsync("/tmp/report", "db.users.find({})", CancellationToken.None);

        saved.Path.Should().Be("/tmp/report.js");
        A.CallTo(() => _store.WriteAsync("/tmp/report.js", "db.users.find({})", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Should_KeepExtension_When_SavingWithOne()
    {
        var saved = await _sut.SaveAsync("/tmp/report.mongo.js", "db.users.find({})", CancellationToken.None);

        saved.Path.Should().Be("/tmp/report.mongo.js");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_PathIsBlank(string path)
    {
        var act = async () => await _sut.SaveAsync(path, "db.users.find({})", CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        A.CallTo(() => _store.WriteAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_Throw_When_ScriptIsBlank(string script)
    {
        var act = async () => await _sut.SaveAsync("/tmp/report.js", script, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_ReadFileAndRecordRecent_When_Opening()
    {
        var file = await _sut.OpenAsync("/tmp/report.js", CancellationToken.None);

        file.Script.Should().Be("db.users.find({})");
        A.CallTo(() => _store.ReadAsync("/tmp/report.js", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        (await _sut.GetRecentAsync(CancellationToken.None)).Should().Equal("/tmp/report.js");
    }

    [Fact]
    public async Task Should_PutNewestFirstAndDeduplicate_When_AddingRecent()
    {
        await _sut.AddRecentAsync("/tmp/a.js", CancellationToken.None);
        await _sut.AddRecentAsync("/tmp/b.js", CancellationToken.None);
        await _sut.AddRecentAsync("/tmp/A.JS", CancellationToken.None);

        (await _sut.GetRecentAsync(CancellationToken.None)).Should().Equal("/tmp/A.JS", "/tmp/b.js");
    }

    [Fact]
    public async Task Should_LimitRecentFiles_When_ManyAreAdded()
    {
        for (var index = 0; index < QueryFileService.MaximumRecentFiles + 5; index++)
        {
            await _sut.AddRecentAsync($"/tmp/file{index}.js", CancellationToken.None);
        }

        var recent = await _sut.GetRecentAsync(CancellationToken.None);

        recent.Should().HaveCount(QueryFileService.MaximumRecentFiles);
        recent[0].Should().Be($"/tmp/file{QueryFileService.MaximumRecentFiles + 4}.js");
    }

    [Fact]
    public async Task Should_ClearRecent_When_Called()
    {
        await _sut.AddRecentAsync("/tmp/a.js", CancellationToken.None);

        await _sut.ClearRecentAsync(CancellationToken.None);

        (await _sut.GetRecentAsync(CancellationToken.None)).Should().BeEmpty();
    }
}
