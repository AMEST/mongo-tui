using System.Text;
using MongoTui.Application.Abstractions.Process;

namespace MongoTui.Application.Models;

/// <summary>Owns one run's transcript and a bounded preview for the result pane.</summary>
public sealed class ScriptOutput : IAsyncDisposable
{
    public const int PreviewCharacters = 64 * 1024;
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(80);

    private readonly SemaphoreSlim _write = new(1);
    private readonly Queue<string> _preview = new();
    private FileStream? _file;
    private long _length;
    private int _previewLength;
    private bool _disposed, _lastWasError;
    private char _lastCharacter;

    public long Length => Interlocked.Read(ref _length);

    public (string Text, bool HasEarlierOutput) ReadPreview()
    {
        lock (_preview)
            return (string.Concat(_preview), _previewLength < _length);
    }

    public async ValueTask AppendAsync(ProcessOutput output)
    {
        if (output.Text.Length == 0) return;
        await _write.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            var text = Format(output);
            if (_file is null && _length + text.Length > PreviewCharacters)
                await SpillAsync().ConfigureAwait(false);
            if (_file is not null)
                await _file.WriteAsync(Encoding.UTF8.GetBytes(text)).ConfigureAwait(false);

            AddPreview(text);
            _lastWasError = output.IsStandardError;
            _lastCharacter = output.Text[^1];
        }
        finally { _write.Release(); }
    }

    /// <summary>Opens a reader for the full transcript. The caller owns the reader.</summary>
    public async ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken)
    {
        await _write.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _file is null
                ? new MemoryStream(Encoding.UTF8.GetBytes(ReadPreview().Text), writable: false)
                : new FileStream(_file.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        finally { _write.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _write.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_file is not null) await _file.DisposeAsync().ConfigureAwait(false);
        }
        finally { _write.Release(); }
    }

    private string Format(ProcessOutput output)
    {
        if (output.IsStandardError == _lastWasError) return output.Text;
        var separator = _length > 0 && _lastCharacter != '\n' ? "\n" : string.Empty;
        return separator + (output.IsStandardError ? "[stderr] " : string.Empty) + output.Text;
    }

    private async Task SpillAsync()
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.CreateNew,
            Share = FileShare.Read | FileShare.Delete,
            BufferSize = 1,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose,
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        _file = new FileStream(Path.Combine(Path.GetTempPath(), $"mongotui-output-{Guid.NewGuid():N}.txt"), options);
        await _file.WriteAsync(Encoding.UTF8.GetBytes(ReadPreview().Text)).ConfigureAwait(false);
    }

    private void AddPreview(string text)
    {
        lock (_preview)
        {
            Interlocked.Add(ref _length, text.Length);
            var start = Math.Max(0, text.Length - PreviewCharacters);
            if (start > 0 && char.IsLowSurrogate(text[start])) start++;
            _preview.Enqueue(text[start..]);
            _previewLength += text.Length - start;
            while (_previewLength > PreviewCharacters) _previewLength -= _preview.Dequeue().Length;
        }
    }
}
