using System.Linq;
using ArsenalCleanup.Tests.TestData;
using ArsenalCleanup.Triage;
using Xunit;

namespace ArsenalCleanup.Tests;

/// <summary>
/// Guards the fixture itself.
///
/// The property tests are only as good as the gear they draw from, and a regeneration that quietly
/// stopped sampling relics, or Armoire-eligible pieces, or gear usable by every class, would leave
/// those suites passing while testing nothing in particular. These assertions describe what the
/// sample has to keep containing for the rest of the suite to mean anything.
/// </summary>
public sealed class RealGameDataTests
{
    [Fact]
    public void CoversEverySlotTheTriageKnowsAbout()
    {
        var slots = RealGameData.Items.Select(i => i.Slot).Distinct().ToList();

        Assert.Equal(12, slots.Count);
        Assert.Contains("soul_crystal", slots);
        Assert.Contains("main", slots);
        Assert.Contains("ring", slots);
    }

    [Fact]
    public void CoversEveryFlagTheTriageHasARuleFor()
    {
        Assert.True(RealGameData.Items.Count(i => i.IsRelic) >= 10, "too few relics to exercise the guard");
        Assert.True(RealGameData.Items.Count(i => i.IsArmoireEligible) >= 10, "too few Armoire-eligible pieces");
        Assert.True(RealGameData.Items.Count(i => i.IsDesynthesisable) >= 10, "too few desynthesisable pieces");
        Assert.True(RealGameData.Items.Count(i => !i.IsDesynthesisable && !i.IsArmoireEligible) >= 10,
            "too few pieces whose only option is the bin");
    }

    [Fact]
    public void CoversTheLevelRangeOfTheWholeGame()
    {
        var equipLevels = RealGameData.Items.Select(i => (int)i.EquipLevel).ToList();

        Assert.Contains(1, equipLevels);
        Assert.True(equipLevels.Max() >= 90, "the sample stops short of current end game");
    }

    [Fact]
    public void ContainsGearUsableByEveryClass()
    {
        // The category that produced two separate bugs: level-1 pieces flagged for all forty-three.
        Assert.Contains(RealGameData.Items, i => i.JobIds.Count >= 40);
        Assert.Contains(RealGameData.Items, i => i.JobIds.Count >= 40 && i.EquipLevel == 1);
    }

    [Fact]
    public void ContainsTheItemsFromEveryReportedBug()
    {
        // Keeping the real numbers in the fixture is what stops a regression being argued about.
        var gloves = RealGameData.Item(8568);
        Assert.Equal("hand", gloves.Slot);
        Assert.Equal(1, gloves.EquipLevel);
        Assert.True(gloves.JobIds.Count >= 40, "the Ehcatl gloves should be usable by every class");

        var cesti = RealGameData.Item(1688);
        Assert.Equal(19, cesti.ItemLevel);
        Assert.Equal(19, cesti.EquipLevel);

        var weatheredFists = RealGameData.Item(31421);
        Assert.Equal(17, weatheredFists.ItemLevel);
        Assert.True(cesti.ItemLevel > weatheredFists.ItemLevel);
    }

    [Fact]
    public void CoversTheGearOnlyABeastmasterCanWear()
    {
        // The sample predates the job, and the bug it is here to guard was invisible without it: a
        // class the game-data library has no column for reads as "no class at all", and every piece
        // only that class can wear comes back unexplained.
        var beastmasterOnly = RealGameData.Items
            .Where(i => i.JobIds.Count == 1 && i.JobIds[0] == KnownJobs.Beastmaster)
            .ToList();

        Assert.True(beastmasterOnly.Count >= 10, $"only {beastmasterOnly.Count} Beastmaster-only pieces");
        Assert.Contains(beastmasterOnly, i => i.Slot == "main");
        Assert.Contains(beastmasterOnly, i => i.Slot == "body");
        Assert.Contains(beastmasterOnly, i => i.Slot == "soul_crystal");

        // And gear it shares with everyone else, which is most of what a player actually owns.
        Assert.True(
            RealGameData.Items.Count(i => i.JobIds.Contains(KnownJobs.Beastmaster)) >= 100,
            "too little shared gear flagged for Beastmaster");
    }

    [Fact]
    public void CarriesRealDefence_NotZeroes()
    {
        // A sample of zeroes would leave the defence rule technically exercised and actually untested.
        Assert.True(
            RealGameData.Items.Count(i => i.DefencePhysical > 0) >= 100,
            "too few pieces with any defence at all");
        Assert.Contains(RealGameData.Items, i => i.DefencePhysical != i.DefenceMagical);

        // No weapon defends at all, and an accessory barely does — a point or two, where a body
        // piece of the same era carries fifty. It is armour the defence rule is really about.
        Assert.All(RealGameData.Items.Where(i => i.Slot == "main"), i => Assert.Equal(0, i.DefencePhysical));
        Assert.All(RealGameData.Items.Where(i => i.Slot == "ring"), i => Assert.True(i.DefencePhysical <= 1));
        Assert.Contains(RealGameData.Items, i => i.Slot == "body" && i.DefencePhysical >= 50);
    }

    [Fact]
    public void KeepsThePairWhereItemLevelAndDefenceDisagree()
    {
        // The whole argument for judging a Beastmaster on defence, in two real coats: eight item
        // levels apart, and the higher one defends less. Invent fixtures and you never write this
        // down; the game ships it.
        var sturdy = RealGameData.Item(3009);
        var flimsy = RealGameData.Item(3076);

        Assert.True(flimsy.ItemLevel > sturdy.ItemLevel, "the sample no longer holds the item level inversion");
        Assert.True(flimsy.DefencePhysical < sturdy.DefencePhysical, "the sample no longer holds the defence inversion");
        Assert.Contains(KnownJobs.Beastmaster, sturdy.JobIds);
        Assert.Contains(KnownJobs.Beastmaster, flimsy.JobIds);
    }

    [Fact]
    public void CarriesEnoughRingsToFillBothHandsSeveralTimesOver()
    {
        // Three rings a single class can wear is the smallest armoury in which the second-best one
        // still matters, and the property tests need more than one such class to stumble onto it.
        var rings = RealGameData.Items.Where(i => i.Slot == "ring").ToList();

        Assert.True(rings.Count >= 10, $"only {rings.Count} rings in the sample");
        Assert.True(
            rings.Count(i => i.JobIds.Contains(KnownJobs.Beastmaster)) >= 3,
            "too few rings a Beastmaster can wear");
    }

    [Fact]
    public void KnowsTheRealClassesAndWhereTheyBegin()
    {
        Assert.Equal(43, RealGameData.Jobs.Count);

        // Beastmaster is a class like any other as far as the triage is concerned; what makes it
        // awkward is the game-data library, not the game.
        var beastmaster = RealGameData.Jobs.Jobs.Single(j => j.Id == KnownJobs.Beastmaster);
        Assert.Equal("BST", beastmaster.LookupAbbreviation);
        Assert.Equal(1, beastmaster.StartingLevel);

        var gunbreaker = RealGameData.Jobs.Jobs.Single(j => j.LookupAbbreviation == "GNB");
        Assert.Equal(60, gunbreaker.StartingLevel);
        Assert.Equal("PSB", gunbreaker.DisplayAbbreviation);

        var viper = RealGameData.Jobs.Jobs.Single(j => j.LookupAbbreviation == "VPR");
        Assert.Equal(80, viper.StartingLevel);

        // A job that shares its class's experience bar begins where that bar does.
        var ninja = RealGameData.Jobs.Jobs.Single(j => j.LookupAbbreviation == "NIN");
        Assert.Equal(1, ninja.StartingLevel);
        Assert.Equal(29u, ninja.ParentId);
    }

    [Fact]
    public void KeepsBothAbbreviationsApart_WhichIsTheWholePoint()
    {
        // If these ever coincide, the sample has been regenerated from an English client and the
        // lookup bug could come back unnoticed.
        var translated = RealGameData.Jobs.Jobs
            .Count(j => j.LookupAbbreviation != j.DisplayAbbreviation);

        Assert.True(translated >= 20, $"only {translated} classes differ between the two abbreviations");
    }

    [Fact]
    public void RecordsWhichGameVersionItCameFrom()
    {
        // So a failure after a patch is recognisable as staleness rather than as a bug.
        Assert.NotEmpty(RealGameData.GameVersion);
    }
}
