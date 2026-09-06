using ArsenalCleanup.Triage;
using Xunit;

namespace ArsenalCleanup.Tests;

public sealed class EquipSlotResolverTests
{
    private static EquipSlotFlags Occupying(string slot)
    {
        sbyte On(string s) => (sbyte)(s == slot ? 1 : 0);
        return new EquipSlotFlags(
            On("main"), On("offhand"), On("head"), On("body"), On("gloves"), On("waist"),
            On("legs"), On("feet"), On("ears"), On("neck"), On("wrists"), On("fingerL"),
            On("fingerR"), On("soul"));
    }

    [Theory]
    [InlineData("main", "main")]
    [InlineData("offhand", "offhand")]
    [InlineData("head", "head")]
    [InlineData("body", "body")]
    [InlineData("gloves", "hand")]
    [InlineData("legs", "legs")]
    [InlineData("feet", "feet")]
    [InlineData("ears", "ear")]
    [InlineData("neck", "neck")]
    [InlineData("wrists", "wrist")]
    [InlineData("fingerL", "ring")]
    [InlineData("fingerR", "ring")]
    [InlineData("soul", "soul_crystal")]
    public void MapsEachOccupiedSlotToItsName(string occupied, string expected)
    {
        Assert.Equal(expected, EquipSlotResolver.Resolve(Occupying(occupied)));
    }

    [Fact]
    public void FilesATwoHanderUnderTheHandItOccupies_NotTheOneItBlocks()
    {
        // A two-handed weapon occupies the main hand (1) and blocks the off hand (-1). Reading the
        // blocked flag as "occupies" would file every greatsword under off hand.
        var twoHander = new EquipSlotFlags(1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.Equal("main", EquipSlotResolver.Resolve(twoHander));
    }

    [Fact]
    public void FilesAFullBodyPieceUnderBody_NotUnderWhateverItCovers()
    {
        var robe = new EquipSlotFlags(0, 0, -1, 1, -1, 0, -1, -1, 0, 0, 0, 0, 0, 0);

        Assert.Equal("body", EquipSlotResolver.Resolve(robe));
    }

    [Fact]
    public void HasNoSlotForABelt()
    {
        // Belts were removed from the game; nothing can ever occupy that slot again.
        var belt = new EquipSlotFlags(0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.Null(EquipSlotResolver.Resolve(belt));
    }

    [Fact]
    public void HasNoSlotForAnEmptyRow()
    {
        Assert.Null(EquipSlotResolver.Resolve(default));
    }
}

public sealed class RelicDetectionTests
{
    [Theory]
    [InlineData("[Manderville Weapon]\nA weapon of legend.")]
    [InlineData("[Antique Weapon]")]
    [InlineData("[Arme majestueuse des Manderville]")]
    public void RecognisesTheBracketedTagEveryRelicLineUses(string description)
    {
        Assert.True(RelicDetection.LooksLikeRelic(description));
    }

    [Theory]
    [InlineData("A sturdy pair of fists.")]
    [InlineData("Item level varies with the wearer's level.")]
    [InlineData("")]
    [InlineData(" [not at the start]")]
    public void LeavesOrdinaryGearAlone(string description)
    {
        Assert.False(RelicDetection.LooksLikeRelic(description));
    }
}

public sealed class JobLevelTableTests
{
    private const uint Archer = 5;
    private const uint Bard = 23;
    private const uint Gladiator = 1;
    private const uint Paladin = 19;

    private static readonly JobLineage[] Lineages =
    [
        new(Archer, Archer),
        new(Bard, Archer),
        new(Gladiator, Gladiator),
        new(Paladin, Gladiator),
    ];

    [Fact]
    public void GivesAJobItsBaseClassLevel_SinceTheyShareOneExperienceBar()
    {
        // A level-41 Bard is a level-41 Archer. A source reporting only the class leaves the job at
        // zero, and every item both can equip would then be unjudgeable.
        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [Archer] = 41, [Bard] = 0 }, Lineages);

        Assert.Equal(41, levels[Bard]);
    }

    [Fact]
    public void GivesABaseClassItsJobsLevel_WhenOnlyTheJobWasReported()
    {
        // The direction that actually bites: most Archer gear is filed under Archer, and a source
        // that only knows "Bard 41" would leave that whole wardrobe unjudged.
        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [Bard] = 41 }, Lineages);

        Assert.Equal(41, levels[Archer]);
    }

    [Fact]
    public void LeavesAJobAtZero_WhenItsWholeLineageIsUntouched()
    {
        // Propagation can only ever carry a level across a shared bar. It must never invent one.
        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [Archer] = 41 }, Lineages);

        Assert.Equal(0, levels.TryGetValue(Paladin, out var paladin) ? paladin : 0);
    }

    [Fact]
    public void NeverLowersALevelThatWasAlreadyKnown()
    {
        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [Archer] = 30, [Bard] = 80 }, Lineages);

        Assert.Equal(80, levels[Bard]);
        Assert.Equal(30, levels[Archer]);
    }

    [Fact]
    public void CopesWithAJobThatIsItsOwnParent()
    {
        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [Archer] = 41 }, Lineages);

        Assert.Equal(41, levels[Archer]);
    }

    [Fact]
    public void NobodyIsBelowTheLevelTheirClassBeginsAt()
    {
        // Gunbreaker has no base class and unlocks at 60, so a character who has never touched it
        // would begin there — never at 1. Assuming otherwise made level-1 gear read as "your best"
        // for a class that cannot exist below 60.
        const uint Gunbreaker = 37;
        const uint Viper = 41;
        var jobs = new[]
        {
            new JobLineage(Gunbreaker, Gunbreaker, StartingLevel: 60),
            new JobLineage(Viper, Viper, StartingLevel: 80),
        };

        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [Gunbreaker] = 0, [Viper] = 0 }, jobs);

        Assert.Equal(60, levels[Gunbreaker]);
        Assert.Equal(80, levels[Viper]);
    }

    [Fact]
    public void LeavesALevelAboveTheStartingOneAlone()
    {
        const uint Gunbreaker = 37;
        var jobs = new[] { new JobLineage(Gunbreaker, Gunbreaker, StartingLevel: 60) };

        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [Gunbreaker] = 90 }, jobs);

        Assert.Equal(90, levels[Gunbreaker]);
    }

    [Fact]
    public void AJobSharingItsClassesBar_BeginsWhereThatClassDoes()
    {
        // Ninja reports a starting level of 1, not 30, because its level *is* the Rogue bar.
        var jobs = new[]
        {
            new JobLineage(29, 29, StartingLevel: 1),
            new JobLineage(30, 29, StartingLevel: 1),
        };

        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [29] = 0, [30] = 0 }, jobs);

        Assert.Equal(1, levels[29]);
        Assert.Equal(1, levels[30]);
    }

    [Fact]
    public void FloorsAfterReconcilingALineage_NotBefore()
    {
        // Bard 41 must lift Archer to 41, and the floor must not then drag either back down.
        var jobs = new[]
        {
            new JobLineage(5, 5, StartingLevel: 1),
            new JobLineage(23, 5, StartingLevel: 1),
        };

        var levels = JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int> { [23] = 41 }, jobs);

        Assert.Equal(41, levels[5]);
        Assert.Equal(41, levels[23]);
    }

    [Fact]
    public void ProducesNothingFromNothing()
    {
        Assert.Empty(JobLevelTable.WithLineageFilledIn(
            new System.Collections.Generic.Dictionary<uint, int>(), Lineages));
    }
}
