using System.Collections.ObjectModel;
using MongoTui.Application.Abstractions.Localization;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618
namespace MongoTui.TUI.Views;

public sealed class ExplainDialog : DiagnosticDialogBase
{
    private readonly ExplainService _service;
    private readonly QueryExecutionContext _context;
    private readonly SyntaxTextView _query = new() { X = 1, Y = 4, Width = Dim.Fill(1), Height = 3, Multiline = true, WordWrap = false };
    private readonly TextView _summary = new() { X = 1, Y = 11, Width = Dim.Fill(1), Height = Dim.Fill(2), ReadOnly = true, WordWrap = true };
    private readonly DropDownList _verbosity = new() { X = 16, Y = 7, Width = Dim.Fill(1), ReadOnly = true };
    private readonly TreeView<ExplainNode> _tree = new() { X = 1, Y = 11, Width = Dim.Percent(45), Height = Dim.Fill(2) };
    private readonly string[] _verbosityLabels;
    private readonly Button _run, _copy, _json;
    private ExplainResult? _result;
    private bool _raw;
    public ExplainDialog(ExplainService service, QueryExecutionContext context, ILocalizer localizer, IApplication application)
        : base(localizer, application, "Explain.Title")
    {
        _service = service; _context = context; Height = Dim.Percent(95);
        _query.Text = context.Script;
        _verbosityLabels = Enum.GetNames<ExplainVerbosity>().Select(name => localizer.Get("Explain." + name)).ToArray();
        _verbosity.Source = new ListWrapper<string>(new ObservableCollection<string>(_verbosityLabels));
        _verbosity.Value = _verbosityLabels[(int)ExplainVerbosity.ExecutionStats];
        _tree.TreeBuilder = new DelegateTreeBuilder<ExplainNode>(node => node.Children, node => node.Children.Count > 0);
        _tree.AspectGetter = node => node.Stage + " " + node.IndexName;
        _tree.SelectionChanged += (_, _) => RenderResult();
        _summary.X = Pos.Right(_tree) + 1; _summary.Width = Dim.Fill(1);
        _summary.Text = localizer.Get("Explain.Empty");
        Feedback.Y = 8; Feedback.Height = 2;
        AddLabels(context.Database);
        Add(_query, _verbosity, _summary, _tree);
        _run = ActionButton("Explain.Run", LoadAsync);
        _copy = ActionButton("Explain.CopyJson", () =>
        { if (_result is not null) UiApplication.Clipboard?.TrySetClipboardData(BsonTextCodec.Format(_result.Raw)); return Task.CompletedTask; });
        _json = ActionButton("Explain.ShowJson", () => { _raw = !_raw; RenderResult(); return Task.CompletedTask; });
        _copy.Enabled = false; _json.Enabled = false;
        _query.KeyDown += OnExplainKeyDown; KeyDown += OnExplainKeyDown;
        Disposing += (_, _) => _result = null;
        _query.SetFocus();
    }

    private void AddLabels(string? database)
    {
        Add(new Label { X = 1, Y = 0, Width = Dim.Fill(1), Text = Localizer.Get("Diagnostics.Database") + ": " + database });
        Add(new Label { X = 1, Y = 1, Height = 2, Width = Dim.Fill(1), Text = Localizer.Get("Explain.Instructions") });
        Add(new Label { X = 1, Y = 3, Text = Localizer.Get("Explain.Query") });
        Add(new Label { X = 1, Y = 7, Text = Localizer.Get("Explain.Verbosity") });
        Add(new Label { X = 1, Y = 10, Width = Dim.Percent(45), Text = Localizer.Get("Explain.Plan") });
        Add(new Label { X = Pos.Right(_tree) + 1, Y = 10, Width = Dim.Fill(1), Text = Localizer.Get("Explain.Summary") });
    }

    private void OnExplainKeyDown(object? sender, Key key)
    {
        if (key != Key.F8 && key != Key.Enter.WithCtrl && key != Key.Enter.WithCtrl.WithShift) return;
        key.Handled = true;
        if (!Busy) _run.InvokeCommand(Command.Accept);
    }

    public Task LoadAsync()
    {
        if (Busy || Closed) return Task.CompletedTask;
        _result = null; _tree.ClearObjects(); _raw = false; _copy.Enabled = false; _json.Enabled = false;
        var context = _context with { Script = _query.Text };
        var verbosity = (ExplainVerbosity)Math.Max(0, Array.IndexOf(_verbosityLabels, _verbosity.Value));
        var request = _service.TryCreateRequest(context, verbosity);
        if (request is null)
        {
            Feedback.Text = Localizer.Get("Explain.Unsupported");
            _summary.Text = Localizer.Get("Explain.Empty"); SetNeedsDraw(); return Task.CompletedTask;
        }
        _query.ReadOnly = true; _verbosity.Enabled = false; _summary.Text = Localizer.Get("Common.Loading");
        return RunAsync(token => _service.ExplainAsync(context.Target, request, token), result =>
        {
            _result = result;
            _tree.AddObject(result.Plan); _tree.ExpandAll();
            RenderResult();
        }, () =>
        {
            _query.ReadOnly = false; _verbosity.Enabled = true;
            _copy.Enabled = _result is not null; _json.Enabled = _result is not null;
            if (_result is null) _summary.Text = Feedback.Text;
        });
    }
    private void RenderResult()
    {
        if (_result is not { } result) return;
        if (_raw) { _summary.Text = BsonTextCodec.Format(result.Raw); return; }
        var node = _tree.SelectedObject ?? result.Plan;
        var lines = new List<string>
        {
            result.Namespace ?? "",
            Localizer.Get("Explain.ExecutionTime") + ": " + Number(result.ExecutionTimeMs),
            Localizer.Get("Diagnostics.DocumentsExamined") + ": " + Number(result.DocumentsExamined),
            Localizer.Get("Diagnostics.KeysExamined") + ": " + Number(result.KeysExamined),
            Localizer.Get("Diagnostics.Returned") + ": " + Number(result.Returned),
            "", Localizer.Get("Explain.Stage") + ": " + node.Stage,
            Localizer.Get("Explain.Index") + ": " + (node.IndexName ?? "—"),
            Localizer.Get("Explain.Spills") + ": " + Number(node.Spills),
            "", Localizer.Get("Explain.Warnings"),
        };
        lines.AddRange(result.Warnings.Count == 0 ? [Localizer.Get("Explain.NoWarnings")]
            : result.Warnings.Select(warning => Localizer.Get("Explain." + warning.Kind)));
        lines.Add(""); lines.Add(Localizer.Get("Explain.PlanHelp"));
        _summary.Text = string.Join('\n', lines);
    }

    private static string Number(long? value) => value?.ToString() ?? "—";
}
