using ArsenalCleanup.Triage;
using Xunit;
using static ArsenalCleanup.Tests.TriageFixtures;

namespace ArsenalCleanup.Tests;

/// <summary>
/// What "better" means, job by job, and how many of the better ones it takes to displace a piece.
///
/// These two rules are small enough to look obvious and consequential enough to be worth pinning:
/// between them they decide whether the plugin tells someone to throw away a ring they are wearing
/// or the sturdiest coat they own.
/// </summary>
public sealed class GearRankingTests
{
    [Fact]
    public void EveryOrdinaryJobIsJudgedOnItemLevel()
    {
        Assert.Equal(Yardstick.ItemLevel, GearRanking.For(Pugilist).Measures);
        Assert.Equal(Yardstick.ItemLevel, GearRanking.For(Monk).Measures);
        Assert.Equal(Yardstick.ItemLevel, GearRanking.For(Bard).Measures);
    }

    [Fact]
    public void BeastmasterIsJudgedOnDefence()
    {
        // The job the game's own "recommended gear" button advises badly: it optimises the stats a
        // higher item level buys and hands over a flimsier outfit.
        Assert.Equal(Yardstick.Defence, GearRanking.For(Beastmaster).Measures);
    }

    [Fact]
    public void OnItemLevel_HigherIsSimplyBetter()
    {
        var worse = Facts(id: 1, itemLevel: 100, defence: 500);
        var better = Facts(id: 2, itemLevel: 110, defence: 1);

        Assert.True(GearRanking.ItemLevel.Beats(better, worse));
        Assert.False(GearRanking.ItemLevel.Beats(worse, better));
    }

    [Fact]
    public void OnDefence_MoreItemLevelDoesNotBuyItsWayPastLessDefence()
    {
        // The complaint this rule exists for. Real numbers, from two coats any class can wear:
        // the Vintage Coatee is eight item levels above the Moth-eaten one and six defence below it.
        var sturdy = Facts(id: 1, name: "Moth-eaten Coatee", slot: "body", itemLevel: 32, defence: 36, magicDefence: 62);
        var flimsy = Facts(id: 2, name: "Vintage Coatee", slot: "body", itemLevel: 40, defence: 30, magicDefence: 61);

        Assert.False(GearRanking.Defence.Beats(flimsy, sturdy));
        Assert.False(GearRanking.Defence.Beats(sturdy, flimsy));

        // And the same pair, judged the ordinary way, is an open-and-shut upgrade.
        Assert.True(GearRanking.ItemLevel.Beats(flimsy, sturdy));
    }

    [Fact]
    public void OnDefence_APieceThatGivesUpNothingStillWins()
    {
        // The rule is not "defence at any price": a piece better at everything is better, full stop.
        var worse = Facts(id: 1, itemLevel: 50, defence: 42, magicDefence: 85);
        var better = Facts(id: 2, itemLevel: 50, defence: 58, magicDefence: 102);

        Assert.True(GearRanking.Defence.Beats(better, worse));
        Assert.False(GearRanking.Defence.Beats(worse, better));

        // Nothing separates them on item level alone, which is why this one needs the defence rule
        // to be spotted at all.
        Assert.False(GearRanking.ItemLevel.Beats(better, worse));
    }

    [Fact]
    public void OnDefence_TradingMagicalForPhysicalIsAChoice_NotAnUpgrade()
    {
        var tough = Facts(id: 1, itemLevel: 90, defence: 200, magicDefence: 100);
        var warded = Facts(id: 2, itemLevel: 90, defence: 100, magicDefence: 200);

        Assert.False(GearRanking.Defence.Beats(tough, warded));
        Assert.False(GearRanking.Defence.Beats(warded, tough));
    }

    [Fact]
    public void OnDefence_PiecesThatDefendNothingFallBackToItemLevel()
    {
        // No weapon has any defence, and the plainest accessories have none either, so Beastmaster
        // judges those exactly as every other job does — no special case needed, and none written.
        var worse = Facts(id: 1, slot: "ring", itemLevel: 100);
        var better = Facts(id: 2, slot: "ring", itemLevel: 110);

        Assert.True(GearRanking.Defence.Beats(better, worse));
        Assert.False(GearRanking.Defence.Beats(worse, better));
    }

    [Fact]
    public void NothingBeatsItself()
    {
        var piece = Facts(id: 1, itemLevel: 100, defence: 100);

        Assert.False(GearRanking.ItemLevel.Beats(piece, piece));
        Assert.False(GearRanking.Defence.Beats(piece, piece));
    }

    [Fact]
    public void OnDefence_TheSturdiestPieceIsNamedFirst()
    {
        var sturdiest = Facts(id: 1, itemLevel: 80, defence: 200);
        var highestLevel = Facts(id: 2, itemLevel: 120, defence: 100);

        Assert.True(GearRanking.Defence.CompareBest(sturdiest, highestLevel) < 0);
        Assert.True(GearRanking.ItemLevel.CompareBest(highestLevel, sturdiest) < 0);
    }

    [Fact]
    public void OrderingIsTotal_SoTheSameArmouryAlwaysNamesTheSamePiece()
    {
        // Equal on everything the ranking values: the tie-break falls to what unlocks earliest, then
        // to the item id, so the answer cannot depend on the order the inventory was read in.
        var early = Facts(id: 900, itemLevel: 100, equipLevel: 50);
        var late = Facts(id: 100, itemLevel: 100, equipLevel: 60);
        var sameAgain = Facts(id: 500, itemLevel: 100, equipLevel: 50);

        Assert.True(GearRanking.ItemLevel.CompareBest(early, late) < 0);
        Assert.True(GearRanking.ItemLevel.CompareBest(sameAgain, early) < 0);
        Assert.Equal(0, GearRanking.ItemLevel.CompareBest(early, early));
    }

    [Theory]
    [InlineData("ring", 2)]
    [InlineData("main", 1)]
    [InlineData("offhand", 1)]
    [InlineData("ear", 1)]
    [InlineData("neck", 1)]
    [InlineData("wrist", 1)]
    [InlineData("body", 1)]
    public void ACharacterWearsTwoRingsAndOneOfEverythingElse(string slot, int expected)
    {
        Assert.Equal(expected, SlotCapacity.For(slot));
    }

    [Fact]
    public void AnUnknownSlotCountsAsOne_WhichCanOnlyEverKeepTooMuch()
    {
        Assert.Equal(1, SlotCapacity.For("something the game has not shipped yet"));
    }
}
