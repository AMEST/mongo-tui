using MongoTui.Application.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private void InsertDocument() => Safe(InsertDocumentAsync);

    private async Task InsertDocumentAsync()
    {
        if (ResolveContext() is not { } context || _queryTabs.ActiveTab is not { } tab)
        {
            return;
        }

        var viewModel = new DocumentEditViewModel(_documents, context.Target, context.Collection);
        using var dialog = new DocumentEditDialog(_localizer, Application!, viewModel);
        Application!.Run(dialog);

        SetStatus(dialog.Saved ? _localizer.Get(LocalizationKeys.Documents.Saved) : null);
        if (dialog.Saved) await RefreshDocumentResultsAsync(tab).ConfigureAwait(false);
    }

    private void EditDocument() => Safe(EditDocumentAsync);

    private async Task EditDocumentAsync()
    {
        if (_openingDocument || ResolveResultContext() is not { } context) return;
        var document = _queryTabs.SelectedDocument;
        if (document is null)
        {
            SetStatus(_localizer.Get(LocalizationKeys.Documents.NoDocument));
            return;
        }
        _openingDocument = true;
        var tab = _queryTabs.ActiveTab!;
        var changed = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_documentLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var viewModel = new DocumentEditViewModel(_documents, context.Target, context.Collection, document);
            var loaded = await viewModel.InitializeAsync(timeout.Token).ConfigureAwait(false);
            if (_documentLifetime.IsCancellationRequested) return;
            await Application!.InvokeAsync(() =>
            {
                if (_documentLifetime.IsCancellationRequested || _queryTabs.ActiveTab != tab) return;
                if (!loaded)
                {
                    SetStatus(viewModel.ValidationErrorKey is { } key ? _localizer.Get(key) : viewModel.ValidationError);
                    return;
                }
                using var dialog = new DocumentEditDialog(_localizer, Application!, viewModel);
                Application!.Run(dialog);
                changed = dialog.Deleted || dialog.Saved;
                if (dialog.Deleted) SetStatus(_localizer.Get(LocalizationKeys.Documents.Deleted));
                else if (dialog.Saved) SetStatus(_localizer.Get(LocalizationKeys.Documents.Saved));
            }).ConfigureAwait(false);
            if (changed) await RefreshDocumentResultsAsync(tab).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_documentLifetime.IsCancellationRequested) { }
        finally { _openingDocument = false; }
    }

    private void DeleteDocument()
    {
        if (ResolveResultContext() is not { } context || _queryTabs.ActiveTab is not { } tab)
        {
            return;
        }

        if (_queryTabs.SelectedDocument is not { } document)
        {
            SetStatus(_localizer.Get(LocalizationKeys.Documents.NoDocument));
            return;
        }

        var filter = BsonTextCodec.ForEditing(BsonTextCodec.IdFilter(document));
        var confirm = new ConfirmDialog(
            _localizer,
            Application!,
            _localizer.Get(LocalizationKeys.Menu.DocumentDelete),
            _localizer.Format(LocalizationKeys.Documents.DeleteCondition, filter));
        Application!.Run(confirm);

        if (confirm.Confirmed)
        {
            Safe(() => DeleteDocumentAsync(tab, context, filter));
        }
    }

    private async Task DeleteDocumentAsync(QueryTabViewModel tab,
        (MongoConnectionTarget Target, CollectionRef Collection) context, string filter)
    {
        try
        {
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(_documentLifetime.Token);
            operation.CancelAfter(TimeSpan.FromSeconds(30));
            var deleted = await _documents.DeleteAsync(context.Target, context.Collection, filter, operation.Token).ConfigureAwait(false);
            if (_documentLifetime.IsCancellationRequested) return;
            await Application!.InvokeAsync(() =>
            {
                if (!_documentLifetime.IsCancellationRequested)
                    SetStatus(_localizer.Get(deleted > 0 ? LocalizationKeys.Documents.Deleted : LocalizationKeys.Documents.NotFound));
            }).ConfigureAwait(false);
            if (deleted > 0) await RefreshDocumentResultsAsync(tab).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_documentLifetime.IsCancellationRequested) { }
        catch (DomainException exception)
        {
            if (_documentLifetime.IsCancellationRequested) return;
            await Application!.InvokeAsync(() =>
            {
                if (!_documentLifetime.IsCancellationRequested) SetStatus(exception.Message);
            }).ConfigureAwait(false);
        }
    }

    private async Task RefreshDocumentResultsAsync(QueryTabViewModel tab)
    {
        if (_documentLifetime.IsCancellationRequested) return;
        Task refresh = Task.CompletedTask;
        await Application!.InvokeAsync(() =>
        {
            if (!_documentLifetime.IsCancellationRequested) refresh = _queryTabs.RefreshAsync(Application!, tab);
        }).ConfigureAwait(false);
        await refresh.ConfigureAwait(false);
    }

    private (MongoConnectionTarget Target, CollectionRef Collection)? ResolveResultContext()
    {
        if (_queryTabs.ActiveTab is { IsExecuting: false, ResultCollection: { } collection } tab)
            return (tab.Target, collection);
        SetStatus(_localizer.Get(LocalizationKeys.Documents.UnsafeResult));
        return null;
    }

    private (MongoConnectionTarget Target, CollectionRef Collection)? ResolveContext()
    {
        var tab = _queryTabs.ActiveTab;
        if (tab?.Database is null || tab.Collection is null)
        {
            SetStatus(_localizer.Get(LocalizationKeys.Documents.NoContext));
            return null;
        }

        var collection = new CollectionRef(DatabaseName.Create(tab.Database), CollectionName.Create(tab.Collection));

        return (tab.Target, collection);
    }

}
