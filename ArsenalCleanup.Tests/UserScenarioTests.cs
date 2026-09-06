using System.Collections.Generic;
using System.Linq;
using ArsenalCleanup.Triage;
using ArsenalCleanup.Windows;
using Xunit;
using static ArsenalCleanup.Tests.TriageFixtures;

namespace ArsenalCleanup.Tests;

/// <summary>
/// Whole journeys, from what a character owns to what the player is told.
///
/// Where the other suites check one rule at a time, these describe situations a real player is in —
/// a fresh character, someone levelling a second class, a hoarder at end game — and assert on the
/// answer they get, wording included. They are the tests that would catch a rule being individually
/// correct and collectively useless.
/// </summary>
public sealed class UserScenarioTests
{
    private static readonly JobDirectory Jobs = new([
        new JobInfo(1, "GLA", "GLA", 1),
        new JobInfo(2, "PGL", "PGL", 2),
        new JobInfo(5, "ARC", "ARC", 5),
        new JobInfo(19, "PLD", "PLD", 1),
        new JobInfo(20, "MNK", "MOI", 2),
        new JobInfo(23, "BRD", "BRD", 5),
        new JobInfo(38, "DNC", "DNS", 38),
    ]);

    private static IReadOnlyList<uint> AllJobs => [.. Jobs.Jobs.Select(j => j.Id)];

    private static string Reason(TriageResult result, string name) =>
        Presentation.Reason(result.Pieces.First(p => p.Facts.Name == name), Jobs);

    private static TriagedPiece Piece(TriageResult result, string name) =>
        result.Pieces.First(p => p.Facts.Name == name);

    [Fact]
    public void ABrandNewCharacter_IsToldToKeepEverything_AndNothingReadsAsAbsurd()
    {
        // Level 1 Gladiator, starting gear, nothing else owned. Every class the player has never
        // touched reads as level 0 — which must not turn a level-1 item into "keep until level 1".
        var pieces = new[]
        {
            Owned(Facts(id: 1, name: "Weathered Sword", slot: "main", itemLevel: 1, equipLevel: 1, jobs: [1]), equipped: true),
            Owned(Facts(id: 2, name: "Weathered Ring", slot: "ring", itemLevel: 1, equipLevel: 1, jobs: AllJobs)),
        };

        var result = ArsenalTriage.Run(pieces, Levels((1, 1)));

        Assert.Empty(result.Actionable);
        Assert.DoesNotContain("reaches level 1", Reason(result, "Weathered Ring"));
        Assert.DoesNotContain("level 1, and none of them are there yet", Reason(result, "Weathered Ring"));
    }

    [Fact]
    public void GearForEveryClass_IsNeverHeldBackByAClassThePlayerHasNotStarted()
    {
        // The reported bug: a level-1 item usable by all classes came out "keep until this job
        // reaches level 1" because untouched classes report level 0. Every class starts at 1 the
        // moment it is picked up, so nothing is ever waiting on level 1.
        var universal = Facts(id: 1, name: "Ring of all trades", slot: "ring", itemLevel: 1, equipLevel: 1, jobs: AllJobs);

        var result = ArsenalTriage.Run([Owned(universal)], Levels((1, 40)));

        Assert.Equal(Verdict.KeptOnly, Piece(result, "Ring of all trades").Verdict);
        Assert.NotEqual(Verdict.KeptUntilLevel, Piece(result, "Ring of all trades").Verdict);
    }

    [Fact]
    public void LevellingASecondClass_KeepsTheGearTheFirstOneStillNeeds()
    {
        // Paladin 90, Monk 25. A shared accessory good for Paladin must not be binned because Monk
        // cannot use it yet, and vice versa.
        var shared = Facts(id: 1, name: "Shared ring", slot: "ring", itemLevel: 400, equipLevel: 80, jobs: [19, 20]);
        var lesser = Facts(id: 2, name: "Old ring", slot: "ring", itemLevel: 50, equipLevel: 20, jobs: [19, 20]);

        var result = ArsenalTriage.Run([Owned(shared), Owned(lesser)], Levels((19, 90), (20, 25)));

        // Paladin can wear the good one, so for Paladin the old one is replaced — but Monk is still
        // twenty levels away from the good one and has nothing else.
        Assert.Equal(Verdict.KeptBest, Piece(result, "Old ring").Verdict);
        Assert.Contains("MOI", Reason(result, "Old ring"));
    }

    [Fact]
    public void AWeaponYouAreWearing_CanItselfBeTheOneToReplace()
    {
        // Wearing the worse of two is common after a dungeon: the upgrade is in the chest, unworn.
        var worn = Facts(id: 1, name: "Old blade", slot: "main", itemLevel: 100, equipLevel: 50, jobs: [1]);
        var spare = Facts(id: 2, name: "New blade", slot: "main", itemLevel: 200, equipLevel: 50, jobs: [1]);

        var result = ArsenalTriage.Run([Owned(worn, equipped: true), Owned(spare)], Levels((1, 90)));

        var oldOne = Piece(result, "Old blade");
        Assert.Equal(Verdict.Obsolete, oldOne.Verdict);
        Assert.True(oldOne.Piece.IsEquipped);
        Assert.Contains("New blade", Reason(result, "Old blade"));
    }

    [Fact]
    public void OwningTwoOfTheSameThing_TellsYouWhichOneYouCanActOn()
    {
        var cesti = Facts(id: 1688, name: "Cesti", slot: "main", itemLevel: 19, equipLevel: 19, jobs: [2], desynthesisable: true);

        var result = ArsenalTriage.Run(
            [Owned(cesti, equipped: true), Owned(cesti, position: 7)], Levels((2, 50)));

        var found = result.PiecesOf(1688);
        Assert.Equal(2, found.Count);
        Assert.Single(found, p => p.Piece.IsEquipped);
        Assert.Single(found, p => !p.Piece.IsEquipped);
    }

    [Fact]
    public void AHoarderAtEndGame_GetsAShortListOfWhatCanActuallyGo()
    {
        // Forty pieces in one slot, one of them best. The point of the plugin is that the answer is
        // short.
        var pieces = Enumerable.Range(1, 40)
            .Select(i => Owned(Facts(id: (uint)i, name: $"Blade {i}", slot: "main",
                itemLevel: (ushort)(i * 10), equipLevel: 1, jobs: [1], desynthesisable: true)))
            .ToList();

        var result = ArsenalTriage.Run(pieces, Levels((1, 100)));

        Assert.Equal(39, result.Count(Verdict.Obsolete));
        Assert.Equal(1, result.Count(Verdict.KeptBest));
        Assert.All(result.Pieces.Where(p => p.Verdict == Verdict.Obsolete),
            p => Assert.Equal(DisposalMethod.Desynthesis, p.Disposal));
        Assert.Contains("Blade 40", Reason(result, "Blade 1"));
    }

    [Fact]
    public void AGlamourCollection_IsSentToTheArmoireRatherThanTheBin()
    {
        var kept = Facts(id: 1, name: "Fancy hat", slot: "head", itemLevel: 100, jobs: [1], armoire: true);
        var better = Facts(id: 2, name: "Practical hat", slot: "head", itemLevel: 200, jobs: [1]);

        var result = ArsenalTriage.Run([Owned(kept), Owned(better)], Levels((1, 90)));

        Assert.Equal(DisposalMethod.Armoire, Piece(result, "Fancy hat").Disposal);
        Assert.Equal("Store in the Armoire", Presentation.DisposalLabel(Piece(result, "Fancy hat").Disposal));
    }

    [Fact]
    public void ARelicOwner_IsNeverToldToBinIt_ButIsToldWhatBeatsIt()
    {
        var relic = Facts(id: 1, name: "Manderville Greatsword", slot: "main", itemLevel: 645, jobs: [1], relic: true);
        var better = Facts(id: 2, name: "Malefic", slot: "main", itemLevel: 690, jobs: [1]);

        var result = ArsenalTriage.Run([Owned(relic), Owned(better)], Levels((1, 100)));

        var row = Piece(result, "Manderville Greatsword");
        Assert.Equal(Verdict.NeedsReview, row.Verdict);
        Assert.Equal(DisposalMethod.None, row.Disposal);
        Assert.Contains("Malefic", Reason(result, "Manderville Greatsword"));
        Assert.Contains("Relic", Reason(result, "Manderville Greatsword"));
    }

    [Fact]
    public void SomeoneWhoOnlyPlaysOneClass_IsNotToldToKeepGearForClassesTheyIgnore()
    {
        // The gear is usable by a class at level 0. That class is not a reason to keep a piece the
        // played class has already outgrown — the merge only keeps it if a class still needs it,
        // and an untouched class at effective level 1 needs nothing above level 1.
        var old = Facts(id: 1, name: "Old bow", slot: "main", itemLevel: 30, equipLevel: 15, jobs: [5, 23], desynthesisable: true);
        var current = Facts(id: 2, name: "Good bow", slot: "main", itemLevel: 400, equipLevel: 80, jobs: [5, 23]);

        var result = ArsenalTriage.Run([Owned(old), Owned(current)], Levels((5, 90), (23, 90)));

        Assert.Equal(Verdict.Obsolete, Piece(result, "Old bow").Verdict);
    }

    [Fact]
    public void AnEmptyArmoury_SaysNothingRatherThanBreaking()
    {
        var result = ArsenalTriage.Run([], Levels((1, 90)));

        Assert.Empty(result.Pieces);
        Assert.Empty(result.Actionable);
        Assert.Empty(result.SoulCrystals);
        Assert.Equal(0, result.Count(DisposalMethod.Discard));
    }

    [Fact]
    public void SoulCrystals_AreNeverPartOfTheAnswer()
    {
        var crystal = Facts(id: 1, name: "Soul of the Monk", slot: "soul_crystal", jobs: [20]);
        var weapon = Facts(id: 2, name: "Fists", slot: "main", jobs: [2]);

        var result = ArsenalTriage.Run([Owned(crystal), Owned(weapon)], Levels((2, 50), (20, 50)));

        Assert.Single(result.SoulCrystals);
        Assert.DoesNotContain(result.Pieces, p => p.Facts.Slot == "soul_crystal");
        Assert.Empty(result.PiecesOf(1));
    }

    [Fact]
    public void EveryPieceGetsAVerdictAndAReadableReason_WhateverTheSituation()
    {
        // The contract the whole window rests on: nothing is ever left unexplained.
        var pieces = new[]
        {
            Owned(Facts(id: 1, name: "A", slot: "main", itemLevel: 10, jobs: [1])),
            Owned(Facts(id: 2, name: "B", slot: "main", itemLevel: 20, jobs: [1])),
            Owned(Facts(id: 3, name: "C", slot: "head", itemLevel: 500, equipLevel: 90, jobs: [1])),
            Owned(Facts(id: 4, name: "D", slot: "ring", jobs: []), equipped: true),
            Unresolved(itemId: 777, slot: "body"),
            Owned(Facts(id: 5, name: "E", slot: "feet", jobs: [38])),
        };

        var result = ArsenalTriage.Run(pieces, Levels((1, 50)));

        Assert.All(result.Pieces, piece =>
        {
            Assert.NotEmpty(Presentation.VerdictLabel(piece.Verdict));
            Assert.NotEmpty(Presentation.Reason(piece, Jobs));
            Assert.NotEmpty(Presentation.SlotLabel(piece.Facts.Slot));
        });
    }
}
