using System.Text;
using Markdig;
using QuickNote.Core.Models;

namespace QuickNote.Core.Business;

public static class NoteExporter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public static string FileExtensionFor(ExportFormat format) => format switch
    {
        ExportFormat.Markdown => ".md",
        ExportFormat.Html => ".html",
        _ => ".txt",
    };

    public static string Render(Note note, ExportFormat format)
    {
        AccessGuard.EnsureCanExport(note);
        var markdown = BuildMarkdown(note);
        return format switch
        {
            ExportFormat.Markdown => markdown,
            ExportFormat.Html => BuildHtml(note.Title, markdown),
            _ => Markdown.ToPlainText(markdown, Pipeline),
        };
    }

    public static void ExportToFile(Note note, ExportFormat format, string targetPath)
        => File.WriteAllText(targetPath, Render(note, format), new UTF8Encoding(false));

    private static string BuildMarkdown(Note note)
    {
        var hasHeading = note.Body.TrimStart().StartsWith('#');
        return hasHeading || string.IsNullOrWhiteSpace(note.Title) ? note.Body : $"# {note.Title}\n\n{note.Body}";
    }

    private static string BuildHtml(string title, string markdown)
    {
        var body = Markdown.ToHtml(markdown, Pipeline);
        var safeTitle = System.Net.WebUtility.HtmlEncode(title);
        const string style =
            "body{font-family:Georgia,serif;max-width:46rem;margin:3rem auto;padding:0 1rem;line-height:1.65;color:#2b2623}" +
            "blockquote{border-left:3px solid #db4c3f;margin-left:0;padding-left:1rem;font-style:italic}" +
            "code{background:#f5eeea;padding:.1em .3em;border-radius:4px}";
        return $"<!DOCTYPE html>\n<html lang=\"de\"><head><meta charset=\"utf-8\"><title>{safeTitle}</title>\n<style>{style}</style>\n</head><body>\n{body}\n</body></html>\n";
    }
}
