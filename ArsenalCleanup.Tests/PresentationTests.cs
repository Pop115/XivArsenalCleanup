using System;
using System.Linq;
using ArsenalCleanup.Triage;
using ArsenalCleanup.Windows;
using Xunit;

namespace ArsenalCleanup.Tests;

public sealed class PresentationTests
{
    [Fact]
    public void GivesEveryVerdictWordingOfItsOwn()
    {
        // Nothing on screen should ever read as an enum name, so every value has to be covered here
        // rather than falling through to the humanised last resort.
        foreach (var verdict in Enum.GetValues<Verdict>())
        {
            var label = Presentation.VerdictLabel(verdict);
            Assert.NotEmpty(label);
            Assert.NotEqual(verdict.ToString(), label);
        }
    }

    [Fact]
    public void GivesEveryVerdictADistinctColour()
    {
        // Two verdicts sharing a colour would make the summary unreadable at a glance.
        var colours = Enum.GetValues<Verdict>().Select(Presentation.VerdictColour).Distinct().ToList();

        Assert.Equal(Enum.GetValues<Verdict>().Length, colours.Count);
    }

    [Fact]
    public void GivesEveryDisposalMethodWordingExceptNone()
    {
        foreach (var disposal in Enum.GetValues<DisposalMethod>())
        {
            var label = Presentation.DisposalLabel(disposal);
            if (disposal == DisposalMethod.None) Assert.Empty(label);
            else Assert.NotEmpty(label);
        }
    }

    [Fact]
    public void NamesEverySlotItOrders()
    {
        foreach (var slot in Presentation.SlotOrder)
        {
            var label = Presentation.SlotLabel(slot);
            Assert.NotEmpty(label);
            Assert.NotEqual(slot, label);
        }
    }

    [Fact]
    public void OrdersSlotsTheWayTheGameDoes_StartingWithWeapons()
    {
        Assert.Equal("main", Presentation.SlotOrder[0]);
        Assert.Equal("offhand", Presentation.SlotOrder[1]);
        Assert.Equal(11, Presentation.SlotOrder.Length);
    }

    [Fact]
    public void LeavesSoulCrystalsOutOfTheSlotTabs()
    {
        // They are reported separately and never triaged, so a tab for them would be an empty
        // promise.
        Assert.DoesNotContain("soul_crystal", Presentation.SlotOrder);
        Assert.NotEmpty(Presentation.SlotLabel("soul_crystal"));
    }

    [Theory]
    [InlineData("soul_crystal", "Soul crystal")]
    [InlineData("some_new_slot", "Some new slot")]
    [InlineData("x", "X")]
    [InlineData("", "")]
    public void FallsBackToReadableWording_ForAValueItHasNoLabelFor(string raw, string expected)
    {
        // A slot the game adds later should read as words, never as its identifier.
        Assert.Equal(expected, Presentation.Humanise(raw));
    }

    [Fact]
    public void StillSaysSomethingReadable_ForAVerdictItHasNeverSeen()
    {
        // Guards the fallback rather than the wording: if a verdict is ever added and someone
        // forgets to give it words here, the screen must degrade to readable text, not to a crash
        // or to an identifier.
        var invented = (Verdict)99;

        Assert.NotEmpty(Presentation.VerdictLabel(invented));
        Assert.Equal(new System.Numerics.Vector4(1f, 1f, 1f, 1f), Presentation.VerdictColour(invented));
    }

    [Fact]
    public void SaysNothingAtAll_ForADisposalMethodItHasNeverSeen()
    {
        Assert.Empty(Presentation.DisposalLabel((DisposalMethod)99));
    }

    [Fact]
    public void NeverShowsAnUnknownSlotAsItsRawKey()
    {
        Assert.Equal("Brand new slot", Presentation.SlotLabel("brand_new_slot"));
    }
}
