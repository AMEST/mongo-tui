using System.Collections.ObjectModel;
using System.Drawing;
using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Small modal list of actions shown as a context menu.</summary>
public sealed class ContextMenuDialog : ThemedDialog
{
    private readonly ListView _list = new();

    public ContextMenuDialog(
        ILocalizer localizer,
        IApplication application,
        IReadOnlyList<string> items,
        Point position)
    {
        Title = localizer.Get("Tree.ContextMenu");
        Width = 36;
        Height = Math.Clamp(items.Count + 3, 5, 18);
        X = position.X;
        Y = position.Y;

        _list.X = 0;
        _list.Y = 0;
        _list.Width = Dim.Fill();
        _list.Height = Dim.Fill();
        _list.SetSource(new ObservableCollection<string>(items));
        _list.SelectedItem = items.Count > 0 ? 0 : null;
        _list.Accepted += (_, _) => Choose(application);
        _list.Activated += (_, _) => Choose(application);

        Add(_list);
        _list.SetFocus();
    }

    private void Choose(IApplication application)
    {
        SelectedIndex = _list.SelectedItem ?? -1;
        application.RequestStop(this);
    }

    public int SelectedIndex { get; private set; } = -1;
}
