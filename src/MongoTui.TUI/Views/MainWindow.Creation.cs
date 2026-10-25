using Microsoft.Extensions.Logging;
using MongoTui.Application.Localization;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private async Task CreateDatabaseAsync(ActiveConnection connection)
    {
        if (Application is null) return;
        using var dialog = new CreateDatabaseDialog(_localizer, Application, collection =>
            _databases.CreateDatabaseAsync(connection.Target, collection, CancellationToken.None));
        Application.Run(dialog);
        if (dialog.CreatedCollection is not { } created) return;
        await RefreshConnectionAsync(connection);
        var row = await RevealCollectionAsync(connection, created.Database.Value, created.Collection.Value);
        if (row is not null) OpenCollectionTab(row);
        SetStatus(_localizer.Format(LocalizationKeys.Status.DatabaseCreated, created.Database.Value));
        _logger.LogInformation("Database created");
    }

    private async Task CreateCollectionAsync(ActiveConnection connection, string database)
    {
        if (Application is null) return;
        using var dialog = new CreateNameDialog(_localizer, Application, _localizer.Get(LocalizationKeys.Create.CollectionTitle), name =>
            _databases.CreateCollectionAsync(connection.Target,
                new CollectionRef(DatabaseName.Create(database), CollectionName.Create(name)), CancellationToken.None));
        Application.Run(dialog);
        if (dialog.CreatedName is not { } name) return;
        await RevealCollectionAsync(connection, database, name);
        SetStatus(_localizer.Format(LocalizationKeys.Status.CollectionCreated, $"{database}.{name}"));
        _logger.LogInformation("Collection created");
    }

    private async Task<TreeRow?> RevealCollectionAsync(ActiveConnection connection, string database, string collection)
    {
        _tree.ExpandConnection(connection.Profile.Id);
        await _tree.ReloadCollectionsAsync(connection.Profile.Id, connection.Target, database, CancellationToken.None);
        if (!_tree.IsExpanded(connection.Profile.Id, database))
        {
            await _tree.ToggleDatabaseAsync(connection.Profile.Id, connection.Target, database, CancellationToken.None);
        }
        RebuildTree();
        var index = _rows.FindIndex(row => row.Profile.Id == connection.Profile.Id
            && row.Kind == TreeRowKind.Collection && row.Database == database && row.Collection == collection);
        if (index < 0) return null;
        _treeList.SelectedItem = index;
        return _rows[index];
    }
}
