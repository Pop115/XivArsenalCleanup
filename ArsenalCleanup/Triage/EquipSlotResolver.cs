namespace ArsenalCleanup.Triage;

/// <summary>
/// One flag per equipment slot, read straight from an EquipSlotCategory row: 1 means the item
/// occupies that slot, -1 means it blocks it (a two-handed weapon blocking the off hand, or a full
/// body piece blocking head/gloves/legs/feet), 0 means unrelated.
/// </summary>
public readonly record struct EquipSlotFlags(
    sbyte MainHand,
    sbyte OffHand,
    sbyte Head,
    sbyte Body,
    sbyte Gloves,
    sbyte Waist,
    sbyte Legs,
    sbyte Feet,
    sbyte Ears,
    sbyte Neck,
    sbyte Wrists,
    sbyte FingerL,
    sbyte FingerR,
    sbyte SoulCrystal);

/// <summary>
/// Maps an item's EquipSlotCategory to the one slot the Armoury Chest files it under.
///
/// The order matters: a piece that occupies one slot and blocks others (a two-hander, a full body
/// piece) must be filed under the slot it occupies, never one it merely blocks — which is why only
/// a flag of exactly 1 counts.
/// </summary>
public static class EquipSlotResolver
{
    /// <summary>Returns the slot name, or null when the row maps to nothing this plugin triages —
    /// in practice the legacy waist slot (belts were removed from the game) or a reserved row.</summary>
    public static string? Resolve(EquipSlotFlags flags)
    {
        if (flags.MainHand == 1) return "main";
        if (flags.OffHand == 1) return "offhand";
        if (flags.Head == 1) return "head";
        if (flags.Body == 1) return "body";
        if (flags.Gloves == 1) return "hand";
        if (flags.Legs == 1) return "legs";
        if (flags.Feet == 1) return "feet";
        if (flags.Ears == 1) return "ear";
        if (flags.Neck == 1) return "neck";
        if (flags.Wrists == 1) return "wrist";
        if (flags.FingerL == 1 || flags.FingerR == 1) return "ring";
        if (flags.SoulCrystal == 1) return "soul_crystal";
        return null;
    }
}
