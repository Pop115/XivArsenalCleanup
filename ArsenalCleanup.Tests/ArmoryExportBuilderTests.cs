using System;
using System.Collections.Generic;
using ArsenalCleanup;
using Xunit;

namespace ArsenalCleanup.Tests;

public sealed class ArmoryExportBuilderTests
{
    [Fact]
    public void Build_FlattensPiecesAcrossSlots_TaggingEachWithItsSlot()
    {
        List<SlottedArmoryPiece> pieces =
        [
            new("main", IsEquipped: false, new RawArmoryPiece(31472, 1, false, false, 0)),
            new("ring", IsEquipped: false, new RawArmoryPiece(4179, 1, true, false, 0)),
            new("ring", IsEquipped: false, new RawArmoryPiece(4180, 1, false, true, 1)),
        ];
        var exportedAt = new DateTimeOffset(2026, 8, 25, 18, 0, 0, TimeSpan.Zero);

        var result = ArmoryExportBuilder.Build("Hero Example", exportedAt, pieces);

        Assert.Equal("Hero Example", result.Character);
        Assert.Equal(exportedAt, result.ExportedAt);
        Assert.Equal(3, result.Pieces.Length);

        var main = Assert.Single(result.Pieces, p => p.Slot == "main");
        Assert.Equal(31472u, main.ItemId);
        Assert.False(main.IsHq);
        Assert.Equal(0, main.Position);

        var hqRing = Assert.Single(result.Pieces, p => p.ItemId == 4179);
        Assert.Equal("ring", hqRing.Slot);
        Assert.True(hqRing.IsHq);
        Assert.Equal(0, hqRing.Position);

        var collectableRing = Assert.Single(result.Pieces, p => p.ItemId == 4180);
        Assert.True(collectableRing.IsCollectable);
        Assert.Equal(1, collectableRing.Position);
    }

    [Fact]
    public void Build_NoPieces_ProducesAnEmptyExport()
    {
        var result = ArmoryExportBuilder.Build("Hero Example", DateTimeOffset.UtcNow, []);

        Assert.Empty(result.Pieces);
    }

    [Fact]
    public void Build_StampsTheCurrentFormatVersion_SoConsumersCanRejectWhatTheyCannotRead()
    {
        var result = ArmoryExportBuilder.Build("Hero Example", DateTimeOffset.UtcNow, []);

        Assert.Equal(ArmoryExportBuilder.CurrentFormatVersion, result.FormatVersion);
    }

    /// <summary>
    /// The worn piece and the chest piece share a slot name and even a position index — they come
    /// from two different containers — so the equipped flag is the only thing telling them apart
    /// downstream.
    /// </summary>
    [Fact]
    public void Build_MarksWornPieces_SoTheyAreDistinguishableFromChestPiecesInTheSameSlot()
    {
        List<SlottedArmoryPiece> pieces =
        [
            new("main", IsEquipped: true, new RawArmoryPiece(1688, 1, false, false, 0)),
            new("main", IsEquipped: false, new RawArmoryPiece(31421, 1, false, false, 0)),
        ];

        var result = ArmoryExportBuilder.Build("Hero Example", DateTimeOffset.UtcNow, pieces);

        Assert.True(Assert.Single(result.Pieces, p => p.ItemId == 1688).IsEquipped);
        Assert.False(Assert.Single(result.Pieces, p => p.ItemId == 31421).IsEquipped);
    }
}
