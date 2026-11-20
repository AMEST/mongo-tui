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

    [Fact]
    public void Should_SendCollectionOperationAndSlowFilters_WhenReadingProfiler()
    {
        var since = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var filter = new ProfilerReadFilter(since, TimeSpan.FromMilliseconds(12.5), "events.archive", QueryOperationKind.Find, 20);

        var query = MongoProfilerReader.Filter("shop", filter);

        query["ns"].AsString.Should().Be("shop.events.archive");
        query["op"]["$in"].AsBsonArray.Select(value => value.AsString).Should().BeEquivalentTo(["query", "getmore"]);
        query["millis"]["$gte"].AsDouble.Should().Be(12.5);
        query["millis"]["$gt"].AsInt32.Should().Be(20);
        query["ts"]["$gte"].ToUniversalTime().Should().Be(since.UtcDateTime);
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
