using System.Collections.Generic;
using ArsenalCleanup.Triage;

namespace ArsenalCleanup.Tests;

/// <summary>
/// Builders for the triage's inputs, so each test states only what it is actually about.
///
/// Defaults are deliberately unremarkable — an ordinary level-1 item usable by one job — so any
/// value a test does spell out is the value under test.
/// </summary>
internal static class TriageFixtures
{
    internal const uint Pugilist = 2;
    internal const uint Monk = 20;
    internal const uint Archer = 5;
    internal const uint Bard = 23;

    internal static GearFacts Facts(
        uint id = 1,
        string name = "Test piece",
        string slot = "main",
        ushort itemLevel = 1,
        byte equipLevel = 1,
        IReadOnlyList<uint>? jobs = null,
        bool relic = false,
        bool desynthesisable = false,
        bool armoire = false) =>
        new(id, name, slot, itemLevel, equipLevel, IconId: 1, jobs ?? [Pugilist], relic, desynthesisable, armoire);

    internal static OwnedPiece Owned(GearFacts facts, bool equipped = false, int position = 0) =>
        new(facts.ItemId, facts.Slot, position, equipped, facts);

    /// <summary>A piece whose item id resolved to nothing in the game's sheets.</summary>
    internal static OwnedPiece Unresolved(uint itemId = 9999, string slot = "main") =>
        new(itemId, slot, 0, false, null);

    internal static Dictionary<uint, int> Levels(params (uint Job, int Level)[] levels)
    {
        var map = new Dictionary<uint, int>();
        foreach (var (job, level) in levels) map[job] = level;
        return map;
    }
}
