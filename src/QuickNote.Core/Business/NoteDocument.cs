using System.Globalization;
using System.Text;
using QuickNote.Core.Models;

namespace QuickNote.Core.Business;

/// <summary>Converts a note to and from its Markdown file representation (Markdown with a small front matter block).</summary>
public static class NoteDocument
{
    private const string FrontMatterFence = "---";
    private const string DateFormat = "o";

    public static string Serialize(Note note)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FrontMatterFence);
        builder.AppendLine($"id: {note.Id}");
        builder.AppendLine($"title: {note.Title.ReplaceLineEndings(" ")}");
        builder.AppendLine($"tags: {string.Join(", ", note.Tags)}");
        builder.AppendLine($"favorite: {note.IsFavorite.ToString().ToLowerInvariant()}");
        builder.AppendLine($"archived: {note.IsArchived.ToString().ToLowerInvariant()}");
        builder.AppendLine($"created: {note.CreatedUtc.ToString(DateFormat, CultureInfo.InvariantCulture)}");
        builder.AppendLine($"modified: {note.ModifiedUtc.ToString(DateFormat, CultureInfo.InvariantCulture)}");
        builder.AppendLine(FrontMatterFence);
        builder.AppendLine();
        builder.Append(note.Body);
        return builder.ToString();
    }

    /// <summary>Applies the content of a Markdown file to the given note. Files without front matter are accepted as plain Markdown.</summary>
    public static void ApplyTo(Note note, string fileText)
    {
        var lines = fileText.Replace("\r\n", "\n").Split('\n');
        if (lines[0].Trim() != FrontMatterFence)
        {
            note.Body = fileText;
            note.Title = DeriveTitle(fileText, note.Title);
            return;
        }

        var closingIndex = Array.FindIndex(lines, 1, line => line.Trim() == FrontMatterFence);
        if (closingIndex < 0)
        {
            note.Body = fileText;
            return;
        }

        foreach (var line in lines[1..closingIndex])
        {
            ApplyFrontMatterLine(note, line);
        }

        note.Body = string.Join('\n', lines[(closingIndex + 1)..]).TrimStart('\n');
    }

    public static string DeriveTitle(string markdown, string fallback)
    {
        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('#').Trim();
            if (line.Length > 0)
            {
                return line.Length > 80 ? line[..80] : line;
            }
        }

        return fallback;
    }

    private static void ApplyFrontMatterLine(Note note, string line)
    {
        var separator = line.IndexOf(':');
        if (separator < 0)
        {
            return;
        }

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();
        switch (key)
        {
            case "id" when Guid.TryParse(value, out var id): note.Id = id; break;
            case "title": note.Title = value; break;
            case "tags": note.Tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); break;
            case "favorite": note.IsFavorite = value == "true"; break;
            case "archived": note.IsArchived = value == "true"; break;
            case "created": note.CreatedUtc = ParseDate(value, note.CreatedUtc); break;
            case "modified": note.ModifiedUtc = ParseDate(value, note.ModifiedUtc); break;
        }
    }

    private static DateTime ParseDate(string value, DateTime fallback)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed.ToUniversalTime() : fallback;
}
