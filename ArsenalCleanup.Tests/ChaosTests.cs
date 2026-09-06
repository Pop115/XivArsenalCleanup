using System;
using System.Collections.Generic;
using System.Linq;
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

    private static readonly string[] Slots =
        ["main", "offhand", "head", "body", "hand", "legs", "feet", "ear", "neck", "wrist", "ring", "soul_crystal"];

    private static readonly JobDirectory Jobs = new([.. Enumerable.Range(1, 12)
        .Select(i => new JobInfo((uint)i, $"J{i}", $"C{i}", (uint)(i > 6 ? i - 6 : i)))]);

    /// <summary>
    /// A catalogue of items, drawn once per seed.
    ///
    /// An item id determines an item's facts, exactly as it does in the game — a generator that
    /// gives the same id two different item levels is not hostile, it is impossible, and chasing the
    /// contradictions it creates says nothing about the plugin.
    /// </summary>
    private static List<GearFacts> Catalogue(Random random) =>
        [.. Enumerable.Range(1, 25).Select(id => new GearFacts(
            ItemId: (uint)id,
            Name: $"Item {id}",
            Slot: Slots[random.Next(Slots.Length)],
            ItemLevel: (ushort)random.Next(0, 800),          // 0 is possible: some rows really are
            EquipLevel: (byte)random.Next(0, 120),           // above the cap on purpose
            IconId: (ushort)random.Next(0, 60000),
            JobIds: [.. Enumerable.Range(0, random.Next(0, 5))
                .Select(_ => (uint)random.Next(1, 14))       // 13 is outside the directory, on purpose
                .Distinct()],
            IsRelic: random.Next(12) == 0,
            IsDesynthesisable: random.Next(2) == 0,
            IsArmoireEligible: random.Next(4) == 0))];

    /// <summary>
    /// One deliberately hostile armoury: duplicates, worn copies, items the game has no row for, and
    /// a character whose classes range from untouched to capped — including classes the game never
    /// reported a level for at all.
    /// </summary>
    private static (List<OwnedPiece> Pieces, Dictionary<uint, int> Levels) Armoury(Random random)
    {
        var catalogue = Catalogue(random);
        var pieceCount = random.Next(0, 60);
        var pieces = new List<OwnedPiece>(pieceCount);

        // Position is a container slot index, so it is unique within a container — as in the game.
        var nextPosition = new Dictionary<(string Slot, bool Worn), int>();

        for (var i = 0; i < pieceCount; i++)
        {
            var facts = random.Next(10) == 0 ? null : catalogue[random.Next(catalogue.Count)];
            var slot = facts?.Slot ?? Slots[random.Next(Slots.Length)];
            var worn = random.Next(6) == 0;

            var key = (slot, worn);
            nextPosition.TryGetValue(key, out var position);
            nextPosition[key] = position + 1;

            pieces.Add(new OwnedPiece(
                facts?.ItemId ?? (uint)random.Next(100, 200),
                slot,
                position,
                worn,
                facts));
        }

        var levels = new Dictionary<uint, int>();
        foreach (var job in Jobs.Jobs)
        {
            // Sometimes absent entirely, which is a different case from level 0.
            if (random.Next(8) == 0) continue;
            levels[job.Id] = random.Next(0, 101);
        }

        return (pieces, levels);
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
                Assert.True(
                    piece.ReplacedBy!.ItemLevel > piece.Facts.ItemLevel,
                    $"seed {seed}: {piece.Facts.Name} 'replaced' by something no better");
                Assert.Equal(piece.Facts.Slot, piece.ReplacedBy.Slot);
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
        var same = new GearFacts(1688, "Cesti", "main", 19, 19, 1, [1], false, true, false);
        var pieces = Enumerable.Range(0, 200)
            .Select(i => new OwnedPiece(1688, "main", i, i == 0, same))
            .ToList();

        var result = ArsenalTriage.Run(pieces, new Dictionary<uint, int> { [1] = 90 });

        Assert.Equal(200, result.Pieces.Count);
        // Identical pieces cannot supersede each other, so none of them is replaced by another.
        Assert.Equal(0, result.Count(Verdict.Obsolete));
    }

    [Fact]
    public void HandlesAnItemNoClassCanUse()
    {
        var orphan = new GearFacts(1, "Orphan", "main", 100, 50, 1, [], false, false, false);

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
        var a = new GearFacts(1, "A", "main", 100, 50, 1, [1, 2], false, false, false);
        var b = new GearFacts(2, "B", "main", 200, 50, 1, [1, 2], false, false, false);

        var result = ArsenalTriage.Run(
            [new OwnedPiece(1, "main", 0, false, a), new OwnedPiece(2, "main", 1, false, b)],
            new Dictionary<uint, int>());

        // Nothing is concluded from a level nobody could read, and nothing is suggested for disposal.
        Assert.All(result.Pieces, p => Assert.Equal(Verdict.NeedsReview, p.Verdict));
        Assert.All(result.Pieces, p => Assert.Equal(DisposalMethod.None, p.Disposal));
    }
}
