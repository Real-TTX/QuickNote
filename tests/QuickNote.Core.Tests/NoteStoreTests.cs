using QuickNote.Core.Business;
using QuickNote.Core.Models;

namespace QuickNote.Core.Tests;

public sealed class NoteStoreTests : IDisposable
{
    private readonly string vaultPath = Path.Combine(Path.GetTempPath(), "quicknote-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(vaultPath))
        {
            Directory.Delete(vaultPath, recursive: true);
        }
    }

    [Fact]
    public void Encryption_RoundTrip_ReturnsOriginalText()
    {
        var encrypted = NoteEncryption.Encrypt("Geheim äöü", "kennwort");
        Assert.Equal("Geheim äöü", NoteEncryption.Decrypt(encrypted, "kennwort"));
    }

    [Fact]
    public void Encryption_WrongPassword_Throws()
    {
        var encrypted = NoteEncryption.Encrypt("Geheim", "richtig");
        Assert.Throws<InvalidPasswordException>(() => NoteEncryption.Decrypt(encrypted, "falsch"));
    }

    [Fact]
    public void Store_SaveAndLoad_KeepsMarkdownAndMetadata()
    {
        var note = NoteStore.CreateNote(vaultPath, "Studio");
        note.Title = "Raum für Ideen";
        note.Body = "# Hallo\n\n> Zitat";
        note.IsFavorite = true;
        note.Tags = ["a", "b"];
        NoteStore.Save(note);

        var loaded = NoteStore.LoadAll(vaultPath).Single();
        Assert.Equal("Raum für Ideen", loaded.Title);
        Assert.Equal("# Hallo\n\n> Zitat", loaded.Body);
        Assert.True(loaded.IsFavorite);
        Assert.Equal(["a", "b"], loaded.Tags);
    }

    [Fact]
    public void Store_EncryptedNote_IsHiddenUntilUnlocked()
    {
        var note = NoteStore.CreateNote(vaultPath, "Studio");
        note.Body = "streng geheim";
        NoteStore.Encrypt(note, "pw");

        var loaded = NoteStore.LoadAll(vaultPath).Single();
        Assert.True(loaded.IsEncrypted);
        Assert.False(loaded.IsUnlocked);
        Assert.EndsWith(".txts", loaded.FilePath);
        Assert.Throws<UnauthorizedAccessException>(() => NoteStore.Save(loaded));

        NoteStore.Unlock(loaded, "pw");
        Assert.Equal("streng geheim", loaded.Body);
    }

    [Fact]
    public void Exporter_LockedNote_IsRejected()
    {
        var locked = new Note { IsEncrypted = true, IsUnlocked = false };
        Assert.Throws<UnauthorizedAccessException>(() => NoteExporter.Render(locked, ExportFormat.Markdown));
    }
}
