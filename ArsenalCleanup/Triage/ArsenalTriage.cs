using System.Collections.Generic;
using System.Linq;

namespace ArsenalCleanup.Triage;

/// <summary>The whole triage, ready to display.</summary>
/// <param name="SoulCrystals">
/// Set aside untouched. A soul crystal is not gear to compare — there is exactly one per job and
/// parting with it is never the right answer — so it is reported separately rather than judged.
/// </param>
public sealed record TriageResult(
    IReadOnlyList<TriagedPiece> Pieces,
    IReadOnlyList<GearFacts> SoulCrystals)
{
    public static readonly TriageResult Empty = new([], []);

    public int Count(Verdict verdict) => Pieces.Count(p => p.Verdict == verdict);

    public int Count(DisposalMethod disposal) => Pieces.Count(p => p.Disposal == disposal);

    /// <summary>Everything the player has no reason to keep, and everything worth a second look.</summary>
    public IEnumerable<TriagedPiece> Actionable =>
        Pieces.Where(p => p.Verdict is Verdict.Obsolete or Verdict.NeedsReview);

    /// <summary>
    /// Every copy of one item the player owns — usually none or one, but two when the same piece is
    /// both worn and spare, and those two have different answers.
    ///
    /// This is what turns the triage into something usable while looking at the Armoury Chest: the
    /// game says which item is under the cursor, and this says what was concluded about it.
    /// </summary>
    public IReadOnlyList<TriagedPiece> PiecesOf(uint itemId) =>
        itemId == 0 ? [] : Pieces.Where(p => p.Facts.ItemId == itemId).ToList();
}

/// <summary>
/// Runs the whole triage over what the character owns: judge each piece per job, fold those
/// judgements into one row per physical piece, and set soul crystals aside.
///
/// Pure by design — inventory reading and sheet lookups happen before this is called, so the entire
/// decision path can be exercised in tests without a running game.
/// </summary>
public static class ArsenalTriage
{
    public static TriageResult Run(
        IReadOnlyList<OwnedPiece> owned,
        IReadOnlyDictionary<uint, int> jobLevels)
    {
        var soulCrystals = new List<GearFacts>();
        var toJudge = new List<OwnedPiece>();

        foreach (var piece in owned)
        {
            if (piece.Facts is { Slot: "soul_crystal" } facts) soulCrystals.Add(facts);
            else toJudge.Add(piece);
        }

        var merged = PieceMerger.Merge(Classifier.Classify(toJudge, jobLevels));

        return new TriageResult(
            merged,
            soulCrystals.GroupBy(c => c.ItemId).Select(g => g.First()).OrderBy(c => c.Name).ToList());
    }
}
