using System.Collections.Generic;
using System.Linq;

namespace ArsenalCleanup.Triage;

/// <summary>One physical piece, with every job's judgement folded into a single answer.</summary>
/// <param name="Jobs">Every class or job that can equip it, for context.</param>
/// <param name="RelevantJobs">
/// The classes the verdict is actually about — the ones still waiting to reach its level, or the
/// ones it is currently best for. This is what turns "keep until this job reaches level 50" into
/// something a player can act on: which of their classes, exactly.
/// </param>
/// <param name="RequiredLevel">The level the piece can be worn from, when that is what the verdict hinges on.</param>
/// <param name="DecidingJobLevel">The level of the job whose judgement was kept.</param>
public sealed record TriagedPiece(
    OwnedPiece Piece,
    GearFacts Facts,
    IReadOnlyList<uint> Jobs,
    IReadOnlyList<uint> RelevantJobs,
    uint? DecidingJob,
    int? DecidingJobLevel,
    int? RequiredLevel,
    Verdict Verdict,
    GearFacts? ReplacedBy,
    ReasonCode Reason,
    DisposalMethod Disposal);

/// <summary>
/// Collapses the per-job judgements of one physical piece into a single row.
///
/// Someone levelling every class in the game does not want a per-job verdict list; their real rule
/// is simpler: let a piece go only when *no* class they intend to level still needs it. So a piece
/// is obsolete here only if every job that can use it says so; otherwise the most actionable reason
/// to keep it wins, and the classes that reason applies to travel with it.
/// </summary>
public static class PieceMerger
{
    /// <summary>
    /// Lower sorts first among the reasons to keep. "Not at that level yet" leads because it is the
    /// most specific and most actionable — it tells the player what will change and when.
    /// </summary>
    private static int KeepPriority(Verdict verdict) => verdict switch
    {
        Verdict.KeptUntilLevel => 0,
        Verdict.KeptBest => 1,
        Verdict.KeptOnly => 2,
        Verdict.NeedsReview => 3,
        _ => 4,
    };

    public static List<TriagedPiece> Merge(IReadOnlyList<JudgedPiece> judged)
    {
        // A worn piece and an identical spare in the chest are two distinct physical objects, and
        // only the spare can be acted on without unequipping — so the equipped flag is part of the
        // identity, not an attribute of a shared row.
        // Identity is the item id and where it sits, never the display name: two different items
        // sharing a name would otherwise be folded into one row, and the surviving row's facts would
        // depend on which was read first.
        var groups = judged
            .GroupBy(j => (j.Facts.ItemId, j.Facts.Slot, j.Piece.IsEquipped, j.Piece.Position));

        var merged = new List<TriagedPiece>();
        foreach (var group in groups)
        {
            var rows = group.ToList();
            var jobs = rows.Where(r => r.JobId != 0).Select(r => r.JobId).Distinct().ToList();

            var allObsolete = rows.All(r => r.Verdict == Verdict.Obsolete);
            var candidates = allObsolete ? rows : rows.Where(r => r.Verdict != Verdict.Obsolete).ToList();
            // Ties are broken on the job id, not on arrival order: the game makes no promise about
            // the order it hands over an inventory, and an answer that changes between two reads of
            // the same armoury is one nobody can trust or report a bug against.
            var representative = candidates
                .OrderBy(r => KeepPriority(r.Verdict))
                .ThenBy(r => r.JobId)
                .First();

            // Every class that reached the same conclusion — the ones the shown reason is about.
            var relevant = rows
                .Where(r => r.Verdict == representative.Verdict && r.JobId != 0)
                .Select(r => r.JobId)
                .Distinct()
                .ToList();

            merged.Add(new TriagedPiece(
                representative.Piece,
                representative.Facts,
                jobs,
                relevant,
                representative.JobId == 0 ? null : representative.JobId,
                representative.JobLevel,
                representative.Facts.EquipLevel,
                representative.Verdict,
                representative.ReplacedBy,
                representative.Reason,
                representative.Disposal));
        }

        return merged;
    }
}
