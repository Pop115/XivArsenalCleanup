using System.Collections.Generic;
using System.Linq;
using ArsenalCleanup.Tests.TestData;
using ArsenalCleanup.Triage;
using ArsenalCleanup.Windows;
using Xunit;

namespace ArsenalCleanup.Tests;

/// <summary>
/// Whole armouries a Beastmaster could actually own, built from gear that actually exists.
///
/// Every item here is a real row from the game's sheets, with its real item level, equip level and
/// defence — see <see cref="RealGameData"/>. That matters more than usual for this job: the awkward
/// pairs are the point, and nobody inventing fixtures writes down a coat that is eight item levels
/// above another and six defence below it. The game did.
///
/// These run the whole triage, not one rule, because the bugs they cover were only visible at that
/// level: a piece can be scrap for one class and the best thing a Beastmaster owns, and what the
/// player finally reads is the answer after both have had their say.
/// </summary>
public sealed class BeastmasterScenarioTests
{
    private const uint Beastmaster = KnownJobs.Beastmaster;

    /// <summary>Real Beastmaster gear: the job's own line, which no other class can wear.</summary>
    private const uint HerderAxe = 47470;        // item level 5, wearable from 1
    private const uint HerderCoat = 50728;       // item level 1, defence 26
    private const uint HerderCirclet = 50727;    // item level 1, defence 20
    private const uint MastersAxe = 50742;       // item level 90, wearable from 50
    private const uint MastersFurs = 50745;      // item level 90, defence 177
    private const uint MastersFursPlusOne = 50752;   // item level 100, defence 180
    private const uint MastersFursPlusTwo = 50759;   // item level 110, defence 182
    private const uint SoulOfTheBeastmaster = 47474;

    /// <summary>Coats any class can wear, and where the two yardsticks disagree.</summary>
    private const uint MothEatenCoatee = 3009;   // item level 32, defence 36 / 62
    private const uint VintageCoatee = 3076;     // item level 40, defence 30 / 61
    private const uint DatedLinenCoatee = 947;   // item level 50, defence 58 / 102
    private const uint PatriciansCoatee = 3215;  // item level 50, defence 42 / 85

    /// <summary>Real rings, none of which carry any defence worth the name.</summary>
    private const uint CoralRing = 4434;         // item level 19
    private const uint SerpentRing = 4436;       // item level 25
    private const uint SunstoneRing = 4443;      // item level 29

    private static OwnedPiece Owns(uint itemId, bool worn = false, int position = 0)
    {
        var facts = RealGameData.Item(itemId);
        return new OwnedPiece(facts.ItemId, facts.Slot, position, worn, facts);
    }

    /// <summary>A character who has levelled everything, Beastmaster included.</summary>
    private static Dictionary<uint, int> EveryClassAt(int level) =>
        RealGameData.Jobs.Jobs.ToDictionary(j => j.Id, _ => level);

    private static TriagedPiece Row(TriageResult result, uint itemId) =>
        result.Pieces.Single(p => p.Facts.ItemId == itemId);

    private static string Reason(TriageResult result, uint itemId) =>
        Presentation.Reason(Row(result, itemId), RealGameData.Jobs);

    [Fact]
    public void TheJobsOwnGearIsJudged_NotShruggedAt()
    {
        // The bug as reported: every Beastmaster piece came back as "check yourself". Nothing was
        // wrong with the gear — the job simply had no column in the game-data library this plugin
        // builds against, so its equipment resolved to no class at all and nothing could be compared.
        var armoury = new List<OwnedPiece>
        {
            Owns(HerderAxe), Owns(MastersAxe), Owns(HerderCirclet),
            Owns(HerderCoat), Owns(MastersFurs), Owns(SoulOfTheBeastmaster),
        };

        var result = ArsenalTriage.Run(armoury, EveryClassAt(50));

        Assert.Equal(0, result.Count(Verdict.NeedsReview));
        Assert.All(result.Pieces, piece => Assert.Contains(Beastmaster, piece.Jobs));
        Assert.Single(result.SoulCrystals);
    }

    [Fact]
    public void AndTheVerdictsNameTheJob_InTheAbbreviationThePlayerSees()
    {
        var result = ArsenalTriage.Run([Owns(HerderAxe), Owns(MastersAxe)], EveryClassAt(50));

        // "DRE" on a French client, "BST" on an English one: whichever the rest of the game shows.
        Assert.Contains(RealGameData.Jobs.Display(Beastmaster), Reason(result, MastersAxe));
    }

    [Fact]
    public void ASturdierCoatIsNotBinnedForAHigherLevelledOne()
    {
        // The complaint behind the defence rule, and the reason the game's own "recommended gear"
        // button is poor advice here: it would hand over the Vintage Coatee and its six fewer points
        // of physical defence. Every other class in the game does prefer it, and the piece is still
        // kept, because one class that still wants it is enough.
        var result = ArsenalTriage.Run(
            [Owns(MothEatenCoatee), Owns(VintageCoatee)], EveryClassAt(50));

        var kept = Row(result, MothEatenCoatee);
        Assert.NotEqual(Verdict.Obsolete, kept.Verdict);
        Assert.Equal(DisposalMethod.None, kept.Disposal);
        Assert.Equal([Beastmaster], kept.RelevantJobs);
        Assert.Contains("defence", Reason(result, MothEatenCoatee));
    }

    [Fact]
    public void TheDefenceRuleCanOnlyEverProtectAPieceEveryClassCanWear()
    {
        // Two coats of the same item level, one better at both kinds of defence. A Beastmaster has
        // no use for the weaker — but every other class in the game has as much claim on it as the
        // Beastmaster does, and a piece goes only when none of them still wants it. So the rule
        // speaks up for gear and never against it here; where it clears anything out is the job's
        // own line, which nobody else can wear.
        var result = ArsenalTriage.Run(
            [Owns(PatriciansCoatee), Owns(DatedLinenCoatee)], EveryClassAt(50));

        var kept = Row(result, PatriciansCoatee);
        Assert.NotEqual(Verdict.Obsolete, kept.Verdict);
        Assert.DoesNotContain(Beastmaster, kept.RelevantJobs);

        // Beastmaster alone does reach the other answer, which is what the merge is overruling.
        var judged = Classifier.Classify(
            [Owns(PatriciansCoatee), Owns(DatedLinenCoatee)],
            new Dictionary<uint, int> { [Beastmaster] = 50 });
        var beastmasterRow = judged.Single(j =>
            j.JobId == Beastmaster && j.Facts.ItemId == PatriciansCoatee);

        Assert.Equal(Verdict.Obsolete, beastmasterRow.Verdict);
        Assert.Equal(DatedLinenCoatee, beastmasterRow.ReplacedBy?.ItemId);
    }

    [Fact]
    public void TheJobsOwnLineIsWhereADefenceVerdictActuallyBites()
    {
        // Nobody else can wear it, so there is no other class to overrule the answer — and the
        // weakest set is cleared out with the Armoire offered first, appearance kept, nothing lost.
        var result = ArsenalTriage.Run(
            [Owns(MastersFurs), Owns(MastersFursPlusTwo)], EveryClassAt(50));

        var replaced = Row(result, MastersFurs);
        Assert.Equal(Verdict.Obsolete, replaced.Verdict);
        Assert.Equal(MastersFursPlusTwo, replaced.ReplacedBy?.ItemId);
        Assert.Equal(DisposalMethod.Armoire, replaced.Disposal);
        Assert.Contains("defence 182", Reason(result, MastersFurs));
    }

    [Fact]
    public void OneBetterRingIsNoReasonToPartWithARing()
    {
        // Two hands, two rings. The better one does not free the hand the other is on.
        var result = ArsenalTriage.Run([Owns(CoralRing), Owns(SunstoneRing)], EveryClassAt(50));

        Assert.All(result.Pieces, piece => Assert.NotEqual(Verdict.Obsolete, piece.Verdict));
        Assert.Contains("two at once", Reason(result, CoralRing));
    }

    [Fact]
    public void TwoBetterRingsAre()
    {
        var result = ArsenalTriage.Run(
            [Owns(CoralRing), Owns(SerpentRing), Owns(SunstoneRing)], EveryClassAt(50));

        var replaced = Row(result, CoralRing);
        Assert.Equal(Verdict.Obsolete, replaced.Verdict);

        // Named after the weaker of the two that displace it — "even this beats it".
        Assert.Equal(SerpentRing, replaced.ReplacedBy?.ItemId);
        Assert.Equal(Verdict.KeptBest, Row(result, SerpentRing).Verdict);
        Assert.Equal(Verdict.KeptBest, Row(result, SunstoneRing).Verdict);
    }

    [Fact]
    public void AWornRingIsNotPulledOffAHandForASingleUpgrade()
    {
        var result = ArsenalTriage.Run(
            [Owns(CoralRing, worn: true), Owns(SunstoneRing)], EveryClassAt(50));

        Assert.Equal(Verdict.KeptBest, Row(result, CoralRing).Verdict);
    }

    [Fact]
    public void GearWaitingOnTheJobsLevelSaysSo_RatherThanLookingUnrecognised()
    {
        // A freshly unlocked Beastmaster: the starting kit is wearable, the level-50 set is not, and
        // neither is a mystery.
        var result = ArsenalTriage.Run(
            [Owns(HerderCoat), Owns(MastersFurs)],
            new Dictionary<uint, int> { [Beastmaster] = 1 });

        Assert.Equal(Verdict.KeptBest, Row(result, HerderCoat).Verdict);

        var waiting = Row(result, MastersFurs);
        Assert.Equal(Verdict.KeptUntilLevel, waiting.Verdict);
        Assert.Contains("level 50", Reason(result, MastersFurs));
    }

    [Fact]
    public void UpgradingWithinTheJobsOwnLineStillClearsOutTheOldPiece()
    {
        // Each step of the line is better at everything, so nothing about the defence rule protects
        // a piece that has genuinely been outgrown — including the one being worn.
        var result = ArsenalTriage.Run(
            [Owns(MastersFurs), Owns(MastersFursPlusOne, worn: true), Owns(MastersFursPlusTwo)],
            EveryClassAt(50));

        Assert.Equal(Verdict.Obsolete, Row(result, MastersFurs).Verdict);

        var worn = Row(result, MastersFursPlusOne);
        Assert.Equal(Verdict.Obsolete, worn.Verdict);
        Assert.Equal(MastersFursPlusTwo, worn.ReplacedBy?.ItemId);
        Assert.Equal(DisposalMethod.Armoire, worn.Disposal);

        Assert.Equal(Verdict.KeptBest, Row(result, MastersFursPlusTwo).Verdict);
    }

    [Fact]
    public void AWholeBeastmasterArmouryComesBackWithSomethingToSayAboutEveryPiece()
    {
        // The end-to-end shape of the fix: a mixed chest of the job's own gear, shared coats and
        // rings, and not one row left to the player to work out for themselves.
        var armoury = new List<OwnedPiece>
        {
            Owns(HerderAxe), Owns(MastersAxe, worn: true),
            Owns(HerderCirclet), Owns(HerderCoat), Owns(MastersFurs, worn: true),
            Owns(MothEatenCoatee), Owns(VintageCoatee), Owns(DatedLinenCoatee), Owns(PatriciansCoatee),
            Owns(CoralRing), Owns(SerpentRing, position: 1), Owns(SunstoneRing, position: 2),
            Owns(SoulOfTheBeastmaster),
        };

        var result = ArsenalTriage.Run(armoury, EveryClassAt(60));

        Assert.Equal(12, result.Pieces.Count);
        Assert.Equal(0, result.Count(Verdict.NeedsReview));
        Assert.All(result.Pieces, piece => Assert.NotEmpty(Presentation.Reason(piece, RealGameData.Jobs)));

        // Everything flagged for disposal says how to part with it, and nothing else does.
        foreach (var piece in result.Pieces)
        {
            Assert.Equal(piece.Verdict == Verdict.Obsolete, piece.Disposal != DisposalMethod.None);
        }
    }
}
