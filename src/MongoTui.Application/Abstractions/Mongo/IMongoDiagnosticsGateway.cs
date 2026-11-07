using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Abstractions.Mongo;

public interface IMongoDiagnosticsGateway
{
    Task RestoreProfilerAsync(MongoConnectionTarget target, ProfilerState original, CancellationToken cancellationToken);
    Task<ProfilerState> GetProfilerStateAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken);
    Task<ProfilerState> SetProfilerLevelAsync(MongoConnectionTarget target, string database, ProfilingLevel level,
        ProfilerOptions options, ProfilerFilterAction filterAction, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfilerEntry>> ReadProfilerAsync(MongoConnectionTarget target, string database,
        DateTimeOffset? since, TimeSpan minimumDuration, int limit, CancellationToken cancellationToken);
    Task<ServerOverview> GetServerStatusAsync(MongoConnectionTarget target, CancellationToken cancellationToken);
    Task<DatabaseOverview> GetDatabaseOverviewAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken);
    Task<BsonDocument> ExplainAsync(MongoConnectionTarget target, ExplainRequest request, CancellationToken cancellationToken);
}
