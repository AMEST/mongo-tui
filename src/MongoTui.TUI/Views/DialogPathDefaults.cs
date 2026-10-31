using System.Globalization;
using System.Text;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Transfer;

namespace MongoTui.TUI.Views;

internal static class DialogPathDefaults
{
    public static string OpenDirectory(string homeDirectory) =>
        Path.EndsInDirectorySeparator(homeDirectory) ? homeDirectory : homeDirectory + Path.DirectorySeparatorChar;

    public static string ExportPath(string homeDirectory, CollectionRef collection, DateTimeOffset timestamp, ExportFormat format)
    {
        var date = timestamp.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture);
        var name = $"{FileNamePart(collection.Database.Value)}.{FileNamePart(collection.Collection.Value)}_{date}";
        return Path.Combine(homeDirectory, name + ExportExtension(format));
    }

    public static string ExportExtension(ExportFormat format) => format switch
    {
        ExportFormat.Csv => ".csv",
        ExportFormat.Bson => ".bson",
        _ => ".jsonl",
    };

    private static string FileNamePart(string value)
    {
        var result = new StringBuilder();
        var bytes = 0;
        foreach (var character in value.EnumerateRunes())
        {
            var safe = Rune.IsControl(character) || character.IsAscii && "<>:\"/\\|?*".Contains((char)character.Value)
                ? new Rune('_') : character;
            if (bytes + safe.Utf8SequenceLength > 64) break;
            result.Append(safe);
            bytes += safe.Utf8SequenceLength;
        }
        return result.ToString().TrimEnd(' ', '.');
    }
}
