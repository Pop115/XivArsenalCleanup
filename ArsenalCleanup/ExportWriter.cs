using System;
using System.IO;
using System.Text.Json;

namespace ArsenalCleanup;

/// <summary>
/// Turns an export document into a file on disk. Separate from <see cref="Plugin"/> so the naming
/// and writing rules can be tested against a real temporary directory without a running game.
/// </summary>
public static class ExportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// File name for one export: <c>yyyy-MM-dd-Character-Name-arsenal.json</c>.
    ///
    /// One file per character per day, and a name that sorts chronologically in any file browser.
    /// Deliberately *not* unique per run: re-exporting the same character on the same day
    /// overwrites that day's file rather than piling up near-identical snapshots, while yesterday's
    /// export stays untouched. Consumers are expected to keep the most recent file per character.
    /// </summary>
    public static string BuildFileName(DateTimeOffset exportedAt, string characterName) =>
        $"{exportedAt:yyyy-MM-dd}-{SanitizeForFileName(characterName)}-arsenal.json";

    /// <summary>
    /// Serialises <paramref name="export"/> into <paramref name="directory"/>, creating the
    /// directory if needed, and returns the full path written.
    ///
    /// Throws whatever the filesystem throws (missing drive, permissions, path too long): the
    /// caller is a UI layer that can tell the player what went wrong, and swallowing the reason
    /// here would leave them with an export that silently never happened.
    /// </summary>
    public static string Write(string directory, ArmoryExportFile export)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, BuildFileName(export.ExportedAt, export.Character));
        File.WriteAllText(path, JsonSerializer.Serialize(export, SerializerOptions));
        return path;
    }

    /// <summary>
    /// Makes a character name safe to embed in a file name: spaces become hyphens (so the result
    /// stays one shell-friendly token) and anything the platform rejects becomes an underscore.
    ///
    /// FFXIV names are letters, apostrophes and hyphens today, but a name arriving from the game is
    /// still outside data — treating it as trusted is how a path traversal or an unwritable file
    /// name gets in.
    /// </summary>
    public static string SanitizeForFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == ' ') { chars[i] = '-'; continue; }
            if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
        }
        return new string(chars);
    }
}
