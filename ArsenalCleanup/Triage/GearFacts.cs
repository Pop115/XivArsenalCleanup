using System.Collections.Generic;

namespace ArsenalCleanup.Triage;

/// <summary>
/// Everything the triage needs to know about an item, and nothing else.
///
/// This is deliberately a plain record rather than a Lumina row: the rules below have to run in a
/// unit test, and a Lumina row cannot be constructed outside a loaded game. Filling it from the
/// game's sheets is <c>GameData/ItemFactsResolver</c>'s job.
/// </summary>
/// <param name="ItemLevel">The item's own level — the yardstick for "better".</param>
/// <param name="EquipLevel">The character level required to equip it.</param>
/// <param name="JobIds">Every ClassJob row id that may equip it. Empty when unresolved.</param>
/// <param name="DefencePhysical">
/// The piece's own physical defence (<c>Item.DefensePhys</c>), zero on weapons and accessories.
/// Item level alone cannot stand in for it: two pieces of the same item level made for different
/// roles defend differently, which is the whole reason Beastmaster is judged on this instead.
/// </param>
/// <param name="DefenceMagical">The piece's magical defence (<c>Item.DefenseMag</c>).</param>
/// <param name="IsRelic">
/// True for an item from a relic line. Detected from the description opening with a bracketed tag
/// (`[Manderville Weapon]`), the convention Square Enix has used across every relic era checked —
/// ordinary raid gear never uses it. A relic is never suggested for disposal, however confident the
/// item-level comparison is.
/// </param>
/// <param name="IsDesynthesisable">Item.Desynth > 0 — it can be broken down for materials.</param>
/// <param name="IsArmoireEligible">It appears in the game's Cabinet sheet, so the Armoire can store it forever.</param>
public sealed record GearFacts(
    uint ItemId,
    string Name,
    string Slot,
    ushort ItemLevel,
    byte EquipLevel,
    ushort IconId,
    IReadOnlyList<uint> JobIds,
    ushort DefencePhysical,
    ushort DefenceMagical,
    bool IsRelic,
    bool IsDesynthesisable,
    bool IsArmoireEligible);

/// <summary>One piece the character owns: where it was found, plus what the game says about it.</summary>
/// <param name="Facts">Null when the item id resolved to nothing in the game's sheets.</param>
public sealed record OwnedPiece(uint ItemId, string Slot, int Position, bool IsEquipped, GearFacts? Facts);

/// <summary>What the triage concluded about one piece.</summary>
public enum Verdict
{
    /// <summary>The best of its slot that the character can equip today.</summary>
    KeptBest,

    /// <summary>The only one owned for that slot and job — kept whatever its level.</summary>
    KeptOnly,

    /// <summary>Not equippable yet, and nothing owned supersedes it. Keep it until the job catches up.</summary>
    KeptUntilLevel,

    /// <summary>Something strictly better is owned and usable. Safe to let go.</summary>
    Obsolete,

    /// <summary>Judged on incomplete information, or protected on purpose. Never an automatic decision.</summary>
    NeedsReview,
}

/// <summary>
/// How to get rid of an obsolete piece, best first: the Armoire stores it forever at no cost,
/// desynthesis destroys it but returns materials, and discarding returns nothing at all.
/// </summary>
public enum DisposalMethod
{
    None,
    Armoire,
    Desynthesis,
    Discard,
}
