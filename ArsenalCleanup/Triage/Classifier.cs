using System.Collections.Generic;
using System.Linq;

namespace ArsenalCleanup.Triage;

/// <summary>One piece judged for one job that can use it.</summary>
/// <param name="ReplacedBy">The piece that supersedes it — set only when the verdict is <see cref="Verdict.Obsolete"/>, or when a relic guard downgraded that verdict.</param>
public sealed record JudgedPiece(
    OwnedPiece Piece,
    GearFacts Facts,
    uint JobId,
    int? JobLevel,
    Verdict Verdict,
    GearFacts? ReplacedBy,
    string Reason,
    DisposalMethod Disposal);

/// <summary>
/// Decides, for every piece owned, whether it is still worth keeping.
///
/// Pieces are grouped by (slot, job): an item several jobs can equip — shared accessories, or a
/// base class and its job — is judged once per job, because "obsolete" can be true for one and
/// false for another. Merging those judgements back into one row per physical piece is
/// <see cref="PieceMerger"/>'s job.
///
/// The rule this exists for: a piece is only obsolete when a strictly better one is *also
/// equippable at the character's current level*. Comparing item levels alone is what makes a naive
/// triage recommend throwing away a level-32 bow in favour of a level-90 one the character cannot
/// hold yet — leaving them weaponless for 49 levels.
///
/// Soul crystals are the caller's business: they are never gear to triage and should be filtered
/// out before calling. Relics are this function's business — an obsolete verdict on one is always
/// downgraded to <see cref="Verdict.NeedsReview"/>, with the full "replaced by" context kept so the
/// player can still see *why* it would have gone.
/// </summary>
public static class Classifier
{
    public static List<JudgedPiece> Classify(
        IReadOnlyList<OwnedPiece> pieces,
        IReadOnlyDictionary<uint, int> jobLevels)
    {
        var results = new List<JudgedPiece>();

        foreach (var piece in pieces)
        {
            if (piece.Facts is null || piece.Facts.JobIds.Count == 0)
            {
                results.Add(Unresolved(piece));
            }
        }

        foreach (var group in GroupBySlotAndJob(pieces))
        {
            if (group.Value.Count == 1)
            {
                var only = group.Value[0];
                results.Add(Judge(only, group.Key.JobId, jobLevels, Verdict.KeptOnly, null,
                    "The only piece owned for this slot and job."));
                continue;
            }

            foreach (var entry in group.Value)
            {
                results.Add(JudgeWithinGroup(entry, group.Value, group.Key.JobId, jobLevels));
            }
        }

        return results;
    }

    private readonly record struct GroupKey(string Slot, uint JobId);

    private static Dictionary<GroupKey, List<OwnedPiece>> GroupBySlotAndJob(IReadOnlyList<OwnedPiece> pieces)
    {
        var groups = new Dictionary<GroupKey, List<OwnedPiece>>();
        foreach (var piece in pieces)
        {
            if (piece.Facts is null) continue;
            foreach (var jobId in piece.Facts.JobIds)
            {
                var key = new GroupKey(piece.Facts.Slot, jobId);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
                list.Add(piece);
            }
        }
        return groups;
    }

    private static JudgedPiece JudgeWithinGroup(
        OwnedPiece piece,
        List<OwnedPiece> group,
        uint jobId,
        IReadOnlyDictionary<uint, int> jobLevels)
    {
        var facts = piece.Facts!;

        if (!jobLevels.TryGetValue(jobId, out var level))
        {
            return Judge(piece, jobId, jobLevels, Verdict.NeedsReview, null,
                "This job's level is unknown, so there is nothing to judge against.");
        }

        var others = group.Where(p => !ReferenceEquals(p, piece)).ToList();

        // Beaten by something strictly better that can be equipped right now.
        var supersededNow = Best(others.Where(o =>
            o.Facts!.ItemLevel > facts.ItemLevel && o.Facts.EquipLevel <= level));
        if (supersededNow is not null)
        {
            return Judge(piece, jobId, jobLevels, Verdict.Obsolete, supersededNow,
                $"Replaced by {supersededNow.Name} (item level {supersededNow.ItemLevel}), which is already equippable.");
        }

        if (facts.EquipLevel > level)
        {
            // Not reachable yet. Only truly obsolete if a future piece dominates it: strictly
            // better, and available at least as early — otherwise this one still bridges the gap.
            var dominated = Best(others.Where(o =>
                o.Facts!.ItemLevel > facts.ItemLevel && o.Facts.EquipLevel <= facts.EquipLevel));
            if (dominated is not null)
            {
                return Judge(piece, jobId, jobLevels, Verdict.Obsolete, dominated,
                    $"Outclassed by {dominated.Name}, which unlocks at least as early (level {dominated.EquipLevel}).");
            }

            return Judge(piece, jobId, jobLevels, Verdict.KeptUntilLevel, null,
                $"Keep until this job reaches level {facts.EquipLevel} (currently {level}).");
        }

        return Judge(piece, jobId, jobLevels, Verdict.KeptBest, null,
            "The best piece currently equippable for this slot and job.");
    }

    /// <summary>
    /// The single best candidate among pieces that already beat the one being judged, or null.
    ///
    /// "Best" is the highest item level; ties go to whichever unlocks earliest, then to the lowest
    /// item id so the answer never depends on the order the inventory happened to be read in.
    ///
    /// Taking the maximum rather than the first match is what makes the replacement shown the one a
    /// player would actually name. The verdict does not depend on it — any candidate at all proves
    /// the piece is obsolete — but the name and icon are what a human reads to decide.
    /// </summary>
    private static GearFacts? Best(IEnumerable<OwnedPiece> candidates)
    {
        GearFacts? champion = null;
        foreach (var challenger in candidates.Select(c => c.Facts!))
        {
            if (champion is null) { champion = challenger; continue; }
            if (challenger.ItemLevel != champion.ItemLevel)
            {
                if (challenger.ItemLevel > champion.ItemLevel) champion = challenger;
                continue;
            }
            if (challenger.EquipLevel != champion.EquipLevel)
            {
                if (challenger.EquipLevel < champion.EquipLevel) champion = challenger;
                continue;
            }
            if (challenger.ItemId < champion.ItemId) champion = challenger;
        }
        return champion;
    }

    private static JudgedPiece Judge(
        OwnedPiece piece,
        uint jobId,
        IReadOnlyDictionary<uint, int> jobLevels,
        Verdict verdict,
        GearFacts? replacedBy,
        string reason)
    {
        var facts = piece.Facts!;

        // A relic is never suggested for the bin, however confident the comparison is — but the
        // reason it would have gone is kept, so the guard prompts a decision instead of hiding one.
        var guarded = verdict == Verdict.Obsolete && facts.IsRelic;
        var finalVerdict = guarded ? Verdict.NeedsReview : verdict;

        return new JudgedPiece(
            piece,
            facts,
            jobId,
            jobLevels.TryGetValue(jobId, out var level) ? level : null,
            finalVerdict,
            replacedBy,
            guarded ? $"{reason} Relic: never disposed of automatically — confirm first." : reason,
            finalVerdict == Verdict.Obsolete ? DisposalFor(facts) : DisposalMethod.None);
    }

    /// <summary>
    /// How to part with an obsolete piece. The Armoire keeps the appearance forever and costs
    /// nothing, so it beats desynthesis, which destroys the item but returns materials, which in
    /// turn beats discarding it for nothing.
    /// </summary>
    public static DisposalMethod DisposalFor(GearFacts facts)
    {
        if (facts.IsArmoireEligible) return DisposalMethod.Armoire;
        if (facts.IsDesynthesisable) return DisposalMethod.Desynthesis;
        return DisposalMethod.Discard;
    }

    private static JudgedPiece Unresolved(OwnedPiece piece)
    {
        var facts = piece.Facts;
        return new JudgedPiece(
            piece,
            facts ?? new GearFacts(piece.ItemId, $"Unknown item ({piece.ItemId})", piece.Slot, 0, 0, 0, [], false, false, false),
            JobId: 0,
            JobLevel: null,
            Verdict.NeedsReview,
            ReplacedBy: null,
            facts is null
                ? "This item is not in the game's item sheet — nothing is known about it."
                : "No job can be resolved for this item, so there is nothing to compare it against.",
            DisposalMethod.None);
    }
}
