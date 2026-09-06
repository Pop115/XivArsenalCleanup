using System.Linq;
using ArsenalCleanup.Triage;
using Xunit;
using static ArsenalCleanup.Tests.TriageFixtures;

namespace ArsenalCleanup.Tests;

public sealed class ClassifierTests
{
    [Fact]
    public void KeepsALowerLevelPiece_WhenItsOnlyUpgradeCannotBeWornYet()
    {
        // The rule this whole plugin exists for. Real numbers: a level-32 bow at item level 34, and
        // a level-50 bow at item level 130, on a character whose Archer is level 41. Comparing item
        // levels alone would bin the only bow they can actually hold.
        var camp = Facts(id: 1, name: "Survival Bow", itemLevel: 34, equipLevel: 32, jobs: [Archer]);
        var magitek = Facts(id: 2, name: "Magitek Bow", itemLevel: 130, equipLevel: 50, jobs: [Archer]);

        var judged = Classifier.Classify([Owned(camp), Owned(magitek)], Levels((Archer, 41)));

        Assert.Equal(Verdict.KeptBest, Single(judged, "Survival Bow").Verdict);
        Assert.Equal(Verdict.KeptUntilLevel, Single(judged, "Magitek Bow").Verdict);
    }

    [Fact]
    public void MarksAPieceObsolete_WhenABetterOneCanAlreadyBeWorn()
    {
        var old = Facts(id: 1, name: "Weathered Fists", itemLevel: 17, equipLevel: 15);
        var better = Facts(id: 2, name: "Cesti", itemLevel: 19, equipLevel: 19);

        var judged = Classifier.Classify([Owned(old), Owned(better)], Levels((Pugilist, 25)));

        var obsolete = Single(judged, "Weathered Fists");
        Assert.Equal(Verdict.Obsolete, obsolete.Verdict);
        Assert.Equal("Cesti", obsolete.ReplacedBy?.Name);
        Assert.Equal(Verdict.KeptBest, Single(judged, "Cesti").Verdict);
    }

    [Fact]
    public void NamesTheBestReplacement_NotMerelyTheFirstThatBeatsThePiece()
    {
        // Both of the others supersede it, so the verdict is not in question — but the name shown is
        // what a player reads to decide, and "replaced by the second best thing you own" is noise.
        var worst = Facts(id: 1, name: "Hard Leather Himantes", itemLevel: 14, equipLevel: 14);
        var middling = Facts(id: 2, name: "Weathered Fists", itemLevel: 17, equipLevel: 15);
        var best = Facts(id: 3, name: "Cesti", itemLevel: 19, equipLevel: 19);

        var judged = Classifier.Classify(
            [Owned(worst), Owned(middling), Owned(best)], Levels((Pugilist, 25)));

        Assert.Equal("Cesti", Single(judged, "Hard Leather Himantes").ReplacedBy?.Name);
    }

    [Fact]
    public void BreaksAnItemLevelTie_ByWhicheverUnlocksEarliest()
    {
        var judgedPiece = Facts(id: 1, name: "Old", itemLevel: 10, equipLevel: 10);
        var late = Facts(id: 2, name: "Late", itemLevel: 20, equipLevel: 20);
        var early = Facts(id: 3, name: "Early", itemLevel: 20, equipLevel: 15);

        var judged = Classifier.Classify(
            [Owned(judgedPiece), Owned(late), Owned(early)], Levels((Pugilist, 25)));

        Assert.Equal("Early", Single(judged, "Old").ReplacedBy?.Name);
    }

    [Fact]
    public void BreaksARemainingTie_ByItemId_SoTheAnswerNeverDependsOnReadOrder()
    {
        var judgedPiece = Facts(id: 100, name: "Old", itemLevel: 10, equipLevel: 10);
        var high = Facts(id: 60, name: "Sixty", itemLevel: 20, equipLevel: 15);
        var low = Facts(id: 50, name: "Fifty", itemLevel: 20, equipLevel: 15);

        var forwards = Classifier.Classify(
            [Owned(judgedPiece), Owned(high), Owned(low)], Levels((Pugilist, 25)));
        var backwards = Classifier.Classify(
            [Owned(judgedPiece), Owned(low), Owned(high)], Levels((Pugilist, 25)));

        Assert.Equal("Fifty", Single(forwards, "Old").ReplacedBy?.Name);
        Assert.Equal("Fifty", Single(backwards, "Old").ReplacedBy?.Name);
    }

    [Fact]
    public void AFuturePieceIsObsolete_OnlyWhenSomethingBetterUnlocksAtLeastAsEarly()
    {
        var bridging = Facts(id: 1, name: "Bridging", itemLevel: 30, equipLevel: 40);
        var laterAndBetter = Facts(id: 2, name: "Later", itemLevel: 50, equipLevel: 50);

        var judged = Classifier.Classify(
            [Owned(bridging), Owned(laterAndBetter)], Levels((Pugilist, 25)));

        // "Later" arrives ten levels after "Bridging" would have been usable, so the earlier piece
        // still has a job to do in between.
        Assert.Equal(Verdict.KeptUntilLevel, Single(judged, "Bridging").Verdict);
    }

    [Fact]
    public void AFuturePieceIsObsolete_WhenSomethingBetterUnlocksNoLater()
    {
        var pointless = Facts(id: 1, name: "Pointless", itemLevel: 30, equipLevel: 40);
        var strictlyBetter = Facts(id: 2, name: "Strictly better", itemLevel: 50, equipLevel: 38);

        var judged = Classifier.Classify(
            [Owned(pointless), Owned(strictlyBetter)], Levels((Pugilist, 25)));

        var row = Single(judged, "Pointless");
        Assert.Equal(Verdict.Obsolete, row.Verdict);
        Assert.Equal("Strictly better", row.ReplacedBy?.Name);
    }

    [Fact]
    public void ASinglePieceIsAlwaysKept_WhateverItsLevel()
    {
        // Nothing to compare it against, and a lone piece can matter again later — a job levelled,
        // a role picked back up.
        var lonely = Facts(id: 1, name: "Lonely", itemLevel: 5, equipLevel: 90);

        var judged = Classifier.Classify([Owned(lonely)], Levels((Pugilist, 1)));

        Assert.Equal(Verdict.KeptOnly, Assert.Single(judged).Verdict);
    }

    [Fact]
    public void JudgesAPieceOncePerJobThatCanUseIt()
    {
        // A shared accessory can genuinely be obsolete for one job and needed by another, so the two
        // answers have to be produced separately before anything merges them.
        var shared = Facts(id: 1, name: "Shared ring", slot: "ring", itemLevel: 10, jobs: [Pugilist, Archer]);
        var betterForPugilist = Facts(id: 2, name: "Pugilist ring", slot: "ring", itemLevel: 20, jobs: [Pugilist]);

        var judged = Classifier.Classify(
            [Owned(shared), Owned(betterForPugilist)], Levels((Pugilist, 50), (Archer, 50)));

        var sharedRows = judged.Where(j => j.Facts.Name == "Shared ring").ToList();
        Assert.Equal(2, sharedRows.Count);
        Assert.Equal(Verdict.Obsolete, sharedRows.Single(r => r.JobId == Pugilist).Verdict);
        Assert.Equal(Verdict.KeptOnly, sharedRows.Single(r => r.JobId == Archer).Verdict);
    }

    [Fact]
    public void NeverSuggestsPartingWithARelic_ButStillSaysWhatWouldHaveReplacedIt()
    {
        // The guard should prompt a decision, not hide one: stripping the context would leave the
        // player wondering why a perfectly identified item is flagged at all.
        var relic = Facts(id: 1, name: "Manderville Fists", itemLevel: 10, relic: true);
        var better = Facts(id: 2, name: "Ordinary Fists", itemLevel: 20);

        var judged = Classifier.Classify([Owned(relic), Owned(better)], Levels((Pugilist, 90)));

        var row = Single(judged, "Manderville Fists");
        Assert.Equal(Verdict.NeedsReview, row.Verdict);
        Assert.Equal("Ordinary Fists", row.ReplacedBy?.Name);
        Assert.Contains("Relic", row.Reason);
        Assert.Equal(DisposalMethod.None, row.Disposal);
    }

    [Fact]
    public void FlagsAPieceForReview_WhenTheJobsLevelIsUnknown()
    {
        var a = Facts(id: 1, name: "A", itemLevel: 10);
        var b = Facts(id: 2, name: "B", itemLevel: 20);

        var judged = Classifier.Classify([Owned(a), Owned(b)], Levels());

        Assert.All(judged, row => Assert.Equal(Verdict.NeedsReview, row.Verdict));
        Assert.All(judged, row => Assert.Null(row.JobLevel));
    }

    [Fact]
    public void FlagsAnItemForReview_WhenTheGameSheetHasNoSuchRow()
    {
        var judged = Assert.Single(Classifier.Classify([Unresolved(itemId: 4242)], Levels((Pugilist, 50))));

        Assert.Equal(Verdict.NeedsReview, judged.Verdict);
        Assert.Contains("4242", judged.Facts.Name);
    }

    [Fact]
    public void FlagsAnItemForReview_WhenNoJobCanBeResolvedForIt()
    {
        var jobless = Facts(id: 1, name: "Jobless", jobs: []);

        var judged = Assert.Single(Classifier.Classify([Owned(jobless)], Levels((Pugilist, 50))));

        Assert.Equal(Verdict.NeedsReview, judged.Verdict);
        Assert.Contains("job", judged.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, true, DisposalMethod.Armoire)]
    [InlineData(true, false, DisposalMethod.Armoire)]
    [InlineData(false, true, DisposalMethod.Desynthesis)]
    [InlineData(false, false, DisposalMethod.Discard)]
    public void PrefersTheDisposalThatLosesLeast(bool armoire, bool desynthesisable, DisposalMethod expected)
    {
        // The Armoire keeps the appearance forever at no cost, desynthesis at least returns
        // materials, and discarding returns nothing — so that is the order of preference.
        Assert.Equal(expected, Classifier.DisposalFor(Facts(armoire: armoire, desynthesisable: desynthesisable)));
    }

    [Fact]
    public void AttachesADisposalMethod_OnlyToWhatIsActuallyObsolete()
    {
        var old = Facts(id: 1, name: "Old", itemLevel: 10, desynthesisable: true);
        var better = Facts(id: 2, name: "Better", itemLevel: 20, desynthesisable: true);

        var judged = Classifier.Classify([Owned(old), Owned(better)], Levels((Pugilist, 50)));

        Assert.Equal(DisposalMethod.Desynthesis, Single(judged, "Old").Disposal);
        Assert.Equal(DisposalMethod.None, Single(judged, "Better").Disposal);
    }

    [Fact]
    public void ComparesOnlyWithinTheSameSlot()
    {
        // A high item level hat says nothing about a weapon, and letting the two meet would be the
        // most embarrassing possible bug.
        var weapon = Facts(id: 1, name: "Weapon", slot: "main", itemLevel: 10);
        var hat = Facts(id: 2, name: "Hat", slot: "head", itemLevel: 500);

        var judged = Classifier.Classify([Owned(weapon), Owned(hat)], Levels((Pugilist, 90)));

        Assert.Equal(Verdict.KeptOnly, Single(judged, "Weapon").Verdict);
        Assert.Equal(Verdict.KeptOnly, Single(judged, "Hat").Verdict);
    }

    [Fact]
    public void AWornPieceIsJudgedLikeAnyOther()
    {
        // Being worn is not a reason to keep something: you can be wearing the worse of two.
        var worn = Facts(id: 1, name: "Worn", itemLevel: 10);
        var spare = Facts(id: 2, name: "Better spare", itemLevel: 20);

        var judged = Classifier.Classify(
            [Owned(worn, equipped: true), Owned(spare)], Levels((Pugilist, 50)));

        Assert.Equal(Verdict.Obsolete, Single(judged, "Worn").Verdict);
    }

    private static JudgedPiece Single(System.Collections.Generic.IEnumerable<JudgedPiece> judged, string name) =>
        judged.Single(j => j.Facts.Name == name);
}
