using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ArsenalCleanup.Triage;

namespace ArsenalCleanup.Windows;

/// <summary>
/// Turns the triage's internal vocabulary into what the player reads.
///
/// Every word on screen comes from here, which is what keeps a raw enum name or a slot key from
/// reaching the player by accident — and what would make translating the plugin a matter of
/// changing one file rather than hunting sentences through the decision logic.
///
/// Class names are shown as the abbreviations the player already sees in game, in their own
/// language: DNS rather than DNC on a French client.
/// </summary>
public static class Presentation
{
    /// <summary>Slot order the game itself uses in the Armoury Chest, so the tabs read familiarly.</summary>
    public static readonly string[] SlotOrder =
    [
        "main", "offhand", "head", "body", "hand", "legs", "feet", "ear", "neck", "wrist", "ring",
    ];

    /// <summary>
    /// How many classes to name before summarising. Past a handful the list stops being read and
    /// starts being scrolled — and a piece usable by thirty classes is telling you something about
    /// the piece, not about the thirty.
    /// </summary>
    private const int MaxNamedJobs = 6;

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

    /// <summary>
    /// The classes a verdict is about, written the way the player would say it.
    ///
    /// "every class" when it really is all of them — a level-1 ring usable by the whole game should
    /// say so, not list forty abbreviations — and a trailing count past a handful, because a list
    /// nobody finishes reading is no clearer than no list at all.
    /// </summary>
    public static string JobList(IReadOnlyList<uint> jobIds, JobDirectory jobs)
    {
        if (jobIds.Count == 0) return string.Empty;
        if (jobs.Count > 0 && jobIds.Count >= jobs.Count) return "every class";

        var named = jobIds.Take(MaxNamedJobs).Select(jobs.Display);
        var listed = string.Join(", ", named);
        var remaining = jobIds.Count - MaxNamedJobs;
        return remaining > 0 ? $"{listed} +{remaining} more" : listed;
    }

    /// <summary>
    /// The sentence explaining a verdict, naming the classes it concerns.
    ///
    /// The previous wording said "this job" throughout, which is accurate and useless: a player
    /// looking at a piece usable by nine classes wants to know which of theirs is waiting on it.
    /// </summary>
    public static string Reason(TriagedPiece piece, JobDirectory jobs)
    {
        var who = JobList(piece.RelevantJobs, jobs);
        var replacement = piece.ReplacedBy;

        var sentence = piece.Reason switch
        {
            ReasonCode.OnlyOneOwned when who.Length > 0 =>
                $"The only one you own for this slot ({who}).",
            ReasonCode.OnlyOneOwned =>
                "The only one you own for this slot.",

            ReasonCode.BestEquippable when who.Length > 0 =>
                $"Your best for this slot, for {who}.",
            ReasonCode.BestEquippable =>
                "Your best for this slot.",

            ReasonCode.NotAtLevelYet when who.Length > 0 && piece.RequiredLevel is { } lvl =>
                $"Keep for {who}: wearable from level {lvl}, and none of them are there yet.",
            ReasonCode.NotAtLevelYet when piece.RequiredLevel is { } lvlOnly =>
                $"Keep: wearable from level {lvlOnly}, not reached yet.",
            ReasonCode.NotAtLevelYet =>
                "Keep until the level is reached.",

            ReasonCode.SupersededNow when replacement is not null =>
                $"Replaced by {replacement.Name} (item level {replacement.ItemLevel}), which you can already wear.",
            ReasonCode.SupersededNow =>
                "Replaced by something better you can already wear.",

            ReasonCode.OutclassedByEarlier when replacement is not null =>
                $"Outclassed by {replacement.Name}, which unlocks no later (level {replacement.EquipLevel}).",
            ReasonCode.OutclassedByEarlier =>
                "Outclassed by something that unlocks no later.",

            ReasonCode.JobLevelUnknown when who.Length > 0 =>
                $"Cannot judge this: the level of {who} could not be read.",
            ReasonCode.JobLevelUnknown =>
                "Cannot judge this: the class level could not be read.",

            ReasonCode.ItemUnknown =>
                "The game's item data has no entry for this, so nothing is known about it.",

            ReasonCode.JobUnresolved =>
                "No class could be resolved for this item, so there is nothing to compare it against.",

            _ => Humanise(piece.Reason.ToString()),
        };

        // The guard that keeps a relic out of the bin has to explain itself, or a perfectly
        // identified weapon just looks unrecognised.
        return piece.Facts.IsRelic && replacement is not null
            ? $"{sentence} Relic: never disposed of automatically — your call."
            : sentence;
    }

    /// <summary>A last resort for a value with no wording of its own: "soul_crystal" reads as "Soul crystal", never as itself.</summary>
    public static string Humanise(string raw)
    {
        if (raw.Length == 0) return raw;
        var spaced = raw.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }
}
