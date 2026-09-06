using System.Collections.Generic;
using Dalamud.Game.Inventory;

namespace ArsenalCleanup;

/// <summary>
/// The vocabulary of equipment slots used throughout an export, and the mapping from the game's two
/// very different container layouts onto it.
///
/// Slot names are lowercase ASCII identifiers, not display text: they are a stable key for whatever
/// reads the export, and they never change with the player's client language.
/// </summary>
public static class ArmorySlot
{
    /// <summary>
    /// The twelve Armoury Chest containers to read, in slot order.
    ///
    /// <c>ArmoryWaist</c> is deliberately absent: belts were removed from the game, so that
    /// container can never hold anything.
    /// </summary>
    public static readonly IReadOnlyList<GameInventoryType> ArmoryTypes =
    [
        GameInventoryType.ArmoryMainHand,
        GameInventoryType.ArmoryOffHand,
        GameInventoryType.ArmoryHead,
        GameInventoryType.ArmoryBody,
        GameInventoryType.ArmoryHands,
        GameInventoryType.ArmoryLegs,
        GameInventoryType.ArmoryFeets,
        GameInventoryType.ArmoryEar,
        GameInventoryType.ArmoryNeck,
        GameInventoryType.ArmoryWrist,
        GameInventoryType.ArmoryRings,
        GameInventoryType.ArmorySoulCrystal,
    ];

    /// <summary>
    /// Slot name for a piece read from <see cref="GameInventoryType.EquippedItems"/>, keyed by its
    /// index in that container, or null when the index holds nothing worth exporting.
    ///
    /// Equipped gear is one flat 14-entry array whose order is the game's own — the same order as
    /// FFXIVClientStructs' <c>RaptureGearsetModule.GearsetItemIndex</c>, from main hand at 0 to soul
    /// crystal at 13. Unlike an Armoury Chest tab, the container type says nothing about which slot
    /// a piece occupies, so the index is the only slot signal available.
    ///
    /// Index 5 (belt) maps to nothing, for the same reason <c>ArmoryWaist</c> is skipped above.
    /// </summary>
    public static string? ResolveEquipped(int equippedIndex) => equippedIndex switch
    {
        0 => "main",
        1 => "offhand",
        2 => "head",
        3 => "body",
        4 => "hand",
        6 => "legs",
        7 => "feet",
        8 => "ear",
        9 => "neck",
        10 => "wrist",
        11 => "ring",
        12 => "ring",
        13 => "soul_crystal",
        _ => null,
    };

    /// <summary>Slot name for an Armoury Chest container, or null if it is not one of the twelve
    /// listed in <see cref="ArmoryTypes"/>.</summary>
    public static string? Resolve(GameInventoryType type) => type switch
    {
        GameInventoryType.ArmoryMainHand => "main",
        GameInventoryType.ArmoryOffHand => "offhand",
        GameInventoryType.ArmoryHead => "head",
        GameInventoryType.ArmoryBody => "body",
        GameInventoryType.ArmoryHands => "hand",
        GameInventoryType.ArmoryLegs => "legs",
        GameInventoryType.ArmoryFeets => "feet",
        GameInventoryType.ArmoryEar => "ear",
        GameInventoryType.ArmoryNeck => "neck",
        GameInventoryType.ArmoryWrist => "wrist",
        GameInventoryType.ArmoryRings => "ring",
        GameInventoryType.ArmorySoulCrystal => "soul_crystal",
        _ => null,
    };
}
