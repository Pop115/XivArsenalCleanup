using System.Collections.Generic;

namespace ArsenalCleanup.Triage;

/// <summary>Class and job row ids named directly by a rule. These ids are fixed for the life of the game.</summary>
public static class KnownJobs
{
    /// <summary>Beastmaster — the limited job whose gear is judged on defence rather than item level.</summary>
    public const uint Beastmaster = 43;
}

/// <summary>What a ranking measures, so the wording can say which yardstick a verdict used.</summary>
public enum Yardstick
{
    ItemLevel,
    Defence,
}

/// <summary>
/// What "better" means for one job.
///
/// For nearly every job it means a higher item level, because the game scales every stat on a piece
/// from that one number: a higher item level is better at everything the job does.
///
/// Beastmaster breaks that. Its content is survived rather than raced, so defence is worth more
/// than the offensive substats a higher item level buys — which is exactly why the game's own
/// "recommended gear" button gives poor advice there: it optimises the stats, and hands over a
/// flimsier outfit. A ranking is therefore a per-job question, not a global constant.
/// </summary>
public abstract class GearRanking
{
    protected GearRanking()
    {
        BestFirst = Comparer<GearFacts>.Create(CompareBest);
    }

    /// <summary>Higher item level wins. Every job but Beastmaster.</summary>
    public static readonly GearRanking ItemLevel = new ItemLevelRanking();

    /// <summary>Defence first, and never traded away for item level. Beastmaster.</summary>
    public static readonly GearRanking Defence = new DefenceFirstRanking();

    /// <summary>The ranking to judge a piece by for a given class or job.</summary>
    public static GearRanking For(uint jobId) =>
        jobId == KnownJobs.Beastmaster ? Defence : ItemLevel;

    /// <summary>What this ranking is about, for the sentence the player reads.</summary>
    public abstract Yardstick Measures { get; }

    /// <summary>
    /// True when <paramref name="challenger"/> is better than <paramref name="incumbent"/> in every
    /// way this ranking cares about, and strictly better in at least one.
    ///
    /// Demanding every axis rather than a single score is the point: two pieces that each win on a
    /// different axis leave the choice with the player, which is the honest answer and the one that
    /// stops a defensive piece being binned for an offensive one.
    /// </summary>
    public abstract bool Beats(GearFacts challenger, GearFacts incumbent);

    /// <summary>
    /// Orders two pieces best-first. Negative when <paramref name="a"/> is the better pick.
    ///
    /// The primary keys belong to the ranking; the tie-breaks are shared and deliberately total, so
    /// that the piece named as a replacement never depends on the order the inventory was read in.
    /// </summary>
    public int CompareBest(GearFacts a, GearFacts b)
    {
        var primary = ComparePrimary(a, b);
        if (primary != 0) return primary;

        // Of two equally good pieces, the one usable soonest is the more useful to name.
        if (a.EquipLevel != b.EquipLevel) return a.EquipLevel.CompareTo(b.EquipLevel);
        return a.ItemId.CompareTo(b.ItemId);
    }

    /// <summary>
    /// <see cref="CompareBest"/> as a comparer, built once rather than per comparison: a refresh
    /// sorts candidates for every piece, for every job that can wear it.
    /// </summary>
    public IComparer<GearFacts> BestFirst { get; }

    /// <summary>Orders on what this ranking actually values. Negative when <paramref name="a"/> is better.</summary>
    protected abstract int ComparePrimary(GearFacts a, GearFacts b);

    private sealed class ItemLevelRanking : GearRanking
    {
        public override Yardstick Measures => Yardstick.ItemLevel;

        public override bool Beats(GearFacts challenger, GearFacts incumbent) =>
            challenger.ItemLevel > incumbent.ItemLevel;

        protected override int ComparePrimary(GearFacts a, GearFacts b) =>
            b.ItemLevel.CompareTo(a.ItemLevel);
    }

    /// <summary>
    /// Defence first: a piece only supersedes another when it gives up none of either defence, and
    /// none of the item level either.
    ///
    /// Both kinds of defence count separately rather than as a total, because a piece that trades
    /// magical defence for physical is a choice, not an upgrade, and the plugin has no business
    /// making it. In practice the two move together on almost every piece the game ships, so this
    /// costs nothing on ordinary gear and only speaks up where the trade is real.
    /// </summary>
    private sealed class DefenceFirstRanking : GearRanking
    {
        public override Yardstick Measures => Yardstick.Defence;

        public override bool Beats(GearFacts challenger, GearFacts incumbent)
        {
            if (challenger.DefencePhysical < incumbent.DefencePhysical) return false;
            if (challenger.DefenceMagical < incumbent.DefenceMagical) return false;
            if (challenger.ItemLevel < incumbent.ItemLevel) return false;

            return challenger.DefencePhysical > incumbent.DefencePhysical
                || challenger.DefenceMagical > incumbent.DefenceMagical
                || challenger.ItemLevel > incumbent.ItemLevel;
        }

        protected override int ComparePrimary(GearFacts a, GearFacts b)
        {
            // Total defence decides which piece to name first; item level only separates equals.
            // No weapon defends at all and the plainest accessories do not either, so for those this
            // falls back to item level on its own — the same answer every other job would give.
            var defence = (b.DefencePhysical + b.DefenceMagical).CompareTo(a.DefencePhysical + a.DefenceMagical);
            return defence != 0 ? defence : b.ItemLevel.CompareTo(a.ItemLevel);
        }
    }
}
