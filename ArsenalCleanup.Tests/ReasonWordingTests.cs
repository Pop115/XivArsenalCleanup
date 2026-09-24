using System.Collections.Generic;
using System.Linq;
using ArsenalCleanup.Triage;
using ArsenalCleanup.Windows;
using Xunit;
using static ArsenalCleanup.Tests.TriageFixtures;

namespace ArsenalCleanup.Tests;

/// <summary>
/// What the player actually reads.
///
/// The wording used to say "this job" everywhere — accurate, and useless to someone holding a piece
/// nine classes can wear and wondering which of theirs is waiting on it.
/// </summary>
public sealed class ReasonWordingTests
{
    private static readonly JobDirectory Jobs = new([
        new JobInfo(2, "PGL", "PGL", 2),
        new JobInfo(20, "MNK", "MOI", 2),
        new JobInfo(38, "DNC", "DNS", 38),
        new JobInfo(40, "SGE", "SAG", 40),
        new JobInfo(Beastmaster, "BST", "DRE", Beastmaster),
    ]);

    private static string Reason(IReadOnlyList<OwnedPiece> owned, params (uint Job, int Level)[] levels) =>
        Presentation.Reason(ArsenalTriage.Run(owned, Levels(levels)).Pieces[0], Jobs);

    private static string ReasonFor(string name, IReadOnlyList<OwnedPiece> owned, params (uint Job, int Level)[] levels) =>
        Presentation.Reason(ArsenalTriage.Run(owned, Levels(levels)).Pieces.First(p => p.Facts.Name == name), Jobs);

    [Fact]
    public void NamesTheClassesStillWaitingOnAPieceTheyCannotWearYet()
    {
        // A second piece is needed for the level rule to come into play at all: a lone piece is kept
        // whatever its level, and never reaches the comparison.
        var usable = Facts(id: 1, name: "Current fists", itemLevel: 50, equipLevel: 10, jobs: [Pugilist, Monk]);
        var future = Facts(id: 2, name: "Future fists", itemLevel: 400, equipLevel: 80, jobs: [Pugilist, Monk]);

        var reason = ReasonFor("Future fists", [Owned(usable), Owned(future)], (Pugilist, 25), (Monk, 25));

        Assert.Contains("PGL", reason);
        Assert.Contains("MOI", reason);
        Assert.Contains("80", reason);
        Assert.DoesNotContain("this job", reason);
    }

    [Fact]
    public void NamesTheClassesAPieceIsCurrentlyBestFor()
    {
        var worse = Facts(id: 1, name: "Worse", itemLevel: 10, jobs: [Pugilist, Monk]);
        var better = Facts(id: 2, name: "Better", itemLevel: 20, jobs: [Pugilist, Monk]);

        var reason = ReasonFor("Better", [Owned(worse), Owned(better)], (Pugilist, 50), (Monk, 50));

        Assert.Contains("PGL", reason);
        Assert.Contains("MOI", reason);
    }

    [Fact]
    public void SaysEveryClassRatherThanListingTheWholeGame()
    {
        var universal = Facts(id: 1, name: "Ring of all trades", slot: "ring", itemLevel: 1, equipLevel: 1,
            jobs: [.. Jobs.Jobs.Select(j => j.Id)]);

        var reason = Reason([Owned(universal)], (2, 50), (20, 50), (38, 50), (40, 50));

        Assert.Contains("every class", reason);
    }

    [Fact]
    public void NamesTheReplacementRatherThanJustSayingThereIsOne()
    {
        var old = Facts(id: 1, name: "Weathered Fists", itemLevel: 17);
        var better = Facts(id: 2, name: "Cesti", itemLevel: 19);

        var reason = ReasonFor("Weathered Fists", [Owned(old), Owned(better)], (Pugilist, 50));

        Assert.Contains("Cesti", reason);
        Assert.Contains("19", reason);
    }

    [Fact]
    public void ExplainsWhyARelicIsBeingProtected()
    {
        var relic = Facts(id: 1, name: "Manderville Fists", itemLevel: 10, relic: true);
        var better = Facts(id: 2, name: "Ordinary Fists", itemLevel: 20);

        var reason = ReasonFor("Manderville Fists", [Owned(relic), Owned(better)], (Pugilist, 50));

        Assert.Contains("Relic", reason);
        Assert.Contains("Ordinary Fists", reason);
    }

    [Fact]
    public void SaysPlainlyWhenTheGameHasNoDataForAnItem()
    {
        var reason = Reason([Unresolved(itemId: 4242)], (Pugilist, 50));

        Assert.Contains("no entry", reason);
    }

    [Fact]
    public void SaysPlainlyWhenNoClassCouldBeResolved()
    {
        var reason = Reason([Owned(Facts(id: 1, name: "Jobless", jobs: []))], (Pugilist, 50));

        Assert.Contains("No class", reason);
    }

    [Fact]
    public void NeverSaysThisJob_ForAnyVerdictAnItemCanReach()
    {
        // A blanket guard: the vague wording must not creep back in through a branch nobody
        // remembered to look at.
        var pieces = new[]
        {
            Owned(Facts(id: 1, name: "Lonely", itemLevel: 10, jobs: [Pugilist])),
            Owned(Facts(id: 2, name: "Future", itemLevel: 400, equipLevel: 90, jobs: [Pugilist])),
            Owned(Facts(id: 3, name: "Beaten", itemLevel: 5, jobs: [Monk])),
            Owned(Facts(id: 4, name: "Winner", itemLevel: 50, jobs: [Monk])),
            Unresolved(itemId: 999),
            Owned(Facts(id: 5, name: "Jobless", jobs: [])),
        };

        var result = ArsenalTriage.Run(pieces, Levels((Pugilist, 25), (Monk, 25)));

        foreach (var piece in result.Pieces)
        {
            var reason = Presentation.Reason(piece, Jobs);
            Assert.DoesNotContain("this job", reason, System.StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(reason);
        }
    }

    [Fact]
    public void StillExplainsItself_WhenNoClassCanBeNamed()
    {
        // Every reason has a shorter form for the case where the classes are unknown — a job list
        // that could not be resolved must degrade to a plain sentence, never to a dangling "for ".
        var piece = ArsenalTriage.Run([Owned(Facts(id: 1))], Levels((Pugilist, 50))).Pieces[0];
        var anonymous = piece with { RelevantJobs = [], Jobs = [] };

        foreach (var code in System.Enum.GetValues<ReasonCode>())
        {
            var worded = Presentation.Reason(anonymous with { Reason = code }, Jobs);

            Assert.NotEmpty(worded);
            Assert.DoesNotContain("for .", worded, System.StringComparison.Ordinal);
            Assert.DoesNotContain("()", worded, System.StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SaysWhatIsWaitedFor_EvenWithNoClassNamed()
    {
        var piece = ArsenalTriage.Run([Owned(Facts(id: 1))], Levels((Pugilist, 50))).Pieces[0];
        var anonymous = piece with { RelevantJobs = [], Reason = ReasonCode.NotAtLevelYet, RequiredLevel = 60 };

        Assert.Contains("60", Presentation.Reason(anonymous, Jobs));
    }

    [Fact]
    public void StillSaysSomething_WhenEvenTheLevelIsMissing()
    {
        var piece = ArsenalTriage.Run([Owned(Facts(id: 1))], Levels((Pugilist, 50))).Pieces[0];
        var anonymous = piece with { RelevantJobs = [], Reason = ReasonCode.NotAtLevelYet, RequiredLevel = null };

        Assert.NotEmpty(Presentation.Reason(anonymous, Jobs));
    }

    [Fact]
    public void DescribesAReplacementItCannotName()
    {
        var piece = ArsenalTriage.Run([Owned(Facts(id: 1))], Levels((Pugilist, 50))).Pieces[0];

        foreach (var code in new[] { ReasonCode.SupersededNow, ReasonCode.OutclassedByEarlier })
        {
            var orphaned = piece with { Reason = code, ReplacedBy = null };

            Assert.NotEmpty(Presentation.Reason(orphaned, Jobs));
        }
    }

    [Fact]
    public void GivesEveryReasonCodeWordingOfItsOwn()
    {
        // Nothing on screen should ever read as an enum name. Swept over a piece of a slot worn once
        // and one worn twice, and with the classes stripped out, because each of those has wording
        // of its own to fall back through.
        var single = ArsenalTriage.Run([Owned(Facts(id: 1))], Levels((Pugilist, 50))).Pieces[0];
        var ring = ArsenalTriage.Run(
            [Owned(Facts(id: 1, slot: "ring"))], Levels((Pugilist, 50))).Pieces[0];

        foreach (var piece in new[] { single, ring, single with { RelevantJobs = [] }, ring with { RelevantJobs = [] } })
        {
            foreach (var code in System.Enum.GetValues<ReasonCode>())
            {
                var worded = Presentation.Reason(piece with { Reason = code }, Jobs);
                Assert.NotEmpty(worded);
                Assert.DoesNotContain(code.ToString(), worded, System.StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void SaysWhyARingSurvivesSomethingBetter()
    {
        // "Your best for this slot" would be a lie about a ring that is plainly not the best one
        // owned. What is true, and what the player needs, is that both are worn.
        var ordinary = Facts(id: 1, name: "Ordinary ring", slot: "ring", itemLevel: 10, jobs: [Pugilist]);
        var better = Facts(id: 2, name: "Better ring", slot: "ring", itemLevel: 50, jobs: [Pugilist]);

        var reason = ReasonFor("Ordinary ring", [Owned(ordinary), Owned(better)], (Pugilist, 50));

        Assert.Contains("two at once", reason);
        Assert.Contains("PGL", reason);
    }

    [Fact]
    public void SaysWhyARingIsFinallySpare()
    {
        var ordinary = Facts(id: 1, name: "Ordinary ring", slot: "ring", itemLevel: 10, jobs: [Pugilist]);
        var good = Facts(id: 2, name: "Good ring", slot: "ring", itemLevel: 40, jobs: [Pugilist]);
        var best = Facts(id: 3, name: "Best ring", slot: "ring", itemLevel: 50, jobs: [Pugilist]);

        var reason = ReasonFor(
            "Ordinary ring", [Owned(ordinary), Owned(good), Owned(best)], (Pugilist, 50));

        Assert.Contains("two at once", reason);
        Assert.Contains("Good ring", reason);
    }

    [Fact]
    public void SaysWhyAFutureRingIsPointless_WithoutClaimingOneRingReplacedIt()
    {
        var future = Facts(id: 1, name: "Future ring", slot: "ring", itemLevel: 30, equipLevel: 60, jobs: [Pugilist]);
        var earlier = Facts(id: 2, name: "Earlier ring", slot: "ring", itemLevel: 50, equipLevel: 55, jobs: [Pugilist]);
        var second = Facts(id: 3, name: "Second ring", slot: "ring", itemLevel: 45, equipLevel: 50, jobs: [Pugilist]);

        var reason = ReasonFor(
            "Future ring", [Owned(future), Owned(earlier), Owned(second)], (Pugilist, 20));

        Assert.Contains("two at once", reason);
        Assert.Contains("unlock no later", reason);
    }

    [Fact]
    public void NamesTheYardstickAVerdictWasActuallyReachedWith()
    {
        // A Beastmaster reading "replaced by something at a higher item level" would have no way to
        // tell whether the plugin had applied the defence rule at all.
        var lesser = Facts(id: 1, name: "Lesser coat", slot: "body", itemLevel: 50,
            jobs: [Beastmaster], defence: 42, magicDefence: 85);
        var sturdier = Facts(id: 2, name: "Sturdier coat", slot: "body", itemLevel: 50,
            jobs: [Beastmaster], defence: 58, magicDefence: 102);

        var reason = ReasonFor("Lesser coat", [Owned(lesser), Owned(sturdier)], (Beastmaster, 50));

        Assert.Contains("Sturdier coat", reason);
        Assert.Contains("defence 58 physical and 102 magical", reason);
        Assert.Contains("item level 50", reason);
    }

    [Fact]
    public void SaysWhatABeastmastersBestPieceIsBestAt()
    {
        var lesser = Facts(id: 1, name: "Lesser coat", slot: "body", itemLevel: 50, jobs: [Beastmaster], defence: 42);
        var sturdier = Facts(id: 2, name: "Sturdier coat", slot: "body", itemLevel: 50, jobs: [Beastmaster], defence: 58);

        var reason = ReasonFor("Sturdier coat", [Owned(lesser), Owned(sturdier)], (Beastmaster, 50));

        Assert.Contains("best defence", reason);
        Assert.Contains("DRE", reason);
    }

    [Fact]
    public void TheLineUnderAPieceFollowsTheSameYardstickAsItsVerdict()
    {
        var ordinary = Facts(id: 1, name: "Ordinary", itemLevel: 50, equipLevel: 45, jobs: [Pugilist]);
        var beastmasters = Facts(id: 2, name: "Furs", slot: "body", itemLevel: 90, equipLevel: 50,
            jobs: [Beastmaster], defence: 177);

        var result = ArsenalTriage.Run(
            [Owned(ordinary), Owned(beastmasters)], Levels((Pugilist, 50), (Beastmaster, 50)));

        var ordinaryRow = result.Pieces.First(p => p.Facts.Name == "Ordinary");
        var beastmasterRow = result.Pieces.First(p => p.Facts.Name == "Furs");

        Assert.Equal("item level 50, wearable from level 45", Presentation.Specs(ordinaryRow));
        Assert.Equal("defence 177, item level 90, wearable from level 50", Presentation.Specs(beastmasterRow));
    }

}
