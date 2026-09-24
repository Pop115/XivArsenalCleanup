using System.Collections.Generic;
using System.Linq;

namespace ArsenalCleanup.Triage;

/// <summary>
/// Why a piece got the verdict it did.
///
/// A code rather than a sentence: the wording belongs to the presentation layer, which knows the
/// player's language and which classes to name. Baking English prose in here made every reason read
/// "this job" — true, unhelpful, and untranslatable.
/// </summary>
public enum ReasonCode
{
    /// <summary>Nothing else owned fills that slot for that job.</summary>
    OnlyOneOwned,

    /// <summary>Nothing owned beats it among what can be equipped now.</summary>
    BestEquippable,

    /// <summary>Better than what is usable today, but the job has not reached its level yet.</summary>
    NotAtLevelYet,

    /// <summary>Beaten by something already equippable.</summary>
    SupersededNow,

    /// <summary>Not usable yet, and something better unlocks no later.</summary>
    OutclassedByEarlier,

    /// <summary>The job's level could not be read, so nothing can be concluded.</summary>
    JobLevelUnknown,

    /// <summary>The game's item sheet has no such row.</summary>
    ItemUnknown,

    /// <summary>The item resolved to no class or job at all.</summary>
    JobUnresolved,
}

/// <summary>One piece judged for one job that can use it.</summary>
/// <param name="ReplacedBy">The piece that supersedes it — set only when superseded, including when a relic guard downgraded the verdict.</param>
/// <param name="Ranking">What "better" meant for this job, so the wording can name the yardstick the verdict actually used.</param>
public sealed record JudgedPiece(
    OwnedPiece Piece,
    GearFacts Facts,
    uint JobId,
    int? JobLevel,
    GearRanking Ranking,
    Verdict Verdict,
    GearFacts? ReplacedBy,
    ReasonCode Reason,
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
/// Two things decide what "better" and "enough of it" mean, and neither is a constant.
/// <see cref="GearRanking"/> says what better means for the job being judged — item level for
/// almost every job, defence for Beastmaster. <see cref="SlotCapacity"/> says how many better
/// pieces it takes to displace one: a character wears two rings, so a single better ring leaves the
/// ring it beat still on the other hand.
///
/// Soul crystals are the caller's business: they are never gear to triage and should be filtered
/// out before calling. Relics are this function's — an obsolete verdict on one is always downgraded
/// to <see cref="Verdict.NeedsReview"/>, with the "replaced by" context kept so the player can still
/// see why it would have gone.
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
                results.Add(Judge(group.Value[0], group.Key.JobId, jobLevels, Verdict.KeptOnly, null, ReasonCode.OnlyOneOwned));
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
        var ranking = GearRanking.For(jobId);

        if (!jobLevels.TryGetValue(jobId, out var reported))
        {
            return Judge(piece, jobId, jobLevels, Verdict.NeedsReview, null, ReasonCode.JobLevelUnknown);
        }

        var level = reported;
        var capacity = SlotCapacity.For(facts.Slot);
        var others = group.Where(p => !ReferenceEquals(p, piece)).ToList();

        // Displaced by better pieces that can be worn right now — and there have to be enough of
        // them to fill every copy of the slot, or this piece is still filling one.
        var supersededNow = Displacing(
            others.Where(o => ranking.Beats(o.Facts!, facts) && o.Facts!.EquipLevel <= level),
            capacity,
            ranking);
        if (supersededNow is not null)
        {
            return Judge(piece, jobId, jobLevels, Verdict.Obsolete, supersededNow, ReasonCode.SupersededNow);
        }

        if (facts.EquipLevel > level)
        {
            // Not reachable yet. Only truly obsolete if enough future pieces dominate it: better,
            // and available at least as early — otherwise this one still bridges the gap.
            var dominated = Displacing(
                others.Where(o => ranking.Beats(o.Facts!, facts) && o.Facts!.EquipLevel <= facts.EquipLevel),
                capacity,
                ranking);
            if (dominated is not null)
            {
                return Judge(piece, jobId, jobLevels, Verdict.Obsolete, dominated, ReasonCode.OutclassedByEarlier);
            }

            return Judge(piece, jobId, jobLevels, Verdict.KeptUntilLevel, null, ReasonCode.NotAtLevelYet);
        }

        return Judge(piece, jobId, jobLevels, Verdict.KeptBest, null, ReasonCode.BestEquippable);
    }

    /// <summary>
    /// The piece that actually pushes this one out of use, or null when too few do.
    ///
    /// A piece only stops being worn once every copy of its slot is filled by something better, so
    /// it takes <paramref name="capacity"/> of them before the answer is yes. One better ring is no
    /// argument for parting with a ring: both are on a hand.
    ///
    /// The one named is the weakest of those ahead of it, which is the tightest true statement — for
    /// an ordinary slot that is simply the best piece owned, and for rings it is the second best,
    /// the one a player needs to see to be convinced. Taking the maximum rather than the first match
    /// is also what keeps the answer independent of the order the inventory happened to arrive in;
    /// the verdict never depends on which piece is named, but what a human reads does.
    /// </summary>
    private static GearFacts? Displacing(IEnumerable<OwnedPiece> candidates, int capacity, GearRanking ranking)
    {
        var ahead = candidates
            .Select(c => c.Facts!)
            .OrderBy(f => f, ranking.BestFirst)
            .Take(capacity)
            .ToList();

        return ahead.Count < capacity ? null : ahead[^1];
    }

    private static JudgedPiece Judge(
        OwnedPiece piece,
        uint jobId,
        IReadOnlyDictionary<uint, int> jobLevels,
        Verdict verdict,
        GearFacts? replacedBy,
        ReasonCode reason)
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
            jobLevels.TryGetValue(jobId, out var reported) ? reported : null,
            GearRanking.For(jobId),
            finalVerdict,
            replacedBy,
            reason,
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
            facts ?? new GearFacts(piece.ItemId, $"#{piece.ItemId}", piece.Slot, 0, 0, 0, [], 0, 0, false, false, false),
            JobId: 0,
            JobLevel: null,
            GearRanking.For(0),
            Verdict.NeedsReview,
            ReplacedBy: null,
            facts is null ? ReasonCode.ItemUnknown : ReasonCode.JobUnresolved,
            DisposalMethod.None);
    }
}
