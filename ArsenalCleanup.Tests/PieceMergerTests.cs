using System.Collections.Generic;
using System.Linq;
using ArsenalCleanup.Triage;
using Xunit;
using static ArsenalCleanup.Tests.TriageFixtures;

namespace ArsenalCleanup.Tests;

public sealed class PieceMergerTests
{
    private static JudgedPiece Row(
        string name,
        Verdict verdict,
        uint jobId = Pugilist,
        bool equipped = false,
        int position = 0,
        DisposalMethod disposal = DisposalMethod.None)
    {
        var facts = Facts(id: 1, name: name);
        return new JudgedPiece(
            Owned(facts, equipped, position), facts, jobId, JobLevel: 50, verdict,
            ReplacedBy: null, Reason: ReasonCode.BestEquippable, disposal);
    }

    [Fact]
    public void CollapsesEveryJobsJudgementIntoOneRowPerPiece()
    {
        var merged = PieceMerger.Merge([
            Row("Shared", Verdict.KeptBest, Pugilist),
            Row("Shared", Verdict.KeptBest, Monk),
        ]);

        var piece = Assert.Single(merged);
        Assert.Equal([Pugilist, Monk], piece.Jobs);
    }

    [Fact]
    public void LetsAPieceGoOnlyWhenEveryJobThatCanUseItAgrees()
    {
        // Someone levelling every class throws a piece away when it will serve none of them — so one
        // job still needing it is enough to keep it.
        var merged = PieceMerger.Merge([
            Row("Shared", Verdict.Obsolete, Pugilist, disposal: DisposalMethod.Discard),
            Row("Shared", Verdict.KeptOnly, Monk),
        ]);

        Assert.Equal(Verdict.KeptOnly, Assert.Single(merged).Verdict);
    }

    [Fact]
    public void LetsAPieceGoWhenEveryJobAgrees()
    {
        var merged = PieceMerger.Merge([
            Row("Shared", Verdict.Obsolete, Pugilist, disposal: DisposalMethod.Desynthesis),
            Row("Shared", Verdict.Obsolete, Monk, disposal: DisposalMethod.Desynthesis),
        ]);

        var piece = Assert.Single(merged);
        Assert.Equal(Verdict.Obsolete, piece.Verdict);
        Assert.Equal(DisposalMethod.Desynthesis, piece.Disposal);
    }

    [Theory]
    [InlineData(Verdict.KeptUntilLevel, Verdict.KeptBest, Verdict.KeptUntilLevel)]
    [InlineData(Verdict.KeptBest, Verdict.KeptOnly, Verdict.KeptBest)]
    [InlineData(Verdict.KeptOnly, Verdict.NeedsReview, Verdict.KeptOnly)]
    public void KeepsTheMostActionableReason(Verdict first, Verdict second, Verdict expected)
    {
        // "You will need this at level 50" tells the player what changes and when; "it is your best"
        // does not. Both being true, the first is the one worth showing.
        var merged = PieceMerger.Merge([
            Row("Shared", first, Pugilist),
            Row("Shared", second, Monk),
        ]);

        Assert.Equal(expected, Assert.Single(merged).Verdict);
    }

    [Fact]
    public void KeepsAWornPieceAndAnIdenticalSpareApart()
    {
        // Two physical objects. Only the spare can be dealt with without unequipping, so folding
        // them together would hide the one the player can actually act on.
        var merged = PieceMerger.Merge([
            Row("Cesti", Verdict.KeptBest, equipped: true, position: 0),
            Row("Cesti", Verdict.Obsolete, equipped: false, position: 4, disposal: DisposalMethod.Discard),
        ]);

        Assert.Equal(2, merged.Count);
        Assert.Single(merged, p => p.Piece.IsEquipped);
        Assert.Single(merged, p => !p.Piece.IsEquipped);
    }

    [Fact]
    public void KeepsTwoCopiesInDifferentChestSlotsApart()
    {
        var merged = PieceMerger.Merge([
            Row("Cesti", Verdict.KeptBest, position: 1),
            Row("Cesti", Verdict.KeptBest, position: 2),
        ]);

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void ReportsNoJobForAPieceThatResolvedToNone()
    {
        var merged = PieceMerger.Merge([Row("Mystery", Verdict.NeedsReview, jobId: 0)]);

        var piece = Assert.Single(merged);
        Assert.Null(piece.DecidingJob);
        Assert.Empty(piece.Jobs);
    }

    [Fact]
    public void MergingNothingProducesNothing()
    {
        Assert.Empty(PieceMerger.Merge(new List<JudgedPiece>()));
    }
}
