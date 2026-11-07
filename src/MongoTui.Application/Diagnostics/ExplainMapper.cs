using MongoDB.Bson;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Diagnostics;

public static class ExplainMapper
{
    public static ExplainResult Map(BsonDocument raw, ExplainRequest request, int slowThreshold)
    {
        var stats = Statistics(raw);
        var planner = raw.GetValue("queryPlanner", new BsonDocument()).AsBsonDocument;
        var source = raw.Contains("stages") ? raw : stats.GetValue("executionStages", planner.GetValue("winningPlan", raw));
        var plan = Node(source, "PLAN", 0);
        var nodes = Flatten(plan).ToArray();
        var warnings = new List<ExplainWarning>();
        Add(nodes.Length == 1 && nodes[0].Stage == "PLAN", ExplainWarningKind.UnknownPlan);
        Add(nodes.Any(n => n.Stage == "COLLSCAN"), ExplainWarningKind.CollectionScan);
        Add(nodes.Any(n => n.Stage is "SORT" or "$sort"), ExplainWarningKind.InMemorySort);
        Add(nodes.Any(n => n.Stage is "$group" or "$sort" or "$lookup" or "$facet"), ExplainWarningKind.BlockingStage);
        Add(nodes.Any(n => n.UsedDisk == true || n.Spills > 0), ExplainWarningKind.UsedDisk);
        Add(HasRejected(raw), ExplainWarningKind.RejectedPlans);
        var returned = Number(stats, "nReturned"); var docs = Number(stats, "totalDocsExamined"); var keys = Number(stats, "totalKeysExamined");
        Add(returned is not null && docs > Math.Max(100d, returned.Value * 10d), ExplainWarningKind.HighDocumentsExaminedRatio);
        Add(returned is not null && keys > Math.Max(100d, returned.Value * 10d), ExplainWarningKind.HighKeysExaminedRatio);
        Add(Number(stats, "executionTimeMillis") > slowThreshold, ExplainWarningKind.HighLatency);
        return new ExplainResult { Verbosity = request.Verbosity, Namespace = request.Collection.Database.Value + "." + request.Collection.Collection.Value,
            Plan = plan, ExecutionTimeMs = Number(stats, "executionTimeMillis"), DocumentsExamined = docs, KeysExamined = keys,
            Returned = returned, Warnings = warnings, Raw = raw };
        void Add(bool condition, ExplainWarningKind kind) { if (condition) warnings.Add(new(kind, null)); }
    }

    private static BsonDocument Statistics(BsonDocument raw)
    {
        if (raw.GetValue("executionStats", BsonNull.Value) is BsonDocument stats) return stats;
        if (raw.GetValue("stages", BsonNull.Value) is not BsonArray stages) return new();
        var cursor = stages.OfType<BsonDocument>().Select(stage => stage.GetValue("$cursor", BsonNull.Value)).OfType<BsonDocument>().FirstOrDefault();
        if (cursor?.GetValue("executionStats", BsonNull.Value) is not BsonDocument cursorStats) return new();
        var result = (BsonDocument)cursorStats.DeepClone();
        if (stages.LastOrDefault() is BsonDocument last && !last.Contains("$cursor"))
        {
            result["nReturned"] = last.GetValue("nReturned", BsonNull.Value);
            result.Remove("executionTimeMillis");
        }
        return result;
    }

    private static ExplainNode Node(BsonValue value, string fallback, int depth)
    {
        if (depth > 64 || value is not BsonDocument document) return new ExplainNode { Stage = fallback };
        var children = new List<ExplainNode>();
        foreach (var element in document)
        {
            if (element.Name is "command" or "filter" or "parsedQuery" or "indexBounds" or "rejectedPlans" or "slotBasedPlan") continue;
            if (element.Value is BsonDocument child && (IsPlanField(element.Name) || element.Name.StartsWith('$') || fallback == "shards"))
                children.Add(Node(child, element.Name, depth + 1));
            if (element.Value is BsonArray array && IsPlanField(element.Name))
                children.AddRange(array.OfType<BsonDocument>().Select(child => Node(child, element.Name, depth + 1)));
        }
        return new ExplainNode { Stage = document.GetValue("stage", fallback).ToString() ?? fallback,
            IndexName = document.TryGetValue("indexName", out var index) && index.IsString ? index.AsString : null,
            ExecutionTimeMs = Number(document, "executionTimeMillisEstimate") ?? Number(document, "executionTimeMillis"),
            DocumentsExamined = Number(document, "docsExamined"), KeysExamined = Number(document, "keysExamined"),
            Returned = Number(document, "nReturned"), UsedDisk = document.TryGetValue("usedDisk", out var disk) && disk.IsBoolean ? disk.AsBoolean : null,
            Spills = Number(document, "spills"), Children = children };
    }
    private static bool IsPlanField(string name) => name is "queryPlan" or "winningPlan" or "executionStages" or "queryPlanner"
        or "executionStats" or "inputStage" or "inputStages" or "outerStage" or "innerStage" or "thenStage" or "elseStage"
        or "children" or "shards" or "stages";
    private static bool HasRejected(BsonValue value) => value is BsonDocument doc
        ? doc.Elements.Any(e => e.Name == "rejectedPlans" && e.Value is BsonArray { Count: > 0 } || HasRejected(e.Value))
        : value is BsonArray array && array.Any(HasRejected);
    public static IEnumerable<ExplainNode> Flatten(ExplainNode node) => new[] { node }.Concat(node.Children.SelectMany(Flatten));
    private static long? Number(BsonDocument doc, string name) => doc.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt64() : null;
}
