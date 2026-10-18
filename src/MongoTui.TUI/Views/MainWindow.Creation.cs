using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using Serilog;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private Task CreateDatabaseAsync(ActiveConnection connection)
    {
        if (Application is null) return Task.CompletedTask;
        using var dialog = new CreateNameDialog(_localizer, Application, _localizer.Get("Create.DatabaseTitle"), name =>
        {
            _tree.AddDatabase(connection.Profile.Id, DatabaseName.Create(name));
            return Task.CompletedTask;
        });
        Application.Run(dialog);
        if (dialog.CreatedName is { } name)
        {
            RebuildTree();
            _treeList.SelectedItem = _rows.FindIndex(row => row.Owner.Profile.Id == connection.Profile.Id
                && row.Kind == TreeRowKind.Database && row.Database == name);
            SetStatus(_localizer.Format("Status.DatabaseCreated", name));
            Log.Information("Database added to connection tree");
        }
        return Task.CompletedTask;
    }

    private async Task CreateCollectionAsync(ActiveConnection connection, string database)
    {
        if (Application is null) return;
        using var dialog = new CreateNameDialog(_localizer, Application, _localizer.Get("Create.CollectionTitle"), name =>
            _databases.CreateCollectionAsync(connection.Target,
                new CollectionRef(DatabaseName.Create(database), CollectionName.Create(name)), CancellationToken.None));
        Application.Run(dialog);
        if (dialog.CreatedName is not { } name) return;
        await _tree.ReloadCollectionsAsync(connection.Profile.Id, connection.Target, database, CancellationToken.None);
        if (!_tree.IsExpanded(connection.Profile.Id, database))
        {
            await _tree.ToggleDatabaseAsync(connection.Profile.Id, connection.Target, database, CancellationToken.None);
        }
        RebuildTree();
        _treeList.SelectedItem = _rows.FindIndex(row => row.Owner.Profile.Id == connection.Profile.Id
            && row.Kind == TreeRowKind.Collection && row.Database == database && row.Collection == name);
        SetStatus(_localizer.Format("Status.CollectionCreated", $"{database}.{name}"));
        Log.Information("Collection created");
    }
}
