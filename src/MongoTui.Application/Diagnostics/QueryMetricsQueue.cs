using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Diagnostics;

/// <summary>Bounded pending persistence queue. Overflow drops the oldest observation.</summary>
public sealed class QueryMetricsQueue
{
    private readonly Lock _gate = new();
    private readonly Queue<QueryObservation> _queue = new();
    private long _lost;
    public long Lost => Interlocked.Read(ref _lost);
    public void Enqueue(QueryObservation observation)
    {
        lock (_gate)
        {
            if (_queue.Count == 4096) { _queue.Dequeue(); Interlocked.Increment(ref _lost); }
            _queue.Enqueue(observation);
        }
    }
    public IReadOnlyList<QueryObservation> Drain()
    {
        lock (_gate) { var batch = _queue.ToArray(); _queue.Clear(); return batch; }
    }
    public void ReportLost(int count) => Interlocked.Add(ref _lost, count);
}
