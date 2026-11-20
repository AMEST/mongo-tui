namespace MongoTui.Domain.Diagnostics;

public enum ExplainWarningKind
{
    CollectionScan, HighDocumentsExaminedRatio, HighKeysExaminedRatio,
    InMemorySort, BlockingStage, RejectedPlans, HighLatency, UsedDisk, UnknownPlan,
}

