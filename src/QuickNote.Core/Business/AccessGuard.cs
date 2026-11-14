using QuickNote.Core.Models;

namespace QuickNote.Core.Business;

/// <summary>Permission checks. Always executed in the business layer, never only in the UI.</summary>
public static class AccessGuard
{
    public static void EnsureCanRead(Note note)
    {
        if (note.IsEncrypted && !note.IsUnlocked)
        {
            throw new UnauthorizedAccessException("Die Notiz ist verschlüsselt und noch nicht entsperrt.");
        }
    }

    public static void EnsureCanModify(Note note)
    {
        EnsureCanRead(note);

        if (note.IsEncrypted && string.IsNullOrEmpty(note.Password))
        {
            throw new UnauthorizedAccessException("Für die verschlüsselte Notiz liegt kein Kennwort vor.");
        }

        if (File.Exists(note.FilePath) && File.GetAttributes(note.FilePath).HasFlag(FileAttributes.ReadOnly))
        {
            throw new UnauthorizedAccessException($"Die Datei ist schreibgeschützt: {note.FilePath}");
        }
    }

    public static void EnsureCanExport(Note note) => EnsureCanRead(note);
}
