namespace QuickNote.Core.Models;

/// <summary>A single note. Content is Markdown; plain data only, no storage logic.</summary>
public sealed class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Notebook { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>True when the note is stored as an encrypted .txts file.</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>False for encrypted notes that have not been unlocked yet (title and body are unknown).</summary>
    public bool IsUnlocked { get; set; } = true;

    /// <summary>Password kept in memory while an encrypted note is unlocked. Never persisted.</summary>
    public string? Password { get; set; }

    public string FilePath { get; set; } = string.Empty;
}
