using System.Numerics;
using ArsenalCleanup.Triage;

namespace ArsenalCleanup.Windows;

/// <summary>
/// Turns the triage's internal vocabulary into what the player reads.
///
/// Kept pure and separate from the window so no raw enum name or slot key can ever reach the screen
/// by accident: every value shown passes through here, and a value with no wording of its own gets
/// a sensible one rather than its identifier.
/// </summary>
public static class Presentation
{
    /// <summary>Slot order the game itself uses in the Armoury Chest, so the tabs read familiarly.</summary>
    public static readonly string[] SlotOrder =
    [
        "main", "offhand", "head", "body", "hand", "legs", "feet", "ear", "neck", "wrist", "ring",
    ];

    public static string SlotLabel(string slot) => slot switch
    {
        "main" => "Main hand",
        "offhand" => "Off hand",
        "head" => "Head",
        "body" => "Body",
        "hand" => "Hands",
        "legs" => "Legs",
        "feet" => "Feet",
        "ear" => "Earrings",
        "neck" => "Necklace",
        "wrist" => "Bracelets",
        "ring" => "Rings",
        "soul_crystal" => "Soul crystals",
        _ => Humanise(slot),
    };

    public static string VerdictLabel(Verdict verdict) => verdict switch
    {
        Verdict.KeptBest => "Best you can wear",
        Verdict.KeptOnly => "Only one you own",
        Verdict.KeptUntilLevel => "Keep — level not reached",
        Verdict.Obsolete => "Replaced",
        Verdict.NeedsReview => "Check yourself",
        _ => Humanise(verdict.ToString()),
    };

    public static string DisposalLabel(DisposalMethod disposal) => disposal switch
    {
        DisposalMethod.Armoire => "Store in the Armoire",
        DisposalMethod.Desynthesis => "Desynthesise",
        DisposalMethod.Discard => "Discard",
        _ => string.Empty,
    };

    /// <summary>
    /// Colour per verdict. "Replaced" is deliberately not red: finding something to clear out is the
    /// point of the plugin, so it reads as progress, and red is kept for the one row that actually
    /// needs the player's judgement.
    /// </summary>
    public static Vector4 VerdictColour(Verdict verdict) => verdict switch
    {
        Verdict.KeptBest => new Vector4(0.42f, 0.78f, 0.47f, 1f),
        Verdict.KeptOnly => new Vector4(0.72f, 0.74f, 0.78f, 1f),
        Verdict.KeptUntilLevel => new Vector4(0.45f, 0.68f, 0.92f, 1f),
        Verdict.Obsolete => new Vector4(0.95f, 0.72f, 0.35f, 1f),
        Verdict.NeedsReview => new Vector4(0.92f, 0.45f, 0.45f, 1f),
        _ => new Vector4(1f, 1f, 1f, 1f),
    };

    /// <summary>A last resort for a value with no wording of its own: "soul_crystal" reads as "Soul crystal", never as itself.</summary>
    public static string Humanise(string raw)
    {
        if (raw.Length == 0) return raw;
        var spaced = raw.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }
}
