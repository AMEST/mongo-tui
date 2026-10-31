using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Queries;

namespace MongoTui.Web.Tests;

public sealed class QueryWorkspaceErrorTests
{
    [Theory]
    [InlineData(false, "en", "Query execution failed:")]
    [InlineData(false, "ru", "Ошибка выполнения запроса:")]
    [InlineData(true, "en", "Query execution failed:")]
    [InlineData(true, "ru", "Ошибка выполнения запроса:")]
    public async Task Should_ShowCauseWithoutUri_WhenShellOrDriverQueryFails(bool browse, string language, string prefix)
    {
        await using var fixture = new WorkspaceFixture();
        fixture.Text.SetLanguage(language);
        await fixture.Catalog.ConnectAsync(fixture.Profile, CancellationToken.None);
        var tab = fixture.Workspace.Open(fixture.Profile.Id, "demo", "orders", browse ? null : "db.orders.find({})");
        var failure = new FormatException("Invalid JSON from mongodb://alice:secret@host/db");
        A.CallTo(() => fixture.Shell.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).Throws(failure);
        A.CallTo(() => fixture.Gateway.FindAsync(A<MongoConnectionTarget>._, A<CollectionRef>._,
            A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._)).Throws(failure);

        await fixture.Workspace.ExecuteAsync(tab);

        tab.Error.Should().StartWith(prefix).And.Contain("FormatException: Invalid JSON")
            .And.NotContain("alice").And.NotContain("secret").And.NotContain("host");
        tab.Busy.Should().BeFalse();
    }
}
