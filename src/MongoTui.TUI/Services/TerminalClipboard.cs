using System.Text;
using Terminal.Gui.App;

namespace MongoTui.TUI.Services;

/// <summary>
/// Clipboard that prefers the platform clipboard and falls back to the OSC 52 terminal escape
/// sequence when the platform clipboard is unavailable. Headless Linux and SSH sessions without
/// <c>xclip</c> report no clipboard support, which previously made copy silently do nothing;
/// terminals that understand OSC 52 (most modern emulators) then place the text on the local
/// clipboard instead.
/// </summary>
internal sealed class TerminalClipboard : IClipboard
{
    private const string Osc52Prefix = "\u001b]52;c;";
    private const char Osc52Terminator = '\u0007';

    private readonly IClipboard? _platform;
    private readonly Action<string> _writeRaw;

    public TerminalClipboard(IClipboard? platform, Action<string> writeRaw)
    {
        _platform = platform;
        _writeRaw = writeRaw;
    }

    /// <summary>Always <see langword="true"/>: the OSC 52 fallback is available on any terminal.</summary>
    public bool IsSupported => true;

    public bool TrySetClipboardData(string text)
    {
        if (_platform?.TrySetClipboardData(text) == true)
        {
            return true;
        }

        return TryWriteOsc52(text);
    }

    public void SetClipboardData(string text)
    {
        if (!TrySetClipboardData(text))
        {
            throw new NotSupportedException("The clipboard is not available.");
        }
    }

    public bool TryGetClipboardData(out string result)
    {
        if (_platform is not null)
        {
            return _platform.TryGetClipboardData(out result);
        }

        result = string.Empty;

        return false;
    }

    public string GetClipboardData()
    {
        if (_platform is not null)
        {
            return _platform.GetClipboardData();
        }

        throw new NotSupportedException("The clipboard is not available.");
    }

    private bool TryWriteOsc52(string text)
    {
        try
        {
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            _writeRaw($"{Osc52Prefix}{payload}{Osc52Terminator}");

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
