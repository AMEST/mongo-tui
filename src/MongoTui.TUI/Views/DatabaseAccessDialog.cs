using MongoTui.Application.Localization;
using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Users;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Dialog for adding database access.</summary>
public sealed class DatabaseAccessDialog : ThemedDialog
{
    private readonly ILocalizer _localizer;
    private readonly IApplication _application;
    private readonly DropDownList _database = new() { X = 1, Y = 2, Width = Dim.Fill(1), ReadOnly = false };
    private readonly ListView _levelList = new() { X = 1, Y = 6, Width = Dim.Fill(1), Height = 4 };
    private readonly Label _error = new() { X = 1, Y = 10, Width = Dim.Fill(1), Height = 2 };

    public DatabaseAccessDialog(ILocalizer localizer, IApplication application, IReadOnlyList<string> knownDatabases)
    {
        _localizer = localizer;
        _application = application;
        Title = localizer.Get(LocalizationKeys.Users.AccessAdd);
        Width = 60;
        Height = 16;

        Add(new Label { Text = localizer.Get(LocalizationKeys.Users.AccessDatabase), X = 1, Y = 0 });
        Add(_database);
        var databases = knownDatabases.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (databases.Count > 0)
        {
            _database.Source = new ListWrapper<string>(new ObservableCollection<string>(databases));
        }
        Add(new Label { Text = localizer.Get(LocalizationKeys.Users.AccessLevel), X = 1, Y = 4 });

        _levelList.SetSource(new ObservableCollection<string>(new[] { localizer.Get(LocalizationKeys.Users.AccessRead), localizer.Get(LocalizationKeys.Users.AccessReadWrite), localizer.Get(LocalizationKeys.Users.AccessDbOwner) }));
        _levelList.SelectedItem = 0;
        Add(_levelList);
        Add(_error);

        var ok = new Button { Text = localizer.Get(LocalizationKeys.Common.Ok), IsDefault = true };
        ok.OnAccept(() => Accept());
        var cancel = new Button { Text = localizer.Get(LocalizationKeys.Connections.Cancel) };
        cancel.OnAccept(() => application.RequestStop(this));
        this.AddButtons(ok, cancel);
    }

    public DatabaseAccess? SelectedAccess { get; private set; }

    private void Accept()
    {
        _error.Text = string.Empty;
        var dbName = (_database.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(dbName))
        {
            _error.Text = _localizer.Get(LocalizationKeys.Users.AccessDatabaseInvalid);
            return;
        }

        try
        {
            var db = DatabaseName.Create(dbName);
            var level = (_levelList.SelectedItem ?? 0) switch
            {
                0 => DatabaseAccessLevel.Read,
                1 => DatabaseAccessLevel.ReadWrite,
                _ => DatabaseAccessLevel.DbOwner
            };
            SelectedAccess = new DatabaseAccess(db, level);
            _application.RequestStop(this);
        }
        catch (Exception)
        {
            _error.Text = _localizer.Get(LocalizationKeys.Users.AccessDatabaseInvalid);
        }
    }
}
