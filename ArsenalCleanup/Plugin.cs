using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ArsenalCleanup.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Inventory;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ArsenalCleanup;

/// <summary>
/// Dalamud entry point and the only place in this plugin that touches live game state. Everything
/// with a decision in it — which containers to read, how a slot is named, how a file is named and
/// written — lives in <see cref="ArmoryReader"/>, <see cref="ArmorySlot"/> and
/// <see cref="ExportWriter"/>, which are covered by tests. What remains here is wiring.
///
/// Excluded from coverage on purpose: every line either registers a Dalamud callback or forwards to
/// a tested type, and none of it can execute outside a running game. The extraction that emptied
/// this class is what makes that exclusion honest rather than convenient.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameInventory GameInventory { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string ExportCommand = "/arsenalcleanup";
    private const string ConfigCommand = "/arsenalcleanupconfig";

    /// <summary>
    /// The names this plugin answered to before it was renamed. Kept working, and hidden from the
    /// command list so they do not read as two separate features — a rename is not a reason to
    /// break the muscle memory of anyone already using it.
    /// </summary>
    private static readonly (string Name, bool IsExport)[] LegacyCommands =
    [
        ("/arsenalexport", true),
        ("/arsenalexportconfig", false),
    ];

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("ArsenalCleanup");
    private readonly ConfigWindow configWindow;
    private readonly string defaultExportDirectory;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        defaultExportDirectory = Configuration.DefaultExportDirectory(PluginInterface.ConfigDirectory.FullName);

        configWindow = new ConfigWindow(this, defaultExportDirectory);
        WindowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(ExportCommand, new CommandInfo(OnExportCommand)
        {
            HelpMessage = "Export the current character's Armoury Chest and worn gear to a JSON file " +
                          "(see /arsenalcleanupconfig for the destination folder).",
        });
        CommandManager.AddHandler(ConfigCommand, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Open Arsenal Cleanup settings (export folder).",
        });

        foreach (var (name, isExport) in LegacyCommands)
        {
            CommandManager.AddHandler(name, new CommandInfo(isExport ? OnExportCommand : OnConfigCommand)
            {
                HelpMessage = $"Renamed to {(isExport ? ExportCommand : ConfigCommand)}.",
                ShowInHelp = false,
            });
        }

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        configWindow.Dispose();

        CommandManager.RemoveHandler(ExportCommand);
        CommandManager.RemoveHandler(ConfigCommand);
        foreach (var (name, _) in LegacyCommands) CommandManager.RemoveHandler(name);
    }

    private void OnExportCommand(string command, string args) => ExportArmoury();

    private void OnConfigCommand(string command, string args) => ToggleConfigUi();

    public void ToggleConfigUi() => configWindow.Toggle();

    private void ExportArmoury()
    {
        if (!PlayerState.IsLoaded)
        {
            ChatGui.PrintError("Arsenal Cleanup: no character is logged in.");
            return;
        }

        var characterName = PlayerState.CharacterName ?? "unknown";
        var pieces = ArmoryReader.CollectPieces(new GameInventorySource());
        var export = ArmoryExportBuilder.Build(characterName, DateTimeOffset.Now, pieces);
        var directory = Configuration.ResolveExportDirectory(Configuration.ExportDirectory, defaultExportDirectory);

        try
        {
            var path = ExportWriter.Write(directory, export);

            var wornCount = pieces.FindAll(p => p.IsEquipped).Count;
            ChatGui.Print($"Arsenal Cleanup: {export.Pieces.Length} piece(s) for {characterName} " +
                          $"({wornCount} worn) written to {path}.");
            Log.Information("Arsenal export written to {Path} ({Count} pieces).", path, export.Pieces.Length);
        }
        catch (Exception ex)
        {
            // The player can only act on this if they are told which folder failed, so the path is
            // part of the message rather than buried in the log.
            ChatGui.PrintError($"Arsenal Cleanup: could not write to \"{directory}\" ({ex.Message}). " +
                               "Check the folder in /arsenalcleanupconfig.");
            Log.Error(ex, "Arsenal export failed for directory {Directory}.", directory);
        }
    }

    /// <summary>The one real <see cref="IInventorySource"/>: Dalamud's live inventory view, with
    /// empty slots dropped and each item reduced to plain data.</summary>
    private sealed class GameInventorySource : IInventorySource
    {
        public IEnumerable<RawArmoryPiece> Read(GameInventoryType type)
        {
            // Dalamud hands back a ReadOnlySpan over game memory, which cannot cross a `yield`
            // boundary and must not outlive this call — so the container is copied out eagerly.
            var pieces = new List<RawArmoryPiece>();
            foreach (var item in GameInventory.GetInventoryItems(type))
            {
                if (item.IsEmpty) continue;
                pieces.Add(new RawArmoryPiece(item.BaseItemId, item.Quantity, item.IsHq, item.IsCollectable, (int)item.InventorySlot));
            }
            return pieces;
        }
    }
}
