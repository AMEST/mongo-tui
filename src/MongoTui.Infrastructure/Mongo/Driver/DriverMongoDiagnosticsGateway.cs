using static MongoTui.Infrastructure.Mongo.Driver.MongoWireNames.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Infrastructure.Mongo.Driver;

public sealed partial class DriverMongoDiagnosticsGateway(DriverMongoGateway driver) : IMongoDiagnosticsGateway
{
    private IMongoDatabase Database(MongoConnectionTarget target, string name) => driver.GetClient(target).GetDatabase(name);

    private async Task<BsonDocument> CommandAsync(MongoConnectionTarget target, string database,
        BsonDocument command, CancellationToken cancellationToken)
    {
        try
        {
            return await Database(target, database).RunCommandAsync<BsonDocument>(command,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoExecutionTimeoutException) { throw new DiagnosticsException("Query.TimedOut"); }
        catch (MongoCommandException exception)
        {
            throw new DiagnosticsException(exception.Code == 13 ? "Diagnostics.Error.Unauthorized"
                : command.Contains(WireProfile) && exception.Code is 20 or 59 or 115 ? "Diagnostics.ProfilerUnavailableMongos" : "Diagnostics.Error.Command", exception.Code);
        }
    }

    public async Task RestoreProfilerAsync(MongoConnectionTarget target, ProfilerState original, CancellationToken cancellationToken)
    {
        var command = new BsonDocument { { WireProfile, (int)original.Level }, { WireSlowms, original.Options.SlowMs },
            { WireSampleRate, original.Options.SampleRate }, { WireFilter, original.Options.FilterJson is { } filter
                ? BsonTextCodec.ParseDocument(filter) : (BsonValue)WireUnset } };
        await CommandAsync(target, original.Database, command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProfilerState> GetProfilerStateAsync(MongoConnectionTarget target, string database, CancellationToken cancellationToken)
    {
        var result = await CommandAsync(target, database, new BsonDocument(WireProfile, -1), cancellationToken).ConfigureAwait(false);
        return new ProfilerState(database, (ProfilingLevel)(Number(result, WireWas) ?? 0),
            new ProfilerOptions { SlowMs = (int)(Number(result, WireSlowms) ?? 100),
                SampleRate = result.GetValue(WireSampleRate, 1.0).ToDouble(),
                FilterJson = result.TryGetValue(WireFilter, out var filter) && filter.IsBsonDocument ? filter.ToJson() : null },
            null, true, null);
    }

    public async Task<ProfilerState> SetProfilerLevelAsync(MongoConnectionTarget target, string database, ProfilingLevel level,
        ProfilerOptions options, ProfilerFilterAction filterAction, CancellationToken cancellationToken)
    {
        var command = new BsonDocument(WireProfile, (int)level);
        if (level != ProfilingLevel.Off)
        {
            command[WireSlowms] = options.SlowMs;
            command[WireSampleRate] = options.SampleRate;
            if (filterAction == ProfilerFilterAction.Replace) command[WireFilter] = BsonTextCodec.ParseDocument(options.FilterJson!);
            if (filterAction == ProfilerFilterAction.Clear) command[WireFilter] = WireUnset;
        }
        await CommandAsync(target, database, command, cancellationToken).ConfigureAwait(false);
        return await GetProfilerStateAsync(target, database, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProfilerEntry>> ReadProfilerAsync(MongoConnectionTarget target, string database,
        DateTimeOffset? since, TimeSpan minimumDuration, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 10000);
        var filter = new BsonDocument(WireMillis, new BsonDocument(WireDollargte, minimumDuration.TotalMilliseconds));
        if (since is { } timestamp) filter[WireTs] = new BsonDocument(WireDollargte, timestamp.UtcDateTime);
        try
        {
            var rows = await Database(target, database).GetCollection<BsonDocument>(WireSystemProfile).Find(filter)
                .Project(MongoProfilerReader.Projection()).Sort(new BsonDocument(WireTs, -1)).Limit(limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            return rows.Select(MongoProfilerReader.Map).ToArray();
        }
        catch (MongoException exception) { throw new DiagnosticsException(exception is MongoCommandException { Code: 13 }
            ? "Diagnostics.Error.Unauthorized" : "Diagnostics.Error.Command"); }
    }

    public Task<BsonDocument> ExplainAsync(MongoConnectionTarget target, ExplainRequest request, CancellationToken cancellationToken)
    {
        var command = MongoExplainCommand.Build(request);
        return CommandAsync(target, request.Collection.Database.Value, command, cancellationToken);
    }

    internal static long? Number(BsonDocument document, string key) =>
        document.TryGetValue(key, out var value) && value.IsNumeric ? value.ToInt64() : null;
    private static BsonDocument Doc(BsonDocument document, string key) =>
        document.GetValue(key, new BsonDocument()) as BsonDocument ?? new BsonDocument();
    private static string? Text(BsonDocument document, string key) =>
        document.TryGetValue(key, out var value) && value.IsString ? value.AsString : null;
}
