using ArsenalCleanup.Triage;
using Xunit;
using static ArsenalCleanup.Tests.TriageFixtures;

namespace ArsenalCleanup.Tests;

public sealed class HoveredItemIdTests
{
    [Fact]
    public void ReadsAnOrdinaryItemAsItself()
    {
        Assert.Equal(1688u, HoveredItemId.BaseItemId(1688));
    }

    [Fact]
    public void StripsTheHighQualityMarker()
    {
        // The game marks a hovered HQ item by adding a million to its id. Left in place, hovering the
        // HQ version of a piece you own looks like an item nobody has ever seen, and the panel
        // silently says nothing — which reads as the plugin being broken.
        Assert.Equal(1688u, HoveredItemId.BaseItemId(1688 + HoveredItemId.HighQualityOffset));
    }

    [Fact]
    public void ReadsNothingHoveredAsNothing()
    {
        Assert.Equal(0u, HoveredItemId.BaseItemId(0));
    }

    [Fact]
    public void LeavesAnIdSittingExactlyOnTheMarkerAlone()
    {
        // A real id of exactly one million is not an HQ marker with a zero id.
        Assert.Equal(1_000_000u, HoveredItemId.BaseItemId(1_000_000));
    }

    [Fact]
    public void RefusesAValueTooLargeToBeAnItem()
    {
        Assert.Equal(0u, HoveredItemId.BaseItemId(ulong.MaxValue));
    }
}

public sealed class TriageResultLookupTests
{
    [Fact]
    public void FindsThePieceUnderTheCursor()
    {
        var cesti = Facts(id: 1688, name: "Cesti", itemLevel: 19);
        var result = ArsenalTriage.Run([Owned(cesti)], Levels((Pugilist, 50)));

        Assert.Equal("Cesti", Assert.Single(result.PiecesOf(1688)).Facts.Name);
    }

    [Fact]
    public void FindsBothCopies_WhenOneIsWornAndOneIsSpare()
    {
        // The two have different answers — only the spare can be dealt with without unequipping —
        // so the panel has to be able to show both.
        var cesti = Facts(id: 1688, name: "Cesti", itemLevel: 19);
        var result = ArsenalTriage.Run(
            [Owned(cesti, equipped: true), Owned(cesti, position: 4)], Levels((Pugilist, 50)));

        var found = result.PiecesOf(1688);
        Assert.Equal(2, found.Count);
        Assert.Single(found, p => p.Piece.IsEquipped);
    }

    [Fact]
    public void FindsNothingForAnItemThePlayerDoesNotOwn()
    {
        var result = ArsenalTriage.Run([Owned(Facts(id: 1688))], Levels((Pugilist, 50)));

        Assert.Empty(result.PiecesOf(9999));
    }

    [Fact]
    public void FindsNothingWhenNothingIsHovered()
    {
        var result = ArsenalTriage.Run([Owned(Facts(id: 1688))], Levels((Pugilist, 50)));

        Assert.Empty(result.PiecesOf(0));
    }

    [Fact]
    public void FindsNothingInAnEmptyResult()
    {
        Assert.Empty(TriageResult.Empty.PiecesOf(1688));
    }

    [Fact]
    public void DoesNotFindASoulCrystal_WhichIsNeverTriaged()
    {
        var crystal = Facts(id: 4542, name: "Soul of the Monk", slot: "soul_crystal");
        var result = ArsenalTriage.Run([Owned(crystal)], Levels((Pugilist, 50)));

        Assert.Empty(result.PiecesOf(4542));
    }
}
