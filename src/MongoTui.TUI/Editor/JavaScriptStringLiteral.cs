using System.Globalization;
using System.Text;

namespace MongoTui.TUI.Editor;

/// <summary>Reads the quoted names used by query completion without evaluating JavaScript.</summary>
internal static class JavaScriptStringLiteral
{
    public static int FindEnd(string text, int start, char quote)
    {
        var end = start;
        while (end < text.Length && text[end] != quote && text[end] is not '\r' and not '\n')
        {
            if (text[end] == '\\' && end + 1 < text.Length && text[end + 1] is not '\r' and not '\n')
                end++;
            end++;
        }
        return end;
    }

    public static string? Decode(string text)
    {
        var decoded = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\')
                decoded.Append(text[index]);
            else if (++index == text.Length || !DecodeEscape(text, ref index, decoded))
                return null;
        }
        return decoded.ToString();
    }

    private static bool DecodeEscape(string text, ref int index, StringBuilder decoded)
    {
        var escaped = text[index];
        if (escaped is 'x' or 'u')
            return DecodeCodePoint(text, ref index, decoded);
        // Legacy octal escapes are not supported; do not resolve them to a different name.
        if (char.IsAsciiDigit(escaped) && (escaped != '0' || index + 1 < text.Length && char.IsAsciiDigit(text[index + 1])))
            return false;
        decoded.Append(escaped switch
        {
            'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r',
            't' => '\t', 'v' => '\v', '0' => '\0', _ => escaped,
        });
        return true;
    }

    private static bool DecodeCodePoint(string text, ref int index, StringBuilder decoded)
    {
        var braced = text[index] == 'u' && index + 1 < text.Length && text[index + 1] == '{';
        var start = index + (braced ? 2 : 1);
        var length = braced ? text.IndexOf('}', start) - start : text[index] == 'x' ? 2 : 4;
        if (length is < 1 or > 6 || start + length > text.Length
            || !int.TryParse(text.AsSpan(start, length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            || value > 0x10ffff)
            return false;
        if (value <= char.MaxValue)
            decoded.Append((char)value);
        else
            decoded.Append(char.ConvertFromUtf32(value));
        index = start + length - (braced ? 0 : 1);
        return true;
    }
}
