using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Infrastructure.Mongo.Driver;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Infrastructure.Tests.Diagnostics;

public sealed class MongoProfilerReaderTests
{
    [Fact]
    public void Should_ProjectOnlySafeFields_WhenReadingProfiler()
    {
        var projection = MongoProfilerReader.Projection();

        projection.Names.Should().NotContain(new[] { "command", "query", "originatingCommand", "user", "client" });
        projection["_id"].AsInt32.Should().Be(0);
    }
    [Theory]
    [InlineData("unknown", "shop", QueryOperationKind.Other)]
    [InlineData("command", "shop.orders", QueryOperationKind.Command)]
    [InlineData("query", "shop.orders", QueryOperationKind.Find)]
    public void Should_MapWithoutReadingCommandOrPrivatePlanText_WhenProfilerRowArrives(string operation, string ns, QueryOperationKind expected)
    {
        var row = new BsonDocument { { "ts", DateTime.UtcNow }, { "op", operation }, { "ns", ns }, { "millis", 200 },
            { "planSummary", "IXSCAN private-filter" }, { "command", new BsonDocument("secret", "private-value") } };

        var result = MongoProfilerReader.Map(row);

        result.Operation.Should().Be(expected);
        result.Database.Should().Be("shop");
        result.PlanSummary.Should().Be("IXSCAN");
        result.ToString().Should().NotContain("private");
    }
}
