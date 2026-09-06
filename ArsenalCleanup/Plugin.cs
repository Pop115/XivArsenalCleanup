using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ArsenalCleanup.GameData;
using ArsenalCleanup.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Inventory;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ArsenalCleanup;

/// <summary>
/// Dalamud entry point, and the only place in this plugin that touches live game state. Everything
/// with a decision in it — which containers to visit, how a slot is named, whether a piece is still
/// worth keeping, how a file is named and written — lives in <see cref="Triage"/>,
/// <see cref="ArmoryReader"/>, <see cref="ArmorySlot"/> and <see cref="ExportWriter"/>, all covered
/// by tests. What remains here is wiring.
///
/// Excluded from coverage: every line either registers a Dalamud callback or forwards to a tested
/// type, and none of it can execute outside a running game. The extraction that emptied this class
/// is what makes that exclusion honest rather than convenient.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameInventory GameInventory { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string MainCommand = "/arsenalcleanup";
    private const string ConfigCommand = "/arsenalcleanupconfig";

    /// <summary>
    /// The names this plugin answered to before it was renamed and grew a window of its own. They
    /// still do exactly what they always did — write the JSON export — because someone who wired a
    /// tool around that command should not have it change meaning under them. Hidden from the
    /// command list so they do not read as separate features.
    /// </summary>
    private static readonly string[] LegacyExportCommands = ["/arsenalexport"];
    private static readonly string[] LegacyConfigCommands = ["/arsenalexportconfig"];

    public Configuration Configuration { get; init; }

    public WindowSystem WindowSystem { get; } = new("ArsenalCleanup");
    private readonly ConfigWindow configWindow;
    private readonly MainWindow mainWindow;
    private readonly ArsenalService arsenal;
    private readonly string defaultExportDirectory;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        defaultExportDirectory = Configuration.DefaultExportDirectory(PluginInterface.ConfigDirectory.FullName);

        arsenal = new ArsenalService(new GameInventorySource(), DataManager, PlayerState, Log);

        configWindow = new ConfigWindow(this, defaultExportDirectory);
        mainWindow = new MainWindow(arsenal, Configuration, defaultExportDirectory, () => configWindow.Toggle());
        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(MainCommand, new CommandInfo(OnMainCommand)
        {
            HelpMessage = "Open Arsenal Cleanup, which sorts your Armoury Chest into what is worth "
                        + "keeping and what is not. Use \"/arsenalcleanup export\" to write the JSON export instead.",
        });
        CommandManager.AddHandler(ConfigCommand, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Open Arsenal Cleanup settings (export folder).",
        });

        foreach (var name in LegacyExportCommands)
        {
            CommandManager.AddHandler(name, new CommandInfo(OnLegacyExportCommand)
            {
                HelpMessage = $"Writes the JSON export. Renamed to \"{MainCommand} export\".",
                ShowInHelp = false,
            });
        }
        foreach (var name in LegacyConfigCommands)
        {
            CommandManager.AddHandler(name, new CommandInfo(OnConfigCommand)
            {
                HelpMessage = $"Renamed to {ConfigCommand}.",
                ShowInHelp = false,
            });
        }

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        configWindow.Dispose();
        mainWindow.Dispose();

        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(ConfigCommand);
        foreach (var name in LegacyExportCommands) CommandManager.RemoveHandler(name);
        foreach (var name in LegacyConfigCommands) CommandManager.RemoveHandler(name);
    }

    private void OnMainCommand(string command, string args)
    {
        if (args.Trim().Equals("export", StringComparison.OrdinalIgnoreCase))
        {
            ExportToChat();
            return;
        }

        ToggleMainUi();
    }

    private void OnLegacyExportCommand(string command, string args) => ExportToChat();

    private void OnConfigCommand(string command, string args) => ToggleConfigUi();

    public void ToggleConfigUi() => configWindow.Toggle();

    private void ToggleMainUi()
    {
        // Opening the window with nothing in it would look broken, so the first open reads the
        // inventory itself. Later opens keep whatever was there until Refresh is pressed.
        if (!mainWindow.IsOpen && arsenal.LastRefreshed is null) arsenal.Refresh();
        mainWindow.Toggle();
    }

    /// <summary>Writes the export and reports the outcome in chat — the command-line path, where
    /// there is no window to show a result in.</summary>
    private void ExportToChat()
    {
        if (!PlayerState.IsLoaded)
        {
            ChatGui.PrintError("Arsenal Cleanup: no character is logged in.");
            return;
        }

        arsenal.Refresh();
        var directory = Configuration.ResolveExportDirectory(Configuration.ExportDirectory, defaultExportDirectory);

        try
        {
            var path = arsenal.Export(directory);
            var worn = 0;
            foreach (var piece in arsenal.LastRead) if (piece.IsEquipped) worn++;

            ChatGui.Print($"Arsenal Cleanup: {arsenal.LastRead.Count} piece(s) ({worn} worn) written to {path}.");
        }
        catch (Exception ex)
        {
            // The player can only act on this if they are told which folder failed, so the path is
            // part of the message rather than buried in the log.
            ChatGui.PrintError($"Arsenal Cleanup: could not write to \"{directory}\" ({ex.Message}). " +
                               $"Check the folder in {ConfigCommand}.");
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
