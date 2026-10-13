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
        _filter = existingJson is null ? "{}" : DocumentService.BuildIdFilter(existingJson);
        _json = Indent(existingJson ?? "{ \"_id\": 1 }");
    }

    /// <summary>Whether the dialog inserts a new document rather than editing an existing one.</summary>
    public bool IsNew { get; }

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

        try
        {
            var document = await _documents.FindOneAsync(_target, _collection, Filter, cancellationToken);

            if (document is null)
            {
                return false;
            }

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
