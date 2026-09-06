using System.Collections.Generic;
using Dalamud.Game.Inventory;

namespace ArsenalCleanup;

/// <summary>
/// Where inventory rows come from. The game is behind this interface for one reason: it lets the
/// collection logic below — which containers to visit, how a slot name is derived, what is skipped
/// — be tested without a running game, while <see cref="Plugin"/> keeps the single real
/// implementation over Dalamud's <c>IGameInventory</c>.
/// </summary>
public interface IInventorySource
{
    /// <summary>Every non-empty item in one container, in the game's own slot order.</summary>
    IEnumerable<RawArmoryPiece> Read(GameInventoryType type);
}

/// <summary>
/// Walks the containers that make up "everything this character owns that can be worn": the twelve
/// Armoury Chest tabs, then the gear actually on the character.
/// </summary>
public static class ArmoryReader
{
    /// <summary>
    /// Collects every wearable piece the character owns, tagged with its canonical slot name and
    /// with whether it was being worn.
    ///
    /// The equipped pass is not a nicety: gear on the active job lives in its own container and
    /// never appears in an Armoury tab, so an export built from the tabs alone silently omits, for
    /// each slot, the piece most likely to be the best one owned. A consumer comparing item levels
    /// would then crown a strictly inferior piece "best available" with no way to notice.
    ///
    /// Anything whose container index maps to no canonical slot is skipped rather than guessed at —
    /// see <see cref="ArmorySlot.ResolveEquipped"/> for the one real case (the removed belt slot).
    /// </summary>
    public static List<SlottedArmoryPiece> CollectPieces(IInventorySource inventory)
    {
        var collected = new List<SlottedArmoryPiece>();

        foreach (var containerType in ArmorySlot.ArmoryTypes)
        {
            var slot = ArmorySlot.Resolve(containerType);
            if (slot is null) continue; // defensive — ArmoryTypes only lists resolvable containers

            foreach (var piece in inventory.Read(containerType))
            {
                collected.Add(new SlottedArmoryPiece(slot, IsEquipped: false, piece));
            }
        }

        foreach (var piece in inventory.Read(GameInventoryType.EquippedItems))
        {
            var slot = ArmorySlot.ResolveEquipped(piece.InventorySlot);
            if (slot is null) continue;
            collected.Add(new SlottedArmoryPiece(slot, IsEquipped: true, piece));
        }

        return collected;
    }
}
