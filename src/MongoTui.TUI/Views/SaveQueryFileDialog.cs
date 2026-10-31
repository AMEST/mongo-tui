using MongoTui.Application.Localization;
using MongoTui.Application.Abstractions.Localization;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Views;

/// <summary>Prompts for a file path (with a folder/file picker) and saves the active query.</summary>
public sealed class SaveQueryFileDialog : ThemedDialog
{
    private readonly IApplication _application;
    private readonly ILocalizer _localizer;
    private readonly Func<string, Task> _save;
    private readonly TextField _path;
    private readonly Label _error = new() { X = 1, Y = 3, Width = Dim.Fill(1), Height = 2 };
    private bool _saving;

    public SaveQueryFileDialog(ILocalizer localizer, IApplication application, string initialPath, Func<string, Task> save)
    {
        _application = application;
        _localizer = localizer;
        _save = save;
        Title = localizer.Get(LocalizationKeys.QueryFiles.SaveTitle);
        Width = 76;
        Height = 11;
        _path = new TextField { X = 1, Y = 1, Width = Dim.Fill(1), Text = initialPath };
        Add(new Label { Text = localizer.Get(LocalizationKeys.QueryFiles.Path), X = 1, Y = 0 }, _path, _error);

        var browse = new Button { Text = localizer.Get(LocalizationKeys.QueryFiles.Browse) };
        browse.OnAccept(Browse);
        var submit = new Button { Text = localizer.Get(LocalizationKeys.QueryFiles.Save), IsDefault = true };
        submit.OnAcceptAsync(SaveAsync);
        var cancel = new Button { Text = localizer.Get(LocalizationKeys.Common.Cancel) };
        cancel.OnAccept(() => { if (!_saving) _application.RequestStop(this); });
        this.AddButtons(browse, submit, cancel);
    }

    public bool Saved { get; private set; }

    public string? Path { get; private set; }

    private void Browse()
    {
        using var dialog = new SaveDialog
        {
            Title = _localizer.Get(LocalizationKeys.QueryFiles.SaveTitle),
            Path = _path.Text ?? string.Empty,
            OpenMode = OpenMode.File,
            MustExist = false,
            AllowedTypes = [new AllowedType(_localizer.Get(LocalizationKeys.QueryFiles.JavaScript), ".js")],
        };
        _application.Run(dialog);
        if (!dialog.Canceled && dialog.FileName is { Length: > 0 } fileName)
        {
            _path.Text = fileName;
        }
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        var path = (_path.Text ?? string.Empty).Trim();
        if (path.Length == 0)
        {
            _error.Text = _localizer.Get(LocalizationKeys.QueryFiles.PathRequired);
            return;
        }

        _saving = true;
        _error.Text = string.Empty;
        try
        {
            await _save(path);
            Path = path;
            Saved = true;
            _application.RequestStop(this);
        }
        catch (Exception exception)
        {
            _error.Text = exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }
}
