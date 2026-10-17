using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.TUI.ViewModels;

/// <summary>Backs the document insert/edit dialog.</summary>
public sealed class DocumentEditViewModel : ObservableObject
{
    private static readonly JsonWriterSettings IndentedJson = new() { Indent = true };

    private readonly DocumentService _documents;
    private readonly MongoConnectionTarget _target;
    private readonly CollectionRef _collection;
    private string _filter;
    private string _json;
    private string? _error;
    private string? _idFilter;

    public DocumentEditViewModel(
        DocumentService documents,
        MongoConnectionTarget target,
        CollectionRef collection,
        string? existingJson = null)
    {
        _documents = documents;
        _target = target;
        _collection = collection;

        _idFilter = existingJson is null ? null : DocumentService.BuildIdFilter(existingJson);
        _filter = _idFilter ?? "{}";
        _json = Indent(existingJson ?? "{ \"_id\": 1 }");
    }

    /// <summary>Whether the dialog inserts a new document rather than editing an existing one.</summary>
    public bool IsNew => _idFilter is null;

    /// <summary>Resource key for an expected identity or missing-document error.</summary>
    public string? ValidationErrorKey { get; private set; }

    /// <summary>Find filter used to load the document being edited.</summary>
    public string Filter
    {
        get => _filter;
        set => SetProperty(ref _filter, value);
    }

    /// <summary>Editable document JSON.</summary>
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

    /// <summary>Loads the first document matching <see cref="Filter"/> into the editor.</summary>
    /// <returns><see langword="true"/> when a document was found; otherwise <see langword="false"/>.</returns>
    public async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;
        ValidationErrorKey = null;

        try
        {
            var document = await _documents.FindOneAsync(_target, _collection, Filter, cancellationToken);

            if (document is null)
            {
                return false;
            }

            _idFilter = DocumentService.BuildIdFilter(document);
            Json = document;

            return true;
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;

            return false;
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;
        ValidationErrorKey = null;

        try
        {
            if (IsNew)
            {
                await _documents.InsertAsync(_target, _collection, Json, cancellationToken);
            }
            else
            {
                if (!ValidateIdentity()) return false;
                var matched = await _documents.ReplaceAsync(_target, _collection, _idFilter!, Json, cancellationToken);
                if (matched == 0) return NotFound();
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
        ValidationErrorKey = null;

        try
        {
            if (!ValidateIdentity()) return false;
            var deleted = await _documents.DeleteAsync(_target, _collection, _idFilter!, cancellationToken);
            if (deleted == 0) return NotFound();

            return true;
        }
        catch (DomainException exception)
        {
            ValidationError = exception.Message;
            return false;
        }
    }

    private bool ValidateIdentity()
    {
        if (_idFilter is not null && DocumentService.BuildIdFilter(Json) == _idFilter) return true;
        ValidationErrorKey = "Documents.IdChanged";
        return false;
    }

    private bool NotFound()
    {
        ValidationErrorKey = "Documents.NotFound";
        return false;
    }

    private static string Indent(string json)
    {
        try
        {
            return BsonDocument.Parse(json).ToJson(IndentedJson);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return json;
        }
    }
}
