using System.Security.Cryptography;
using System.Text;
using QuickNote.Core.Models;

namespace QuickNote.Core.Business;

/// <summary>
/// Password based encryption for .txts files.
/// Layout: magic "QNSEC1" | iterations (int32) | salt (16) | nonce (12) | tag (16) | ciphertext.
/// Key derivation: PBKDF2-SHA256, cipher: AES-256-GCM.
/// </summary>
public static class NoteEncryption
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;
    private static readonly byte[] Magic = "QNSEC1"u8.ToArray();

    public static byte[] Encrypt(string plainText, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveKey(password, salt, Iterations);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag, Magic);

        using var stream = new MemoryStream();
        stream.Write(Magic);
        stream.Write(BitConverter.GetBytes(Iterations));
        stream.Write(salt);
        stream.Write(nonce);
        stream.Write(tag);
        stream.Write(cipherBytes);
        return stream.ToArray();
    }

    public static string Decrypt(byte[] fileContent, string password)
    {
        var headerSize = Magic.Length + sizeof(int) + SaltSize + NonceSize + TagSize;
        if (fileContent.Length < headerSize || !LooksEncrypted(fileContent))
        {
            throw new InvalidNoteFileException("Keine gültige verschlüsselte QuickNote-Datei.");
        }

        var offset = Magic.Length;
        var iterations = BitConverter.ToInt32(fileContent, offset);
        offset += sizeof(int);
        var salt = fileContent.AsSpan(offset, SaltSize).ToArray();
        offset += SaltSize;
        var nonce = fileContent.AsSpan(offset, NonceSize).ToArray();
        offset += NonceSize;
        var tag = fileContent.AsSpan(offset, TagSize).ToArray();
        offset += TagSize;
        var cipherBytes = fileContent.AsSpan(offset).ToArray();
        var plainBytes = new byte[cipherBytes.Length];

        var key = DeriveKey(password, salt, iterations);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes, Magic);
        }
        catch (CryptographicException)
        {
            throw new InvalidPasswordException();
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    public static bool LooksEncrypted(byte[] fileContent)
        => fileContent.Length >= Magic.Length && fileContent.AsSpan(0, Magic.Length).SequenceEqual(Magic);

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
}
