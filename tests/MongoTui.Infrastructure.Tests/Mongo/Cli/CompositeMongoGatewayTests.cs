using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Queries;
using MongoTui.Domain.Transfer;
using MongoTui.Infrastructure.Mongo.Cli;

namespace MongoTui.Infrastructure.Tests.Mongo.Cli;

public sealed class CompositeMongoGatewayTests
{
    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");

    [Fact]
    public async Task Should_RouteFindToDriver_When_FindRequested()
    {
        var driver = A.Fake<IDriverMongoGateway>();
        var cli = A.Fake<ICliMongoTransferGateway>();
        var expected = QueryResult.Empty(QueryResultKind.Documents);
        A.CallTo(() => driver.FindAsync(
                A<MongoConnectionTarget>._, A<CollectionRef>._, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .Returns(expected);
        var sut = new CompositeMongoGateway(driver, cli);

        var result = await sut.FindAsync(Target, NewCollection(), "{}", new QueryExecutionOptions(), CancellationToken.None);

        result.Should().BeSameAs(expected);
        A.CallTo(() => cli.ExportAsync(A<MongoConnectionTarget>._, A<ExportRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Should_RouteExportToCli_When_ExportRequested()
    {
        var driver = A.Fake<IDriverMongoGateway>();
        var cli = A.Fake<ICliMongoTransferGateway>();
        var sut = new CompositeMongoGateway(driver, cli);
        var request = new ExportRequest
        {
            Collection = NewCollection(),
            OutputPath = "/tmp/out.json",
            Format = ExportFormat.Json,
        };

        await sut.ExportAsync(Target, request, CancellationToken.None);

        A.CallTo(() => cli.ExportAsync(Target, request, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => driver.FindAsync(
                A<MongoConnectionTarget>._, A<CollectionRef>._, A<string>._, A<QueryExecutionOptions>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private static CollectionRef NewCollection() =>
        new(DatabaseName.Create("app"), CollectionName.Create("users"));
}
