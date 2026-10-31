using MongoTui.Application.Localization;
using System.Drawing;
using System.Globalization;
using MongoTui.Application.Services;
using MongoTui.TUI.Editor;
using MongoTui.TUI.ViewModels;
using Serilog;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // The query workspace uses Terminal.Gui's TextView editor.

namespace MongoTui.TUI.Views;

/// <summary>Query editor with asynchronously loaded MongoDB name completions.</summary>
internal sealed class QueryEditorView : SyntaxTextView, ISuggestionGenerator
{
    private readonly AutocompleteService _autocomplete;
    private readonly QueryTabViewModel _model;
    private CancellationTokenSource? _request;
    private QueryCompletionContext? _loadedContext;
    private IReadOnlyList<string> _candidates = [];

    public QueryEditorView(AutocompleteService autocomplete, QueryTabViewModel model)
    {
        _autocomplete = autocomplete;
        _model = model;
        Multiline = true;
        WordWrap = false;
        Text = model.Script;
        Autocomplete = new QueryAutocomplete(GetCaretBack);
        Autocomplete.SuggestionGenerator = this;
        Autocomplete.Reopen = new Key(KeyCode.Space | KeyCode.CtrlMask);
        Autocomplete.MaxWidth = 60;
        Autocomplete.MaxHeight = 8;
        ContentsChanged += (_, _) => _request?.Cancel();
    }

    internal async Task<string?> CompleteAsync(CancellationToken cancellationToken)
    {
        CancelCompletion();
        var text = Text;
        var cursor = CursorOffset();
        var context = QueryCompletionContext.Parse(text, cursor, _model.Database);
        if (!HasFocus || SelectedLength > 0 || context is null)
            return LocalizationKeys.Autocomplete.NoContext;

        using var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _request = request;
        try
        {
            var candidates = context.MethodScope is { } scope
                ? QueryMethodCatalog.Get(scope).Select(method => method.Name).ToArray()
                : await _autocomplete.GetSuggestionsAsync(
                    _model.Target, context.Database, context.Collection, string.Empty, request.Token);
            if (request.IsCancellationRequested || !HasFocus || Text != text || CursorOffset() != cursor)
                return null;
            _loadedContext = context;
            _candidates = candidates;
            var popupContext = new AutocompleteContext(GetCurrentLine(), CurrentColumn, false);
            Autocomplete.Context = popupContext;
            Autocomplete.GenerateSuggestions(popupContext);
            Autocomplete.ProcessKey(Autocomplete.Reopen);
            RefreshAutocompletePopup();
            SetNeedsDraw();
            return Autocomplete.Suggestions.Count == 0 ? LocalizationKeys.Autocomplete.NoMatches : LocalizationKeys.Autocomplete.Hint;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { return null; }
        catch (Exception exception)
        {
            Log.Warning("Autocomplete failed with {ErrorType}", exception.GetType().Name);
            return LocalizationKeys.Autocomplete.Failed;
        }
        finally { if (ReferenceEquals(_request, request)) _request = null; }
    }

    public IEnumerable<Suggestion> GenerateSuggestions(AutocompleteContext context)
    {
        var text = Text;
        var cursor = CursorOffset();
        var parsed = QueryCompletionContext.Parse(text, cursor, _model.Database);
        if (parsed is null || !HasFocus || SelectedLength > 0)
            return [];
        var candidates = parsed.MethodScope is { } scope
            ? QueryMethodCatalog.Get(scope).Select(method => method.Name)
            : MatchesLoadedContext(parsed) ? _candidates : [];

        context.CursorPosition = CurrentColumn - new StringInfo(text[parsed.Start..cursor]).LengthInTextElements;
        var remove = new StringInfo(text.Substring(parsed.Start, parsed.Length)).LengthInTextElements;
        return candidates
            .Where(candidate => candidate.StartsWith(parsed.Prefix, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => parsed.Replacement(candidate) != text.Substring(parsed.Start, parsed.Length))
            .Select(candidate => new Suggestion(remove, parsed.Replacement(candidate), candidate));
    }

    public bool IsWordChar(string text) =>
        text.Length > 0 && (char.IsLetterOrDigit(text, 0) || text[0] is '_' or '$' or '.');

    private bool MatchesLoadedContext(QueryCompletionContext parsed) => _loadedContext is { } loaded
        && parsed.Database == loaded.Database && parsed.Collection == loaded.Collection && parsed.Start == loaded.Start
        && parsed.Quote == loaded.Quote && parsed.MemberAccess == loaded.MemberAccess && loaded.MethodScope is null;

    private int GetCaretBack(Suggestion suggestion)
    {
        var context = QueryCompletionContext.Parse(Text, CursorOffset(), _model.Database);
        return context is { MethodScope: { } scope, HasCallSuffix: false }
            ? QueryMethodCatalog.Get(scope).Single(method => method.Name == suggestion.Title).CaretBack : 0;
    }

    protected override bool OnKeyDownNotHandled(Key key)
    {
        var handled = base.OnKeyDownNotHandled(key);
        if (handled && !string.IsNullOrEmpty(key.AsGrapheme)
            && QueryCompletionContext.Parse(Text, CursorOffset(), _model.Database)?.MethodScope is not null)
        {
            Autocomplete.ProcessKey(Autocomplete.Reopen);
            RefreshAutocompletePopup();
        }
        return handled;
    }

    internal void CancelCompletion()
    {
        _request?.Cancel();
        _loadedContext = null;
        _candidates = [];
        Autocomplete.ClearSuggestions();
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.Esc && _request is not null)
        {
            CancelCompletion();
            return true;
        }
        var handled = base.OnKeyDown(key);
        if (handled && Autocomplete.Visible && (key == Key.CursorDown || key == Key.CursorUp))
        {
            // The popup is a sibling of the editor and needs its own draw invalidation.
            SuperView?.SetNeedsDraw();
        }
        return handled;
    }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocused, View? focused)
    {
        if (!newHasFocus) CancelCompletion();
        base.OnHasFocusChanged(newHasFocus, previousFocused, focused);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) CancelCompletion();
        base.Dispose(disposing);
    }

    private int CursorOffset()
    {
        var text = Text;
        var offset = 0;
        for (var row = 0; row < CurrentRow; row++)
        {
            var newline = text.IndexOf('\n', offset);
            if (newline < 0) return text.Length;
            offset = newline + 1;
        }
        return Math.Min(text.Length,
            offset + string.Concat(GetCurrentLine().Take(CurrentColumn).Select(cell => cell.Grapheme)).Length);
    }

    private void RefreshAutocompletePopup()
    {
        if (App?.Driver is null) return;
        // TextView normally positions the popup only when its contents change.
        Autocomplete.RenderOverlay(new Point(Autocomplete.Context.CursorPosition, CurrentRow + 1 - Viewport.Y));
        SuperView?.SetNeedsDraw();
    }
}
