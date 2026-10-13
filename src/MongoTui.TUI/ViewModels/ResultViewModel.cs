using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoTui.Domain.Queries;

namespace MongoTui.TUI.ViewModels;

/// <summary>Projects a <see cref="QueryResult"/> into tabular, tree and JSON views.</summary>
public sealed class ResultViewModel : ObservableObject
{
    private static readonly JsonWriterSettings IndentedJson = new() { Indent = true };

    private QueryResultKind _kind = QueryResultKind.Documents;
    private IReadOnlyList<BsonDocument> _documents = [];
    private IReadOnlyList<string> _columns = [];
    private IReadOnlyList<IReadOnlyList<string>> _rows = [];
    private string _json = string.Empty;
    private string? _error;
    private TimeSpan _elapsed;
    private ResultViewMode _mode = ResultViewMode.Table;

    public QueryResultKind Kind
    {
        get => _kind;
        private set => SetProperty(ref _kind, value);
    }

    public IReadOnlyList<BsonDocument> Documents
    {
        get => _documents;
        private set => SetProperty(ref _documents, value);
    }

    public IReadOnlyList<string> Columns
    {
        get => _columns;
        private set => SetProperty(ref _columns, value);
    }

    public IReadOnlyList<IReadOnlyList<string>> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public string Json
    {
        get => _json;
        private set => SetProperty(ref _json, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public TimeSpan Elapsed
    {
        get => _elapsed;
        private set => SetProperty(ref _elapsed, value);
    }

    public ResultViewMode Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    public bool HasError => _error is not null;

    public void Load(QueryResult result)
    {
        Kind = result.Kind;
        Documents = result.Documents;
        Elapsed = result.Elapsed;
        Error = null;

        Rebuild();
    }

    public void LoadError(string message)
    {
        Kind = QueryResultKind.Scalar;
        Documents = [];
        Elapsed = TimeSpan.Zero;
        Error = message;

        Rebuild();
    }

    /// <summary>
    /// Resolves the document whose rendered lines contain <paramref name="row"/>,
    /// or <see langword="null"/> when the row is not on a document (for example a table header).
    /// </summary>
    public int? DocumentIndexAtRow(int row)
    {
        if (row < 0 || Documents.Count == 0)
        {
            return null;
        }

        if (Mode == ResultViewMode.Table)
        {
            var index = row - 2;
            return index >= 0 && index < Documents.Count ? index : null;
        }

        int? found = null;
        var start = 0;

        for (var index = 0; index < Documents.Count; index++)
        {
            var length = LineCount(Documents[index]);

            if (row >= start && row < start + length)
            {
                found = index;
            }

            // Tree documents are separated by a newline; JSON array elements share
            // their boundary line (for example "  }, {") with the following document.
            start += Mode == ResultViewMode.Tree ? length : length - 1;
        }

        return found;
    }

    /// <summary>Renders the current result using <see cref="Mode"/>.</summary>
    public string Render()
    {
        if (HasError)
        {
            return Error!;
        }

        return Mode switch
        {
            ResultViewMode.Json => Json,
            ResultViewMode.Tree => string.Join("\n", Documents.Select(document => document.ToJson(IndentedJson))),
            _ => RenderTable(),
        };
    }

    private string RenderTable()
    {
        if (Columns.Count == 0 || Rows.Count == 0)
        {
            return string.Empty;
        }

        var widths = Columns
            .Select((_, columnIndex) => Math.Max(
                Columns[columnIndex].Length,
                Rows.Max(row => row[columnIndex].Length)))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(" | ", Columns.Select((column, index) => column.PadRight(widths[index]))));
        builder.AppendLine(string.Join("-+-", widths.Select(width => new string('-', width))));

        foreach (var row in Rows)
        {
            builder.AppendLine(string.Join(" | ", row.Select((value, index) => value.PadRight(widths[index]))));
        }

        return builder.ToString();
    }

    private void Rebuild()
    {
        Columns = Documents
            .SelectMany(document => document.Names)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Rows = Documents
            .Select(document => (IReadOnlyList<string>)Columns
                .Select(column => FormatValue(document, column))
                .ToList())
            .ToList();

        Json = new BsonArray(Documents).ToJson(IndentedJson);
    }

    private static int LineCount(BsonDocument document) =>
        document.ToJson(IndentedJson).Count(character => character == '\n') + 1;

    private static string FormatValue(BsonDocument document, string column)
    {
        if (!document.TryGetValue(column, out var value) || value.IsBsonNull)
        {
            return string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
