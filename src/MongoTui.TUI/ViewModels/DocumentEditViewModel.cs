using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.TUI.ViewModels;

/// <summary>Backs the document insert/edit dialog.</summary>
public sealed class DocumentEditViewModel : ObservableObject
{
    private readonly DocumentService _documents;
    private readonly MongoConnectionTarget _target;
    private readonly CollectionRef _collection;
    private string _json;
    private string? _error;

    public DocumentEditViewModel(
        DocumentService documents,
        MongoConnectionTarget target,
        CollectionRef collection,
        string? existingJson = null)
    {
        _documents = documents;
        _target = target;
        _collection = collection;

        IsNew = existingJson is null;
        _json = existingJson ?? "{ \"_id\": 1 }";
    }

    public bool IsNew { get; }

    public string Json
    {
        get => _json;
        set => SetProperty(ref _json, value);
    }

    public string? ValidationError
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;

        try
        {
            if (IsNew)
            {
                await _documents.InsertAsync(_target, _collection, Json, cancellationToken);
            }
            else
            {
                var filter = DocumentService.BuildIdFilter(Json);
                await _documents.ReplaceAsync(_target, _collection, filter, Json, cancellationToken);
            }

            return true;
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;
            return false;
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;

        try
        {
            var filter = DocumentService.BuildIdFilter(Json);
            await _documents.DeleteAsync(_target, _collection, filter, cancellationToken);

            return true;
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;
            return false;
        }
    }
}
