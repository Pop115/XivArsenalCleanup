using ArsenalCleanup.Triage;
using ArsenalCleanup.Windows;
using Xunit;

namespace ArsenalCleanup.Tests;

/// <summary>
/// The abbreviation split, and the bug it exists to prevent.
///
/// A class has two abbreviations: the English one the game's own ClassJobCategory columns are named
/// after, and the translated one the player reads. On a French client 26 of the 43 classes differ —
/// Dancer is DNS, Sage is SAG, Monk is MOI, Dark Knight is CHN. Using the translated one to look up
/// a column matches nothing at all, so more than half the game's classes end up owning no gear and
/// the triage judges the wardrobe against a fraction of it, without ever reporting a problem.
/// </summary>
public sealed class JobDirectoryTests
{
    private static readonly JobInfo Dancer = new(38, "DNC", "DNS", 38);
    private static readonly JobInfo Sage = new(40, "SGE", "SAG", 40);
    private static readonly JobInfo Archer = new(5, "ARC", "ARC", 5);
    private static readonly JobInfo Bard = new(23, "BRD", "BRD", 5);

    private static readonly JobDirectory French = new([Dancer, Sage, Archer, Bard]);

    [Fact]
    public void LooksUpSheetColumnsByTheEnglishAbbreviation()
    {
        Assert.Equal("DNC", Dancer.LookupAbbreviation);
        Assert.Equal("SGE", Sage.LookupAbbreviation);
    }

    [Fact]
    public void ShowsThePlayerTheAbbreviationTheirGameUses()
    {
        Assert.Equal("DNS", French.Display(38));
        Assert.Equal("SAG", French.Display(40));
    }

    [Fact]
    public void ShowsSomethingLegibleForAClassItHasNeverHeardOf()
    {
        // A class added by a patch this build predates must read as odd, never crash a tooltip.
        Assert.Equal("#99", French.Display(99));
    }

    [Fact]
    public void FallsBackToTheLookupAbbreviationWhenNoTranslationExists()
    {
        var directory = new JobDirectory([new JobInfo(1, "GLA", string.Empty, 1)]);

        Assert.Equal("GLA", directory.Display(1));
    }

    [Fact]
    public void ReportsHowManyClassesExist_SoEveryOneOfThemCanBeRecognised()
    {
        Assert.Equal(4, French.Count);
    }

    [Fact]
    public void CarriesLineageForLevelReconciliation()
    {
        var lineages = French.Lineages();

        Assert.Contains(new JobLineage(23, 5), lineages);
        Assert.Contains(new JobLineage(5, 5), lineages);
    }

    [Fact]
    public void AnEmptyDirectoryAnswersWithoutThrowing()
    {
        Assert.Equal(0, JobDirectory.Empty.Count);
        Assert.Equal("#5", JobDirectory.Empty.Display(5));
        Assert.Empty(JobDirectory.Empty.Lineages());
    }

    [Fact]
    public void NamesEveryClassWhenThereAreFewEnoughToRead()
    {
        Assert.Equal("DNS, SAG", Presentation.JobList([38, 40], French));
    }

    [Fact]
    public void SaysEveryClassRatherThanListingThemAll()
    {
        // A level-1 ring usable by the whole game should say so. Forty abbreviations is not a list
        // anybody reads.
        Assert.Equal("every class", Presentation.JobList([38, 40, 5, 23], French));
    }

    [Fact]
    public void CountsTheRestOnceTheListStopsBeingReadable()
    {
        var many = new JobDirectory([.. System.Linq.Enumerable.Range(1, 20)
            .Select(i => new JobInfo((uint)i, $"J{i}", $"J{i}", (uint)i))]);

        var listed = Presentation.JobList([1, 2, 3, 4, 5, 6, 7, 8, 9], many);

        Assert.Equal("J1, J2, J3, J4, J5, J6 +3 more", listed);
    }

    [Fact]
    public void SaysNothingWhenNoClassIsConcerned()
    {
        Assert.Empty(Presentation.JobList([], French));
    }
}
