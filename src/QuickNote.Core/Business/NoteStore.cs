using System.Text;
using QuickNote.Core.Models;

namespace QuickNote.Core.Business;

/// <summary>
/// File based note storage. Layout: vault/notebook/id.md (plain) or id.txts (encrypted).
/// Notebooks are folders. There is no database.
/// </summary>
public static class NoteStore
{
    public const string PlainExtension = ".md";
    public const string EncryptedExtension = ".txts";
    public const string DefaultNotebook = "Allgemein";

    public static string DefaultVaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QuickNote");

    public static IReadOnlyList<string> LoadNotebooks(string vaultPath)
    {
        Directory.CreateDirectory(vaultPath);
        var notebooks = Directory.GetDirectories(vaultPath)
            .Select(folder => Path.GetFileName(folder))
            .Where(name => !string.IsNullOrEmpty(name) && !name.StartsWith('.'))
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (notebooks.Count == 0)
        {
            CreateNotebook(vaultPath, DefaultNotebook);
            notebooks.Add(DefaultNotebook);
        }

        return notebooks;
    }

    public static void CreateNotebook(string vaultPath, string name)
    {
        var safeName = SanitizeFileName(name);
        if (safeName.Length == 0)
        {
            throw new ArgumentException("Der Name des Notizbuchs ist leer.", nameof(name));
        }

        Directory.CreateDirectory(Path.Combine(vaultPath, safeName));
    }

    public static IReadOnlyList<Note> LoadAll(string vaultPath)
    {
        var notes = new List<Note>();
        foreach (var notebook in LoadNotebooks(vaultPath))
        {
            var folder = Path.Combine(vaultPath, notebook);
            var loaded = Directory.EnumerateFiles(folder)
                .Select(file => TryLoad(file, notebook))
                .OfType<Note>();
            notes.AddRange(loaded);
        }

        return notes;
    }

    public static Note CreateNote(string vaultPath, string notebook)
    {
        var note = new Note { Notebook = notebook, Title = "Neue Notiz" };
        note.FilePath = BuildPath(vaultPath, note);
        Save(note);
        return note;
    }

    public static void Save(Note note)
    {
        AccessGuard.EnsureCanModify(note);
        note.ModifiedUtc = DateTime.UtcNow;
        var markdown = NoteDocument.Serialize(note);
        Directory.CreateDirectory(Path.GetDirectoryName(note.FilePath)!);

        if (note.IsEncrypted)
        {
            File.WriteAllBytes(note.FilePath, NoteEncryption.Encrypt(markdown, note.Password!));
            return;
        }

        File.WriteAllText(note.FilePath, markdown, new UTF8Encoding(false));
    }

    public static void Delete(Note note)
    {
        // Deleting an encrypted note does not require the password, but the file must not be read-only.
        if (File.Exists(note.FilePath) && File.GetAttributes(note.FilePath).HasFlag(FileAttributes.ReadOnly))
        {
            throw new UnauthorizedAccessException($"Die Datei ist schreibgeschützt: {note.FilePath}");
        }

        File.Delete(note.FilePath);
    }

    public static void MoveToNotebook(Note note, string vaultPath, string notebook)
    {
        AccessGuard.EnsureCanModify(note);
        var oldPath = note.FilePath;
        note.Notebook = notebook;
        note.FilePath = BuildPath(vaultPath, note);
        Save(note);
        if (!string.Equals(oldPath, note.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(oldPath);
        }
    }

    /// <summary>Encrypts a note with a password (plain .md becomes .txts).</summary>
    public static void Encrypt(Note note, string password)
    {
        AccessGuard.EnsureCanModify(note);
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Das Kennwort darf nicht leer sein.", nameof(password));
        }

        var oldPath = note.FilePath;
        note.IsEncrypted = true;
        note.Password = password;
        note.FilePath = Path.ChangeExtension(oldPath, EncryptedExtension);
        Save(note);
        File.Delete(oldPath);
    }

    /// <summary>Removes the encryption (.txts becomes plain .md). Requires an unlocked note.</summary>
    public static void Decrypt(Note note)
    {
        AccessGuard.EnsureCanModify(note);
        var oldPath = note.FilePath;
        note.IsEncrypted = false;
        note.Password = null;
        note.FilePath = Path.ChangeExtension(oldPath, PlainExtension);
        Save(note);
        File.Delete(oldPath);
    }

    /// <summary>Unlocks an encrypted note. Throws <see cref="InvalidPasswordException"/> for a wrong password.</summary>
    public static void Unlock(Note note, string password)
    {
        var markdown = NoteEncryption.Decrypt(File.ReadAllBytes(note.FilePath), password);
        NoteDocument.ApplyTo(note, markdown);
        note.Password = password;
        note.IsUnlocked = true;
    }

    public static void Lock(Note note)
    {
        if (!note.IsEncrypted)
        {
            return;
        }

        note.Password = null;
        note.IsUnlocked = false;
        note.Body = string.Empty;
        note.Title = string.Empty;
    }

    /// <summary>Copies an external .md/.txt/.txts file into the vault and returns the new note.</summary>
    public static Note Import(string vaultPath, string notebook, string sourcePath)
    {
        var isEncrypted = Path.GetExtension(sourcePath).Equals(EncryptedExtension, StringComparison.OrdinalIgnoreCase);
        var note = new Note { Notebook = notebook, Title = Path.GetFileNameWithoutExtension(sourcePath) };
        if (isEncrypted)
        {
            note.IsEncrypted = true;
            note.IsUnlocked = false;
            note.Title = string.Empty;
            note.FilePath = BuildPath(vaultPath, note);
            Directory.CreateDirectory(Path.GetDirectoryName(note.FilePath)!);
            File.Copy(sourcePath, note.FilePath);
            return note;
        }

        note.FilePath = BuildPath(vaultPath, note);
        NoteDocument.ApplyTo(note, File.ReadAllText(sourcePath));
        Save(note);
        return note;
    }

    private static Note? TryLoad(string file, string notebook)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();
        var isEncrypted = extension == EncryptedExtension;
        if (!isEncrypted && extension != PlainExtension)
        {
            return null;
        }

        var info = new FileInfo(file);
        var note = new Note
        {
            Notebook = notebook,
            FilePath = file,
            IsEncrypted = isEncrypted,
            IsUnlocked = !isEncrypted,
            Title = isEncrypted ? string.Empty : Path.GetFileNameWithoutExtension(file),
            ModifiedUtc = info.LastWriteTimeUtc,
            CreatedUtc = info.CreationTimeUtc,
        };
        if (Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var idFromName))
        {
            note.Id = idFromName;
        }

        if (!isEncrypted)
        {
            NoteDocument.ApplyTo(note, File.ReadAllText(file));
        }

        return note;
    }

    private static string BuildPath(string vaultPath, Note note)
    {
        var extension = note.IsEncrypted ? EncryptedExtension : PlainExtension;
        return Path.Combine(vaultPath, SanitizeFileName(note.Notebook), note.Id + extension);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(character => !invalid.Contains(character)).ToArray()).Trim();
    }
}
