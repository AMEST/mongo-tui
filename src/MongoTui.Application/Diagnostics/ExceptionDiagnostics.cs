using System.Diagnostics;

namespace MongoTui.Application.Diagnostics;

/// <summary>Formats types and stack frames without exception messages, data or server responses.</summary>
public static class ExceptionDiagnostics
{
    private const int MaximumCauses = 16;
    public static string Describe(Exception exception)
    {
        var pending = new Queue<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var details = new List<string>();
        pending.Enqueue(exception);
        while (pending.Count > 0 && visited.Count < MaximumCauses)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current)) continue;
            details.Add(current.GetType().Name + Environment.NewLine + new StackTrace(current, true));
            if (current is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions) pending.Enqueue(inner);
            else if (current.InnerException is { } inner) pending.Enqueue(inner);
        }
        return string.Join(Environment.NewLine, details);
    }
}
