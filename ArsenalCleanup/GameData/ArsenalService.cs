using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ArsenalCleanup.Triage;
using Dalamud.Plugin.Services;

namespace ArsenalCleanup.GameData;

/// <summary>
/// Holds the current triage and knows how to rebuild it from the live game.
///
/// The window renders whatever this holds and never reads the game itself, so a redraw — which
/// happens every frame — never walks the inventory or the sheets. A refresh is an explicit act.
///
/// Excluded from coverage: every line either calls a Dalamud service or forwards to a tested pure
/// function.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ArsenalService(
    IInventorySource inventory,
    IDataManager data,
    IPlayerState playerState,
    IPluginLog log)
{
    private readonly GameItemFacts itemFacts = new(data);

    /// <summary>Raised after every refresh, successful or not, so anything displaying a count from
    /// <see cref="Current"/> updates with it instead of drifting.</summary>
    public event Action? Refreshed;

    public TriageResult Current { get; private set; } = TriageResult.Empty;

    /// <summary>What went wrong on the last refresh, or null when it worked.</summary>
    public string? LastError { get; private set; }

    /// <summary>When the current result was produced, or null if it never was.</summary>
    public DateTimeOffset? LastRefreshed { get; private set; }

    /// <summary>The pieces the last refresh read, kept so an export writes exactly what is on screen.</summary>
    public IReadOnlyList<SlottedArmoryPiece> LastRead { get; private set; } = [];

    public void Refresh()
    {
        if (!playerState.IsLoaded)
        {
            LastError = "No character is logged in.";
            return;
        }

        try
        {
            var read = ArmoryReader.CollectPieces(inventory);
            var jobLevels = GameJobLevels.Read(data, playerState);

            var owned = new List<OwnedPiece>(read.Count);
            foreach (var slotted in read)
            {
                owned.Add(new OwnedPiece(
                    slotted.Piece.BaseItemId,
                    slotted.Slot,
                    slotted.Piece.InventorySlot,
                    slotted.IsEquipped,
                    itemFacts.For(slotted.Piece.BaseItemId)));
            }

            LastRead = read;
            Current = ArsenalTriage.Run(owned, jobLevels);
            LastRefreshed = DateTimeOffset.Now;
            LastError = null;

            log.Information("Triaged {Count} piece(s) for {Character}.", Current.Pieces.Count, playerState.CharacterName);
        }
        catch (Exception ex)
        {
            // A refresh failing must not take the window down with it: the player keeps whatever
            // was on screen, plus a line saying what happened.
            LastError = ex.Message;
            log.Error(ex, "Arsenal triage failed.");
        }
        finally
        {
            Refreshed?.Invoke();
        }
    }

    /// <summary>Writes the last read inventory to disk and returns the path.</summary>
    public string Export(string directory)
    {
        var export = ArmoryExportBuilder.Build(
            playerState.CharacterName ?? "unknown", DateTimeOffset.Now, LastRead);
        return ExportWriter.Write(directory, export);
    }
}
