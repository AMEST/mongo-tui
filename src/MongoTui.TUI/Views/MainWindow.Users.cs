using MongoTui.Application.Services;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;

namespace MongoTui.TUI.Views;

public sealed partial class MainWindow
{
    private async Task OpenUserManagementAsync(ActiveConnection connection, string database)
    {
        if (Application is null)
        {
            return;
        }

        var scope = DatabaseName.Create(database);
        var current = ConnectionUriParser.Parse(connection.Target.ConnectionString);
        var dialog = new UserManagementDialog(
            _users,
            _localizer,
            Application,
            connection,
            scope,
            current.Username,
            current.AuthDatabase ?? "admin");

        await dialog.LoadAsync();
        Application.Run(dialog);
    }
}
