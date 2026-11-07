using System.Text;

namespace MongoTui.Application.Queries;

internal static class ExplainScriptText
{
    public static string WithoutComments(string script)
    {
        var result = new StringBuilder(script.Length);
        char quote = '\0';
        var escaped = false;
        for (var index = 0; index < script.Length; index++)
        {
            var character = script[index];
            if (quote != '\0')
            {
                result.Append(character);
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == quote) quote = '\0';
            }
            else if (character is '\'' or '"') { quote = character; result.Append(character); }
            else if (character == '/' && index + 1 < script.Length && script[index + 1] == '/')
            {
                while (index + 1 < script.Length && script[index + 1] is not ('\r' or '\n')) index++;
                result.Append(' ');
            }
            else if (character == '/' && index + 1 < script.Length && script[index + 1] == '*')
            {
                var end = script.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0) throw new FormatException();
                index = end + 1; result.Append(' ');
            }
            else result.Append(character);
        }
        return result.ToString();
    }
}
