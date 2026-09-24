using System;
using System.Collections.Generic;
using System.Linq;
using ArsenalCleanup.Tests.TestData;
using ArsenalCleanup.Triage;
using ArsenalCleanup.Windows;
using Xunit;

namespace ArsenalCleanup.Tests;

/// <summary>
/// Thousands of randomly generated armouries, checked against properties that must hold for any of
/// them.
///
/// The hand-written tests all describe situations somebody thought of. These exist for the ones
/// nobody did: absurd levels, items usable by no class, forty copies of the same ring, an item level
/// of zero, a character whose classes are all level 0. A real inventory is player-made and will
/// eventually contain something nobody predicted, and the plugin advises people to throw away gear —
/// crashing, or quietly recommending the destruction of the only weapon someone can hold, is not an
/// acceptable failure.
///
/// Seeds are fixed so a failure is reproducible: the seed is printed with the assertion.
/// </summary>
public sealed class ChaosTests
{
    private const int Runs = 400;

    /// <summary>
    /// The real classes, with the two abbreviations and the level each begins at — Beastmaster
    /// included, which is what puts the defence rule under the generator as well.
    /// </summary>
    private static readonly JobDirectory Jobs = RealGameData.Jobs;

    /// <summary>Real gear, sampled from the game's own sheets — see <see cref="RealGameData"/>.</summary>
    private static readonly IReadOnlyList<GearFacts> Catalogue = RealGameData.Items;

    private static readonly string[] Slots =
        ["main", "offhand", "head", "body", "hand", "legs", "feet", "ear", "neck", "wrist", "ring", "soul_crystal"];

    /// <summary>
    /// One deliberately hostile armoury, built from gear that actually exists.
    ///
    /// Real items bring the awkwardness invented ones never do: item levels that do not track equip
    /// levels, level-1 pieces flagged for all forty-three classes, jobs sharing a category with their
    /// base class. On top of that sit the cases the game itself would not produce but a client can —
    /// an id the sheets have no row for, a piece belonging to no class — because those are what a
    /// mod, a stale patch or a bad read look like.
    /// </summary>
    private static (List<OwnedPiece> Pieces, Dictionary<uint, int> Levels) Armoury(Random random)
    {
        var pieceCount = random.Next(0, 60);
        var pieces = new List<OwnedPiece>(pieceCount);

        // Position is a container slot index, so it is unique within a container — as in the game.
        var nextPosition = new Dictionary<(string Slot, bool Worn), int>();

        for (var i = 0; i < pieceCount; i++)
        {
            // One piece in ten is something the game data cannot explain.
            var facts = random.Next(10) == 0 ? null : Catalogue[random.Next(Catalogue.Count)];
            var slot = facts?.Slot ?? Slots[random.Next(Slots.Length)];
            var worn = random.Next(6) == 0;

            var key = (slot, worn);
            nextPosition.TryGetValue(key, out var position);
            nextPosition[key] = position + 1;

            pieces.Add(new OwnedPiece(
                facts?.ItemId ?? (uint)random.Next(900_000, 950_000),
                slot,
                position,
                worn,
                facts));
        }

        // A character anywhere between untouched and capped, with some classes the game never
        // reported at all — then reconciled the way the plugin does it, lineages and starting
        // levels included.
        var raw = new Dictionary<uint, int>();
        foreach (var job in Jobs.Jobs)
        {
            if (random.Next(8) == 0) continue;
            raw[job.Id] = random.Next(0, 101);
        }

        return (pieces, JobLevelTable.WithLineageFilledIn(raw, Jobs.Lineages()));
    }

    [Fact]
    public void NeverThrows_AndAlwaysExplainsItself()
    {
        for (var seed = 0; seed < Runs; seed++)
        {
            var random = new Random(seed);
            var (pieces, levels) = Armoury(random);

            TriageResult result;
            try
            {
                result = ArsenalTriage.Run(pieces, levels);
            }
            catch (Exception ex)
            {
                Assert.Fail($"seed {seed}: triage threw {ex.GetType().Name}: {ex.Message}");
                return;
            }

            foreach (var piece in result.Pieces)
            {
                Assert.False(string.IsNullOrWhiteSpace(Presentation.VerdictLabel(piece.Verdict)), $"seed {seed}");
                Assert.False(string.IsNullOrWhiteSpace(Presentation.Reason(piece, Jobs)), $"seed {seed}");
                Assert.False(string.IsNullOrWhiteSpace(Presentation.SlotLabel(piece.Facts.Slot)), $"seed {seed}");
            }
        }
    }

    [Fact]
    public void NeverSuggestsPartingWithARelic()
    {
        // The one promise the plugin makes unconditionally. A relic is often months of work.
        for (var seed = 0; seed < Runs; seed++)
        {
            var (pieces, levels) = Armoury(new Random(seed));
            var result = ArsenalTriage.Run(pieces, levels);

            foreach (var piece in result.Pieces.Where(p => p.Facts.IsRelic))
            {
                Assert.True(piece.Disposal == DisposalMethod.None, $"seed {seed}: a relic was given a disposal method");
                Assert.NotEqual(Verdict.Obsolete, piece.Verdict);
            }
        }
    }

    [Fact]
    public void NeverCallsAPieceReplaced_WithoutSomethingBetterToReplaceIt()
    {
        // The failure that would actually cost someone their gear: being told to bin the only thing
        // they can wear.
        for (var seed = 0; seed < Runs; seed++)
        {
            var (pieces, levels) = Armoury(new Random(seed));
            var result = ArsenalTriage.Run(pieces, levels);

            foreach (var piece in result.Pieces.Where(p => p.Verdict == Verdict.Obsolete))
            {
                Assert.True(piece.ReplacedBy is not null, $"seed {seed}: {piece.Facts.Name} replaced by nothing");

                // "Better" by the same yardstick the verdict was reached with, which for a
                // Beastmaster is not the item level — asserting on item level here would have the
                // property test disagree with the rule it is meant to be guarding.
                Assert.True(
                    piece.Ranking.Beats(piece.ReplacedBy!, piece.Facts),
                    $"seed {seed}: {piece.Facts.Name} 'replaced' by something no better");
                Assert.Equal(piece.Facts.Slot, piece.ReplacedBy.Slot);
            }
        }
    }

    [Fact]
    public void NeverLeavesACharacterAHandShort()
    {
        // Two rings are worn at once, so a ring is only spare once two better ones can be. This is
        // the invariant behind that rule, checked against whatever the generator throws up:
        // duplicates, level-90 rings on a level-3 class, forty copies of the same band.
        for (var seed = 0; seed < Runs; seed++)
        {
            var (pieces, levels) = Armoury(new Random(seed));
            var result = ArsenalTriage.Run(pieces, levels);

            foreach (var piece in result.Pieces.Where(p => p.Verdict == Verdict.Obsolete && p.Facts.Slot == "ring"))
            {
                var job = piece.DecidingJob!.Value;
                var level = piece.DecidingJobLevel ?? 0;

                // Everything that beats it and is either wearable today or unlocks no later — a
                // superset of both routes to "replaced", so two of them is the weakest true claim.
                var ahead = pieces.Count(other =>
                    other.Facts is { Slot: "ring" } facts
                    && facts.JobIds.Contains(job)
                    && piece.Ranking.Beats(facts, piece.Facts)
                    && (facts.EquipLevel <= level || facts.EquipLevel <= piece.Facts.EquipLevel));

                Assert.True(
                    ahead >= SlotCapacity.For("ring"),
                    $"seed {seed}: {piece.Facts.Name} called replaced with only {ahead} better ring(s)");
            }
        }
    }

    [Fact]
    public void NeverTellsABeastmasterToGiveUpDefence()
    {
        // The job's own rule, as a property: nothing it is told to part with is sturdier than what
        // replaces it. Where a piece is shared with another class the merge may keep it for that
        // class instead — which can only ever keep more, never less.
        for (var seed = 0; seed < Runs; seed++)
        {
            var (pieces, levels) = Armoury(new Random(seed));
            var result = ArsenalTriage.Run(pieces, levels);

            foreach (var piece in result.Pieces.Where(p =>
                p.Verdict == Verdict.Obsolete && p.DecidingJob == KnownJobs.Beastmaster))
            {
                Assert.True(
                    piece.ReplacedBy!.DefencePhysical >= piece.Facts.DefencePhysical
                    && piece.ReplacedBy.DefenceMagical >= piece.Facts.DefenceMagical,
                    $"seed {seed}: {piece.Facts.Name} traded away defence for {piece.ReplacedBy.Name}");
            }
        }
    }

    [Fact]
    public void AlwaysAttachesADisposalMethod_ToExactlyWhatCanGo()
    {
        for (var seed = 0; seed < Runs; seed++)
        {
            var (pieces, levels) = Armoury(new Random(seed));
            var result = ArsenalTriage.Run(pieces, levels);

            foreach (var piece in result.Pieces)
            {
                if (piece.Verdict == Verdict.Obsolete)
                {
                    Assert.NotEqual(DisposalMethod.None, piece.Disposal);
                }
                else
                {
                    Assert.Equal(DisposalMethod.None, piece.Disposal);
                }
            }
        }
    }

    [Fact]
    public void NeverLosesOrInventsAPiece()
    {
        // Every physical piece owned, soul crystals aside, must come back exactly once. Merging is
        // where a piece could quietly vanish.
        for (var seed = 0; seed < Runs; seed++)
        {
            var (pieces, levels) = Armoury(new Random(seed));
            var result = ArsenalTriage.Run(pieces, levels);

            var expected = pieces.Count(p => p.Facts?.Slot != "soul_crystal");
            var distinctPhysical = pieces
                .Where(p => p.Facts?.Slot != "soul_crystal")
                .Select(p => (p.Facts?.ItemId ?? p.ItemId, p.Facts?.Slot ?? p.Slot, p.IsEquipped, p.Position))
                .Distinct()
                .Count();

            Assert.True(
                result.Pieces.Count <= expected && result.Pieces.Count == distinctPhysical,
                $"seed {seed}: {pieces.Count} owned became {result.Pieces.Count} rows (expected {distinctPhysical})");
        }
    }

    [Fact]
    public void IsDeterministic_WhateverOrderTheInventoryWasReadIn()
    {
        // The game does not promise a stable order, and an answer that changes between two reads of
        // the same armoury would be impossible to trust or to report a bug about.
        for (var seed = 0; seed < Runs; seed++)
        {
            var random = new Random(seed);
            var (pieces, levels) = Armoury(random);

            var forwards = ArsenalTriage.Run(pieces, levels);
            var backwards = ArsenalTriage.Run([.. Enumerable.Reverse(pieces)], levels);

            static List<(string Name, Verdict Verdict, string? Replacement, DisposalMethod Disposal)> Fingerprint(TriageResult r) =>
                [.. r.Pieces
                    .Select(p => (Name: p.Facts.Name, p.Verdict, Replacement: p.ReplacedBy?.Name, p.Disposal))
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .ThenBy(x => x.Replacement, StringComparer.Ordinal)
                    .ThenBy(x => x.Verdict)];

            var a = Fingerprint(forwards);
            var b = Fingerprint(backwards);

            Assert.True(a.SequenceEqual(b), $"seed {seed}: reading the same armoury backwards gave a different answer");
        }
    }

    [Fact]
    public void NeverWaitsOnALevelAlreadyReached()
    {
        // "Keep until level N" must never appear for a class already at or above N — the complaint
        // that started this: level-1 gear told to wait for level 1.
        for (var seed = 0; seed < Runs; seed++)
        {
            var (pieces, levels) = Armoury(new Random(seed));
            var result = ArsenalTriage.Run(pieces, levels);

            foreach (var piece in result.Pieces.Where(p => p.Verdict == Verdict.KeptUntilLevel))
            {
                Assert.True(
                    piece.DecidingJobLevel is null || piece.Facts.EquipLevel > piece.DecidingJobLevel,
                    $"seed {seed}: {piece.Facts.Name} waits for level {piece.Facts.EquipLevel} at level {piece.DecidingJobLevel}");
            }
        }
    }

    [Fact]
    public void HandlesAnArmouryOfNothingButDuplicates()
    {
        // Cesti, the real item: id 1688, item level 19, wearable from 19.
        var same = RealGameData.Item(1688);
        var pieces = Enumerable.Range(0, 200)
            .Select(i => new OwnedPiece(same.ItemId, same.Slot, i, i == 0, same))
            .ToList();

        var levels = Jobs.Jobs.ToDictionary(j => j.Id, _ => 90);
        var result = ArsenalTriage.Run(pieces, levels);

        Assert.Equal(200, result.Pieces.Count);
        // Identical pieces cannot supersede each other, so none of them is replaced by another.
        Assert.Equal(0, result.Count(Verdict.Obsolete));
    }

    [Fact]
    public void HandlesAnItemNoClassCanUse()
    {
        var orphan = new GearFacts(1, "Orphan", "main", 100, 50, 1, [], 0, 0, false, false, false);

        var result = ArsenalTriage.Run([new OwnedPiece(1, "main", 0, false, orphan)], new Dictionary<uint, int>());

        var piece = Assert.Single(result.Pieces);
        Assert.Equal(Verdict.NeedsReview, piece.Verdict);
        Assert.NotEmpty(Presentation.Reason(piece, Jobs));
    }

    [Fact]
    public void HandlesACharacterWithNoLevelsAtAll()
    {
        // Before the player state has loaded, every class reads as absent. Two pieces, so the
        // comparison is actually attempted rather than short-circuited by "the only one you own".
        var a = new GearFacts(1, "A", "main", 100, 50, 1, [1, 2], 0, 0, false, false, false);
        var b = new GearFacts(2, "B", "main", 200, 50, 1, [1, 2], 0, 0, false, false, false);

        var result = ArsenalTriage.Run(
            [new OwnedPiece(1, "main", 0, false, a), new OwnedPiece(2, "main", 1, false, b)],
            new Dictionary<uint, int>());

        // Nothing is concluded from a level nobody could read, and nothing is suggested for disposal.
        Assert.All(result.Pieces, p => Assert.Equal(Verdict.NeedsReview, p.Verdict));
        Assert.All(result.Pieces, p => Assert.Equal(DisposalMethod.None, p.Disposal));
    }
}
