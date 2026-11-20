using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Abstractions.Mongo;

public interface IMongoDiagnosticsGateway
{
    /// <summary>Restores the database level and filter. A null options argument leaves server-wide settings in place.</summary>
    Task RestoreProfilerAsync(MongoConnectionTarget target, ProfilerState original, ProfilerOptions? globalOptions,
        CancellationToken cancellationToken);
    Task<ProfilerState> GetProfilerStateAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken);
    Task<ProfilerState> SetProfilerLevelAsync(MongoConnectionTarget target, string database, ProfilingLevel level,
        ProfilerOptions options, ProfilerFilterAction filterAction, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfilerEntry>> ReadProfilerAsync(MongoConnectionTarget target, string database,
        ProfilerReadFilter filter, int limit, CancellationToken cancellationToken);
    Task<ServerOverview> GetServerStatusAsync(MongoConnectionTarget target, CancellationToken cancellationToken);
    Task<DatabaseOverview> GetDatabaseOverviewAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken);
    Task<BsonDocument> ExplainAsync(MongoConnectionTarget target, ExplainRequest request, CancellationToken cancellationToken);
}
