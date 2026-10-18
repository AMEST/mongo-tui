using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Prompts for a database or collection name and keeps creation errors visible.</summary>
public sealed class CreateNameDialog : ThemedDialog
{
    private readonly IApplication _application;
    private readonly Func<string, Task> _create;
    private readonly TextField _name = new() { X = 1, Y = 2, Width = Dim.Fill(1) };
    private readonly Label _error = new() { X = 1, Y = 4, Width = Dim.Fill(1), Height = 2 };
    private bool _creating;

    public CreateNameDialog(ILocalizer localizer, IApplication application, string title, Func<string, Task> create)
    {
        _application = application;
        _create = create;
        Title = title;
        Width = 64;
        Height = 12;
        Add(new Label { Text = localizer.Get("Create.Name"), X = 1, Y = 0 }, _name, _error);
        var submit = new Button { Text = localizer.Get("Create.Submit"), IsDefault = true };
        submit.OnAcceptAsync(CreateAsync);
        var cancel = new Button { Text = localizer.Get("Connections.Cancel") };
        cancel.OnAccept(() => { if (!_creating) _application.RequestStop(this); });
        this.AddButtons(submit, cancel);
    }

    public string? CreatedName { get; private set; }

    private async Task CreateAsync()
    {
        if (_creating) return;
        _creating = true;
        _error.Text = string.Empty;
        try
        {
            var name = (_name.Text ?? string.Empty).Trim();
            await _create(name);
            CreatedName = name;
            _application.RequestStop(this);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
        finally
        {
            _creating = false;
        }
    }
}
