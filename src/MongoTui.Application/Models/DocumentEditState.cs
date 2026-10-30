using MongoTui.Domain.Primitives;
using MongoTui.Application.Localization;
using MongoDB.Bson;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Models;

/// <summary>Owns one document editor, its original identity and its write eligibility.</summary>
public sealed class DocumentEditState
{
    private readonly DocumentService _documents;
    private readonly MongoConnectionTarget _target;
    private readonly CollectionRef _collection;
    private bool _isNew;
    private string? _idFilter;
    private readonly BsonDocument? _selectedDocument;

    public DocumentEditState(
        DocumentService documents,
        MongoConnectionTarget target,
        CollectionRef collection,
        BsonDocument? existingDocument = null)
    {
        _documents = documents;
        _target = target;
        _collection = collection;

        _selectedDocument = (BsonDocument?)existingDocument?.DeepClone();
        _isNew = existingDocument is null;
        _idFilter = existingDocument?.Contains(MongoSystemNames.IdField) == true
            ? BsonTextCodec.Format(BsonTextCodec.IdFilter(existingDocument), false) : null;
        Filter = _idFilter ?? "{}";
        Json = BsonTextCodec.Format(existingDocument ?? new BsonDocument());
        CanWrite = existingDocument is null;
    }

    /// <summary>Whether the dialog inserts a new document rather than editing an existing one.</summary>
    public bool IsNew => _isNew;

    public bool CanWrite { get; private set; }

    public CollectionRef Collection => _collection;

    /// <summary>Reloads an existing selection before it can be saved, including shell results.</summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (_selectedDocument is null) return true;
        ValidationError = null;
        ValidationErrorKey = null;
        CanWrite = false;
        try
        {
            var content = await _documents.LoadForEditAsync(_target, _collection, _selectedDocument,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (content is null) return NotFound();
            Json = content.Json;
            Filter = _idFilter = content.IdFilter;
            CanWrite = true;
            _isNew = false;
            return true;
        }
        catch (DomainException exception) { return Fail(exception); }
    }

    /// <summary>Resource key for an expected identity or missing-document error.</summary>
    public string? ValidationErrorKey { get; private set; }

    /// <summary>Find filter used to load the document being edited.</summary>
    public string Filter { get; set; }

    /// <summary>Editable document JSON.</summary>
    public string Json { get; set; }

    public string? ValidationError { get; private set; }

    /// <summary>Loads the first document matching <see cref="Filter"/> into the editor.</summary>
    /// <returns><see langword="true"/> when a document was found; otherwise <see langword="false"/>.</returns>
    public async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        ValidationError = null;
        ValidationErrorKey = null;
        CanWrite = false;

        try
        {
            var filter = Filter;
            var document = await _documents.FindOneAsync(_target, _collection, filter, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (document is null)
            {
                return NotFound();
            }

            _idFilter = DocumentService.BuildIdFilter(document);
            Json = document;
            _isNew = false;
            CanWrite = true;

            return true;
        }
        catch (DomainException exception)
        {
            return Fail(exception);
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanWrite) return false;
        ValidationError = null;
        ValidationErrorKey = null;

        try
        {
            if (IsNew)
            {
                await _documents.InsertAsync(_target, _collection, Json, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var matched = await _documents.ReplaceAsync(_target, _collection, _idFilter!, Json, cancellationToken).ConfigureAwait(false);
                if (matched == 0) return NotFound();
            }

            return true;
        }
        catch (DomainException exception)
        {
            return Fail(exception);
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanWrite || IsNew) return false;
        ValidationError = null;
        ValidationErrorKey = null;

        try
        {
            DocumentService.EnsureIdentity(_idFilter!, Json);
            var deleted = await _documents.DeleteAsync(_target, _collection, _idFilter!, cancellationToken).ConfigureAwait(false);
            if (deleted == 0) return NotFound();

            return true;
        }
        catch (DomainException exception)
        {
            return Fail(exception);
        }
    }

    private bool Fail(DomainException exception)
    {
        ValidationError = exception.Message;
        ValidationErrorKey = (exception as DocumentEditException)?.ErrorKey;
        return false;
    }

    private bool NotFound()
    {
        CanWrite = false;
        ValidationErrorKey = LocalizationKeys.Documents.NotFound;
        return false;
    }
}
