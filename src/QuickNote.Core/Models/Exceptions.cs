namespace QuickNote.Core.Models;

public sealed class InvalidPasswordException : Exception
{
    public InvalidPasswordException() : base("Das Kennwort ist falsch oder die Datei ist beschädigt.") { }
}

public sealed class InvalidNoteFileException(string message) : Exception(message);

public enum ExportFormat
{
    Markdown,
    Html,
    PlainText,
}
