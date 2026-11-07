using AwesomeAssertions;
using MongoDB.Bson;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Queries;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Tests.Diagnostics;

public sealed class ExplainMapperTests
{
    [Fact]
    public void Should_ReturnMetricsAndWarnings_WhenExecutionIncludesScanSortAndSpill()
    {
        var request = ExplainQuerySource.TryCreate(ExplainQuerySourceTests.Context("db.orders.find({})"), ExplainVerbosity.ExecutionStats)!;
        var raw = BsonDocument.Parse("{queryPlanner:{rejectedPlans:[{stage:'IXSCAN'}]}, executionStats:{nReturned:1,totalDocsExamined:500,totalKeysExamined:200,executionTimeMillis:150,executionStages:{stage:'SORT',usedDisk:true,spills:1,inputStage:{stage:'COLLSCAN'}}}}");

        var result = ExplainMapper.Map(raw, request, 100);

        result.DocumentsExamined.Should().Be(500);
        result.Warnings.Select(warning => warning.Kind).Should().Contain(new[] { ExplainWarningKind.CollectionScan,
            ExplainWarningKind.InMemorySort, ExplainWarningKind.UsedDisk, ExplainWarningKind.RejectedPlans,
            ExplainWarningKind.HighDocumentsExaminedRatio, ExplainWarningKind.HighLatency });
        result.Raw.Should().BeSameAs(raw);
    }
    [Theory]
    [InlineData("{queryPlanner:{winningPlan:{queryPlan:{stage:'IXSCAN',indexName:'x_1'},slotBasedPlan:{stages:'private'}}}}", "IXSCAN")]
    [InlineData("{stages:[{$cursor:{queryPlanner:{winningPlan:{stage:'COLLSCAN'}}}},{$sort:{sortKey:{x:1}},usedDisk:true}]}", "COLLSCAN")]
    [InlineData("{shards:{one:{queryPlanner:{winningPlan:{stage:'IXSCAN'}}},two:{queryPlanner:{winningPlan:{stage:'COLLSCAN'}}}}}", "COLLSCAN")]
    public void Should_TraverseModernPlanShapes_WhenExecutionMetricsAreAbsent(string json, string stage)
    {
        var request = ExplainQuerySource.TryCreate(ExplainQuerySourceTests.Context("db.orders.find({})"), ExplainVerbosity.QueryPlanner)!;

        var result = ExplainMapper.Map(BsonDocument.Parse(json), request, 100);

        ExplainMapper.Flatten(result.Plan).Should().Contain(node => node.Stage == stage);
        result.ExecutionTimeMs.Should().BeNull();
        result.Returned.Should().BeNull();
    }
}
