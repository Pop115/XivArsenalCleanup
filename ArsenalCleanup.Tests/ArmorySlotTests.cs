using ArsenalCleanup;
using Dalamud.Game.Inventory;
using Xunit;

namespace ArsenalCleanup.Tests;

public sealed class ArmorySlotTests
{
    [Theory]
    [InlineData(GameInventoryType.ArmoryMainHand, "main")]
    [InlineData(GameInventoryType.ArmoryOffHand, "offhand")]
    [InlineData(GameInventoryType.ArmoryHead, "head")]
    [InlineData(GameInventoryType.ArmoryBody, "body")]
    [InlineData(GameInventoryType.ArmoryHands, "hand")]
    [InlineData(GameInventoryType.ArmoryLegs, "legs")]
    [InlineData(GameInventoryType.ArmoryFeets, "feet")]
    [InlineData(GameInventoryType.ArmoryEar, "ear")]
    [InlineData(GameInventoryType.ArmoryNeck, "neck")]
    [InlineData(GameInventoryType.ArmoryWrist, "wrist")]
    [InlineData(GameInventoryType.ArmoryRings, "ring")]
    [InlineData(GameInventoryType.ArmorySoulCrystal, "soul_crystal")]
    public void Resolve_MapsEachArmoryType_ToTheMatchingCatalogSlotName(GameInventoryType type, string expected)
    {
        Assert.Equal(expected, ArmorySlot.Resolve(type));
    }

    [Fact]
    public void Resolve_ArmoryWaist_ReturnsNull()
    {
        // Belts were removed from the game — the enum value still exists but nothing can ever
        // occupy it, so it has no canonical arsenal slot (mirrors EquipSlotResolver on the catalog side).
        Assert.Null(ArmorySlot.Resolve(GameInventoryType.ArmoryWaist));
    }

    [Fact]
    public void Resolve_NonArmoryType_ReturnsNull()
    {
        Assert.Null(ArmorySlot.Resolve(GameInventoryType.Inventory1));
    }

    [Fact]
    public void ArmoryTypes_HasExactlyTwelveEntries_MatchingTheCatalogSlotCount()
    {
        Assert.Equal(12, ArmorySlot.ArmoryTypes.Count);
    }

    [Fact]
    public void ArmoryTypes_EveryEntry_ResolvesToANonNullSlot()
    {
        foreach (var type in ArmorySlot.ArmoryTypes)
        {
            Assert.NotNull(ArmorySlot.Resolve(type));
        }
    }

    [Theory]
    [InlineData(0, "main")]
    [InlineData(1, "offhand")]
    [InlineData(2, "head")]
    [InlineData(3, "body")]
    [InlineData(4, "hand")]
    [InlineData(6, "legs")]
    [InlineData(7, "feet")]
    [InlineData(8, "ear")]
    [InlineData(9, "neck")]
    [InlineData(10, "wrist")]
    [InlineData(11, "ring")]
    [InlineData(12, "ring")]
    [InlineData(13, "soul_crystal")]
    public void ResolveEquipped_MapsEachEquippedIndex_ToTheMatchingCatalogSlotName(int index, string expected)
    {
        // Order is the game's own (RaptureGearsetModule.GearsetItemIndex) — the equipped container
        // is one flat array, so the index is the only thing saying which slot a piece sits in.
        Assert.Equal(expected, ArmorySlot.ResolveEquipped(index));
    }

    [Fact]
    public void ResolveEquipped_BeltIndex_ReturnsNull()
    {
        // Index 5 is the removed belt slot — same reasoning as ArmoryWaist above.
        Assert.Null(ArmorySlot.ResolveEquipped(5));
    }

    [Fact]
    public void ResolveEquipped_IndexOutsideTheContainer_ReturnsNull()
    {
        Assert.Null(ArmorySlot.ResolveEquipped(14));
        Assert.Null(ArmorySlot.ResolveEquipped(-1));
    }

    [Fact]
    public void ResolveEquipped_CoversEveryCatalogSlot_ThatAnArmoryTabCovers()
    {
        // A gap here would silently drop a whole slot's worn piece from the triage — exactly the
        // class of bug this pass exists to fix (2026-09-06: worn gear missing from the export).
        var armorySlots = new HashSet<string?>();
        foreach (var type in ArmorySlot.ArmoryTypes) armorySlots.Add(ArmorySlot.Resolve(type));

        var equippedSlots = new HashSet<string?>();
        for (var index = 0; index <= 13; index++)
        {
            var slot = ArmorySlot.ResolveEquipped(index);
            if (slot is not null) equippedSlots.Add(slot);
        }

        Assert.Equal(armorySlots, equippedSlots);
    }
}
