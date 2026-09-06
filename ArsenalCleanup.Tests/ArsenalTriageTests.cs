using System.Linq;
using ArsenalCleanup.Triage;
using Xunit;
using static ArsenalCleanup.Tests.TriageFixtures;

namespace ArsenalCleanup.Tests;

public sealed class ArsenalTriageTests
{
    [Fact]
    public void SetsSoulCrystalsAside_RatherThanJudgingThem()
    {
        // There is exactly one per job and parting with it is never the answer, so comparing them
        // against each other would be meaningless as well as dangerous.
        var crystal = Facts(id: 1, name: "Soul of the Monk", slot: "soul_crystal");
        var weapon = Facts(id: 2, name: "Fists", slot: "main");

        var result = ArsenalTriage.Run([Owned(crystal), Owned(weapon)], Levels((Pugilist, 50)));

        Assert.Equal("Soul of the Monk", Assert.Single(result.SoulCrystals).Name);
        Assert.Equal("Fists", Assert.Single(result.Pieces).Facts.Name);
    }

    [Fact]
    public void ListsEachSoulCrystalOnce_HoweverManyWereRead()
    {
        // The same crystal is read twice when one is worn and one sits in the chest.
        var crystal = Facts(id: 1, name: "Soul of the Monk", slot: "soul_crystal");

        var result = ArsenalTriage.Run(
            [Owned(crystal, equipped: true), Owned(crystal, position: 3)], Levels((Pugilist, 50)));

        Assert.Single(result.SoulCrystals);
    }

    [Fact]
    public void CountsWhatCanGo_AndWhatNeedsAHumanLook()
    {
        var old = Facts(id: 1, name: "Old", itemLevel: 10, armoire: true);
        var relic = Facts(id: 2, name: "Relic", itemLevel: 12, relic: true);
        var best = Facts(id: 3, name: "Best", itemLevel: 20);

        var result = ArsenalTriage.Run(
            [Owned(old), Owned(relic), Owned(best)], Levels((Pugilist, 50)));

        Assert.Equal(1, result.Count(Verdict.Obsolete));
        Assert.Equal(1, result.Count(DisposalMethod.Armoire));
        Assert.Equal(1, result.Count(Verdict.NeedsReview));
        Assert.Equal(2, result.Actionable.Count());
    }

    [Fact]
    public void AnEmptyArmouryProducesAnEmptyResult()
    {
        var result = ArsenalTriage.Run([], Levels((Pugilist, 50)));

        Assert.Empty(result.Pieces);
        Assert.Empty(result.SoulCrystals);
        Assert.Empty(result.Actionable);
    }

    [Fact]
    public void RunsEndToEndOnTheCaseThatPromptedTheFeature()
    {
        // Worn gear lives in its own container, so a triage that only reads the chest never sees the
        // piece in use — and can crown a strictly worse one "best available". Here the worn Cesti
        // must supersede both chest pieces, and must itself be reported as worn.
        var himantes = Facts(id: 1685, name: "Hard Leather Himantes", itemLevel: 14, equipLevel: 14, desynthesisable: true);
        var fists = Facts(id: 31421, name: "Weathered Fists", itemLevel: 17, equipLevel: 15, desynthesisable: true);
        var cesti = Facts(id: 1688, name: "Cesti", itemLevel: 19, equipLevel: 19);

        var result = ArsenalTriage.Run(
            [Owned(himantes, position: 1), Owned(fists, position: 2), Owned(cesti, equipped: true)],
            Levels((Pugilist, 25)));

        var worn = result.Pieces.Single(p => p.Piece.IsEquipped);
        Assert.Equal("Cesti", worn.Facts.Name);
        Assert.Equal(Verdict.KeptBest, worn.Verdict);

        foreach (var name in new[] { "Hard Leather Himantes", "Weathered Fists" })
        {
            var row = result.Pieces.Single(p => p.Facts.Name == name);
            Assert.Equal(Verdict.Obsolete, row.Verdict);
            Assert.Equal("Cesti", row.ReplacedBy?.Name);
            Assert.Equal(DisposalMethod.Desynthesis, row.Disposal);
        }
    }

    [Fact]
    public void EmptyResultReportsNothingAtAll()
    {
        Assert.Empty(TriageResult.Empty.Pieces);
        Assert.Empty(TriageResult.Empty.SoulCrystals);
        Assert.Equal(0, TriageResult.Empty.Count(Verdict.Obsolete));
        Assert.Equal(0, TriageResult.Empty.Count(DisposalMethod.Armoire));
    }
}
