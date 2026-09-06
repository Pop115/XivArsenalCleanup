using System.Collections.Generic;
using System.Linq;

namespace ArsenalCleanup.Triage;

/// <summary>One physical piece, with every job's judgement folded into a single answer.</summary>
/// <param name="Jobs">Every job that can equip it, for context — no longer an organising axis.</param>
/// <param name="DecidingJob">The job whose judgement was kept as representative, paired with <paramref name="DecidingJobLevel"/> so the UI never has to guess which job a shown level belongs to.</param>
public sealed record TriagedPiece(
    OwnedPiece Piece,
    GearFacts Facts,
    IReadOnlyList<uint> Jobs,
    uint? DecidingJob,
    int? DecidingJobLevel,
    Verdict Verdict,
    GearFacts? ReplacedBy,
    string Reason,
    DisposalMethod Disposal);

/// <summary>
/// Collapses the per-job judgements of one physical piece into a single row.
///
/// Someone levelling every class in the game does not want a per-job verdict list; their real rule
/// is simpler: let a piece go only when *no* class they intend to level still needs it. So a piece
/// is obsolete here only if every job that can use it says so; otherwise the most actionable reason
/// to keep it wins.
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
        var groups = judged
            .GroupBy(j => (j.Facts.Name, j.Facts.Slot, j.Piece.IsEquipped, j.Piece.Position));

        var merged = new List<TriagedPiece>();
        foreach (var group in groups)
        {
            var rows = group.ToList();
            var jobs = rows.Where(r => r.JobId != 0).Select(r => r.JobId).Distinct().ToList();

            var allObsolete = rows.All(r => r.Verdict == Verdict.Obsolete);
            var candidates = allObsolete ? rows : rows.Where(r => r.Verdict != Verdict.Obsolete).ToList();
            var representative = candidates
                .OrderBy(r => KeepPriority(r.Verdict))
                .First();

            merged.Add(new TriagedPiece(
                representative.Piece,
                representative.Facts,
                jobs,
                representative.JobId == 0 ? null : representative.JobId,
                representative.JobLevel,
                representative.Verdict,
                representative.ReplacedBy,
                representative.Reason,
                representative.Disposal));
        }

        return merged;
    }
}
