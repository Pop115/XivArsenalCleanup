using System.Collections.Generic;
using System.Linq;
using ArsenalCleanup;
using Dalamud.Game.Inventory;
using Xunit;

namespace ArsenalCleanup.Tests;

/// <summary>
/// An <see cref="IInventorySource"/> backed by a dictionary — the whole reason that interface
/// exists. Containers not listed read as empty, which is what the game reports for a character who
/// owns nothing in that slot.
/// </summary>
internal sealed class FakeInventory : IInventorySource
{
    private readonly Dictionary<GameInventoryType, List<RawArmoryPiece>> byContainer = new();

    public FakeInventory With(GameInventoryType type, params RawArmoryPiece[] pieces)
    {
        byContainer[type] = [.. pieces];
        return this;
    }

    public IEnumerable<RawArmoryPiece> Read(GameInventoryType type) =>
        byContainer.TryGetValue(type, out var pieces) ? pieces : [];
}

public sealed class ArmoryReaderTests
{
    private static RawArmoryPiece Item(uint id, int slot = 0) => new(id, 1, false, false, slot);

    [Fact]
    public void CollectPieces_ReadsEveryArmouryContainer_TaggingEachPieceWithItsSlot()
    {
        var inventory = new FakeInventory()
            .With(GameInventoryType.ArmoryMainHand, Item(31472))
            .With(GameInventoryType.ArmoryRings, Item(4179, slot: 0), Item(4180, slot: 1));

        var pieces = ArmoryReader.CollectPieces(inventory);

        Assert.Equal(3, pieces.Count);
        Assert.Equal("main", Assert.Single(pieces, p => p.Piece.BaseItemId == 31472).Slot);
        Assert.All(pieces.Where(p => p.Piece.BaseItemId != 31472), p => Assert.Equal("ring", p.Slot));
        Assert.All(pieces, p => Assert.False(p.IsEquipped));
    }

    [Fact]
    public void CollectPieces_AlsoReadsWornGear_WhichNoArmouryContainerEverHolds()
    {
        // Gear on the active job is the piece most likely to be the best one owned; an export
        // without it lets a consumer crown a strictly inferior chest piece "best available".
        var inventory = new FakeInventory()
            .With(GameInventoryType.ArmoryMainHand, Item(31421))
            .With(GameInventoryType.EquippedItems, Item(1688, slot: 0));

        var pieces = ArmoryReader.CollectPieces(inventory);

        var worn = Assert.Single(pieces, p => p.IsEquipped);
        Assert.Equal(1688u, worn.Piece.BaseItemId);
        Assert.Equal("main", worn.Slot);
        Assert.False(Assert.Single(pieces, p => p.Piece.BaseItemId == 31421).IsEquipped);
    }

    [Fact]
    public void CollectPieces_MapsEveryEquippedIndex_ToItsOwnSlot()
    {
        var inventory = new FakeInventory().With(
            GameInventoryType.EquippedItems,
            Item(1, slot: 0), Item(2, slot: 2), Item(3, slot: 11), Item(4, slot: 12), Item(5, slot: 13));

        var pieces = ArmoryReader.CollectPieces(inventory);

        Assert.Equal(["main", "head", "ring", "ring", "soul_crystal"], pieces.Select(p => p.Slot));
    }

    [Fact]
    public void CollectPieces_SkipsAnEquippedIndexThatMapsToNoSlot()
    {
        // Index 5 is the belt slot, removed from the game years ago. Exporting it under a guessed
        // slot name would put a phantom piece in front of whoever reads the file.
        var inventory = new FakeInventory().With(GameInventoryType.EquippedItems, Item(9, slot: 5));

        Assert.Empty(ArmoryReader.CollectPieces(inventory));
    }

    [Fact]
    public void CollectPieces_EmptyInventory_ProducesNothing()
    {
        Assert.Empty(ArmoryReader.CollectPieces(new FakeInventory()));
    }

    [Fact]
    public void CollectPieces_KeepsWornAndStoredCopiesOfTheSameItem_AsSeparateEntries()
    {
        // Two physical objects: one on the character, one spare in the chest. Only the spare can be
        // acted on without unequipping, so collapsing them would lose a real distinction.
        var inventory = new FakeInventory()
            .With(GameInventoryType.ArmoryMainHand, Item(1688, slot: 3))
            .With(GameInventoryType.EquippedItems, Item(1688, slot: 0));

        var pieces = ArmoryReader.CollectPieces(inventory);

        Assert.Equal(2, pieces.Count);
        Assert.Single(pieces, p => p.IsEquipped);
        Assert.Single(pieces, p => !p.IsEquipped);
    }
}
