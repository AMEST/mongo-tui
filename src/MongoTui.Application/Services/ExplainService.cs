using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Diagnostics;
using MongoTui.Application.Models;
using MongoTui.Application.Queries;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

public sealed class ExplainService(IMongoDiagnosticsGateway gateway, SettingsService settings, DiagnosticsSettingsService diagnostics)
{
    public ExplainRequest? TryCreateRequest(QueryExecutionContext context, ExplainVerbosity verbosity) => ExplainQuerySource.TryCreate(context, verbosity);
    public async Task<ExplainResult> ExplainAsync(MongoConnectionTarget target, ExplainRequest request, CancellationToken cancellationToken)
    {
        var seconds = await settings.GetQueryTimeoutSecondsAsync(cancellationToken).ConfigureAwait(false);
        var timeout = TimeSpan.FromSeconds(seconds == 0 ? 30 : seconds);
        if (request.MaxTime is { } requested && requested > TimeSpan.Zero && requested < timeout) timeout = requested;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            var raw = await gateway.ExplainAsync(target, request with { MaxTime = timeout }, deadline.Token).ConfigureAwait(false);
            return ExplainMapper.Map(raw, request, diagnostics.Current.SlowThresholdMs);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        { throw new QueryTimeoutException(timeout, exception); }
    }
}
