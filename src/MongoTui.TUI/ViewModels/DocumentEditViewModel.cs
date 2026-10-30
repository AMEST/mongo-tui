using MongoDB.Bson;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;

namespace MongoTui.TUI.ViewModels;

/// <summary>Adapts the shared editor state to the terminal dialog.</summary>
public sealed class DocumentEditViewModel : ObservableObject
{
    private readonly DocumentEditState _state;

    public DocumentEditViewModel(DocumentService documents, MongoConnectionTarget target,
        CollectionRef collection, BsonDocument? existingDocument = null) =>
        _state = new DocumentEditState(documents, target, collection, existingDocument);

    public bool IsNew => _state.IsNew;
    public bool CanWrite => _state.CanWrite;
    public string? ValidationError => _state.ValidationError;
    public string? ValidationErrorKey => _state.ValidationErrorKey;

    public string Filter
    {
        get => _state.Filter;
        set { _state.Filter = value; OnPropertyChanged(); }
    }

    public string Json
    {
        get => _state.Json;
        set { _state.Json = value; OnPropertyChanged(); }
    }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken) => RunAsync(_state.InitializeAsync, cancellationToken);
    public Task<bool> LoadAsync(CancellationToken cancellationToken) => RunAsync(_state.LoadAsync, cancellationToken);
    public Task<bool> SaveAsync(CancellationToken cancellationToken) => RunAsync(_state.SaveAsync, cancellationToken);
    public Task<bool> DeleteAsync(CancellationToken cancellationToken) => RunAsync(_state.DeleteAsync, cancellationToken);

    private async Task<bool> RunAsync(Func<CancellationToken, Task<bool>> action, CancellationToken cancellationToken)
    {
        try { return await action(cancellationToken).ConfigureAwait(false); }
        finally
        {
            OnPropertyChanged(nameof(Json));
            OnPropertyChanged(nameof(Filter));
            OnPropertyChanged(nameof(CanWrite));
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(ValidationError));
            OnPropertyChanged(nameof(ValidationErrorKey));
        }
    }
}
