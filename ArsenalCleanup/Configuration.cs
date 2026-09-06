using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Dalamud.Configuration;

namespace ArsenalCleanup;

[Serializable]
public class Configuration : IPluginConfiguration
{
    /// <summary>Schema version of this file, bumped when a stored setting changes meaning.</summary>
    public int Version { get; set; }

    /// <summary>
    /// Where exports are written, or empty to use the plugin's own config folder.
    ///
    /// Empty is the default on purpose: a hardcoded absolute path is meaningless on someone else's
    /// machine, and writing to a folder the player never chose is worse than writing somewhere
    /// boring but predictable. Set it from the config window (<c>/arsenalcleanupconfig</c>) to point
    /// exports at whatever tool consumes them.
    /// </summary>
    public string ExportDirectory { get; set; } = string.Empty;

    /// <summary>
    /// The directory an export will actually land in: the configured one when the player set it,
    /// otherwise <paramref name="defaultDirectory"/>.
    ///
    /// Whitespace counts as unset — an all-spaces path is a slip, not a choice, and
    /// <c>Directory.CreateDirectory</c> would throw on it at the least helpful moment.
    /// </summary>
    public static string ResolveExportDirectory(string? configured, string defaultDirectory) =>
        string.IsNullOrWhiteSpace(configured) ? defaultDirectory : configured;

    /// <summary>Where exports go when the player has not chosen a folder: an <c>exports</c>
    /// subfolder of the plugin's own Dalamud config directory.</summary>
    public static string DefaultExportDirectory(string pluginConfigDirectory) =>
        Path.Combine(pluginConfigDirectory, "exports");

    /// <summary>
    /// Whether to show the verdict panel next to the cursor when hovering a piece.
    ///
    /// On by default: it is the whole point of running this in game rather than reading a list. It
    /// is a setting because it draws over the game's own interface, and anything that does should be
    /// something the player can turn off.
    /// </summary>
    public bool ShowHoverPanel { get; set; } = true;

    /// <summary>Persists this configuration through Dalamud. Excluded from coverage: it is a
    /// single call into a service that only exists inside the game process.</summary>
    [ExcludeFromCodeCoverage]
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
