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
        // answers have to be produced separately before anything merges them. A necklace rather than
        // a ring, because only one is worn: rings have their own rule and would muddle this one.
        var shared = Facts(id: 1, name: "Shared choker", slot: "neck", itemLevel: 10, jobs: [Pugilist, Archer]);
        var betterForPugilist = Facts(id: 2, name: "Pugilist choker", slot: "neck", itemLevel: 20, jobs: [Pugilist]);

        var judged = Classifier.Classify(
            [Owned(shared), Owned(betterForPugilist)], Levels((Pugilist, 50), (Archer, 50)));

        var sharedRows = judged.Where(j => j.Facts.Name == "Shared choker").ToList();
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
        Assert.Equal(ReasonCode.SupersededNow, row.Reason);
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
        Assert.Equal(ReasonCode.JobUnresolved, judged.Reason);
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

    [Fact]
    public void KeepsARing_WhenOnlyOneBetterOneCanBeWorn()
    {
        // A character wears two rings. One better ring does not free the other hand, so the piece it
        // beat is still the one being worn there — and telling someone to bin it is telling them to
        // go a ring short.
        var ordinary = Facts(id: 1, name: "Ordinary ring", slot: "ring", itemLevel: 10, equipLevel: 10);
        var better = Facts(id: 2, name: "Better ring", slot: "ring", itemLevel: 50, equipLevel: 10);

        var judged = Classifier.Classify([Owned(ordinary), Owned(better)], Levels((Pugilist, 50)));

        Assert.Equal(Verdict.KeptBest, Single(judged, "Ordinary ring").Verdict);
        Assert.Equal(Verdict.KeptBest, Single(judged, "Better ring").Verdict);
        Assert.All(judged, row => Assert.Equal(DisposalMethod.None, row.Disposal));
    }

    [Fact]
    public void LetsARingGo_OnceTwoBetterOnesCanBeWorn()
    {
        // Two better rings do fill both hands, and only then is the third one spare.
        var ordinary = Facts(id: 1, name: "Ordinary ring", slot: "ring", itemLevel: 10, equipLevel: 10);
        var good = Facts(id: 2, name: "Good ring", slot: "ring", itemLevel: 40, equipLevel: 10);
        var better = Facts(id: 3, name: "Better ring", slot: "ring", itemLevel: 50, equipLevel: 10);

        var judged = Classifier.Classify(
            [Owned(ordinary), Owned(good), Owned(better)], Levels((Pugilist, 50)));

        var replaced = Single(judged, "Ordinary ring");
        Assert.Equal(Verdict.Obsolete, replaced.Verdict);
        Assert.Equal(ReasonCode.SupersededNow, replaced.Reason);
    }

    [Fact]
    public void NamesTheWeakerOfTheTwoRings_ThatPushedItOut()
    {
        // For an ordinary slot the replacement named is the best piece owned. For rings the tighter
        // and more convincing statement is the second best: even that one beats it.
        var ordinary = Facts(id: 1, name: "Ordinary ring", slot: "ring", itemLevel: 10, equipLevel: 10);
        var good = Facts(id: 2, name: "Good ring", slot: "ring", itemLevel: 40, equipLevel: 10);
        var best = Facts(id: 3, name: "Best ring", slot: "ring", itemLevel: 50, equipLevel: 10);

        var judged = Classifier.Classify(
            [Owned(ordinary), Owned(good), Owned(best)], Levels((Pugilist, 50)));

        Assert.Equal("Good ring", Single(judged, "Ordinary ring").ReplacedBy?.Name);
    }

    [Fact]
    public void CountsOnlyTheRingsThatCanActuallyBeWornToday()
    {
        // Two better rings, but one of them is fifty levels away: only one hand is really spoken for.
        var ordinary = Facts(id: 1, name: "Ordinary ring", slot: "ring", itemLevel: 10, equipLevel: 10);
        var usable = Facts(id: 2, name: "Usable ring", slot: "ring", itemLevel: 40, equipLevel: 20);
        var faraway = Facts(id: 3, name: "Far-off ring", slot: "ring", itemLevel: 90, equipLevel: 90);

        var judged = Classifier.Classify(
            [Owned(ordinary), Owned(usable), Owned(faraway)], Levels((Pugilist, 40)));

        Assert.Equal(Verdict.KeptBest, Single(judged, "Ordinary ring").Verdict);
    }

    [Fact]
    public void AFutureRingIsKept_UnlessTwoBetterOnesUnlockNoLater()
    {
        var future = Facts(id: 1, name: "Future ring", slot: "ring", itemLevel: 30, equipLevel: 60);
        var earlier = Facts(id: 2, name: "Earlier ring", slot: "ring", itemLevel: 50, equipLevel: 55);

        var judged = Classifier.Classify([Owned(future), Owned(earlier)], Levels((Pugilist, 20)));

        // One earlier ring is not enough to make the later one pointless: both will be worn.
        Assert.Equal(Verdict.KeptUntilLevel, Single(judged, "Future ring").Verdict);

        var second = Facts(id: 3, name: "Second earlier ring", slot: "ring", itemLevel: 45, equipLevel: 50);
        var withTwo = Classifier.Classify(
            [Owned(future), Owned(earlier), Owned(second)], Levels((Pugilist, 20)));

        Assert.Equal(Verdict.Obsolete, Single(withTwo, "Future ring").Verdict);
        Assert.Equal(ReasonCode.OutclassedByEarlier, Single(withTwo, "Future ring").Reason);
    }

    [Fact]
    public void KeepsBothRings_WhenTwoAreAllYouOwn()
    {
        var worn = Facts(id: 1, name: "Worn ring", slot: "ring", itemLevel: 10, equipLevel: 10);
        var spare = Facts(id: 2, name: "Spare ring", slot: "ring", itemLevel: 400, equipLevel: 10);

        var judged = Classifier.Classify(
            [Owned(worn, equipped: true), Owned(spare)], Levels((Pugilist, 90)));

        Assert.All(judged, row => Assert.NotEqual(Verdict.Obsolete, row.Verdict));
    }

    [Fact]
    public void KeepsABeastmastersSturdierPiece_WhenTheUpgradeIsOnlyHigherLevelled()
    {
        // Real coats, either of which any class can wear: eight item levels apart, and the higher one
        // defends less. Judged the ordinary way the first is scrap; judged as Beastmaster it is not.
        var sturdy = Facts(id: 1, name: "Moth-eaten Coatee", slot: "body", itemLevel: 32, equipLevel: 37,
            jobs: [Beastmaster], defence: 36, magicDefence: 62);
        var flimsy = Facts(id: 2, name: "Vintage Coatee", slot: "body", itemLevel: 40, equipLevel: 37,
            jobs: [Beastmaster], defence: 30, magicDefence: 61);

        var judged = Classifier.Classify([Owned(sturdy), Owned(flimsy)], Levels((Beastmaster, 50)));

        Assert.Equal(Verdict.KeptBest, Single(judged, "Moth-eaten Coatee").Verdict);
        Assert.Equal(Verdict.KeptBest, Single(judged, "Vintage Coatee").Verdict);
    }

    [Fact]
    public void TheSamePairIsStillAnUpgradeForEveryOtherJob()
    {
        // The defence rule is Beastmaster's alone. Nothing else changes for anybody else.
        var sturdy = Facts(id: 1, name: "Moth-eaten Coatee", slot: "body", itemLevel: 32, equipLevel: 37,
            jobs: [Pugilist], defence: 36, magicDefence: 62);
        var flimsy = Facts(id: 2, name: "Vintage Coatee", slot: "body", itemLevel: 40, equipLevel: 37,
            jobs: [Pugilist], defence: 30, magicDefence: 61);

        var judged = Classifier.Classify([Owned(sturdy), Owned(flimsy)], Levels((Pugilist, 50)));

        Assert.Equal(Verdict.Obsolete, Single(judged, "Moth-eaten Coatee").Verdict);
    }

    [Fact]
    public void StillLetsBeastmasterGearGo_WhenSomethingBeatsItAtEverything()
    {
        // The rule is not a blanket refusal to decide: a piece that is sturdier and no lower levelled
        // replaces one that is neither, and this pair is invisible to an item-level comparison.
        var lesser = Facts(id: 1, name: "Patrician's Coatee", slot: "body", itemLevel: 50, equipLevel: 50,
            jobs: [Beastmaster], defence: 42, magicDefence: 85);
        var sturdier = Facts(id: 2, name: "Dated Linen Coatee", slot: "body", itemLevel: 50, equipLevel: 50,
            jobs: [Beastmaster], defence: 58, magicDefence: 102);

        var judged = Classifier.Classify([Owned(lesser), Owned(sturdier)], Levels((Beastmaster, 50)));

        var replaced = Single(judged, "Patrician's Coatee");
        Assert.Equal(Verdict.Obsolete, replaced.Verdict);
        Assert.Equal("Dated Linen Coatee", replaced.ReplacedBy?.Name);
        Assert.Equal(Verdict.KeptBest, Single(judged, "Dated Linen Coatee").Verdict);
    }

    [Fact]
    public void NamesTheSturdiestPiece_WhenSeveralBeatABeastmastersGear()
    {
        var scrap = Facts(id: 1, name: "Scrap", slot: "body", itemLevel: 50, equipLevel: 50,
            jobs: [Beastmaster], defence: 40);
        var sturdy = Facts(id: 2, name: "Sturdy", slot: "body", itemLevel: 50, equipLevel: 50,
            jobs: [Beastmaster], defence: 60);
        var sturdiest = Facts(id: 3, name: "Sturdiest", slot: "body", itemLevel: 50, equipLevel: 50,
            jobs: [Beastmaster], defence: 90);

        var judged = Classifier.Classify(
            [Owned(scrap), Owned(sturdy), Owned(sturdiest)], Levels((Beastmaster, 50)));

        Assert.Equal("Sturdiest", Single(judged, "Scrap").ReplacedBy?.Name);
    }

    [Fact]
    public void ABeastmastersWeaponIsJudgedOnItemLevelLikeAnyOther()
    {
        // No weapon carries defence, so there is nothing for the rule to prefer and it gets out of
        // the way rather than declaring every axe incomparable.
        var old = Facts(id: 1, name: "Beast Herder's Hand Axe", itemLevel: 5, equipLevel: 1, jobs: [Beastmaster]);
        var better = Facts(id: 2, name: "Beastmaster's Hand Axe", itemLevel: 90, equipLevel: 50, jobs: [Beastmaster]);

        var judged = Classifier.Classify([Owned(old), Owned(better)], Levels((Beastmaster, 50)));

        Assert.Equal(Verdict.Obsolete, Single(judged, "Beast Herder's Hand Axe").Verdict);
        Assert.Equal("Beastmaster's Hand Axe", Single(judged, "Beast Herder's Hand Axe").ReplacedBy?.Name);
    }

    [Fact]
    public void ABeastmasterWearsTwoRingsToo()
    {
        var ordinary = Facts(id: 1, name: "Ordinary ring", slot: "ring", itemLevel: 10, jobs: [Beastmaster]);
        var better = Facts(id: 2, name: "Better ring", slot: "ring", itemLevel: 50, jobs: [Beastmaster]);

        var judged = Classifier.Classify([Owned(ordinary), Owned(better)], Levels((Beastmaster, 50)));

        Assert.Equal(Verdict.KeptBest, Single(judged, "Ordinary ring").Verdict);
    }

    private static JudgedPiece Single(System.Collections.Generic.IEnumerable<JudgedPiece> judged, string name) =>
        judged.Single(j => j.Facts.Name == name);
}
