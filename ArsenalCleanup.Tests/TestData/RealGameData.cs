using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArsenalCleanup.Triage;

namespace ArsenalCleanup.Tests.TestData;

/// <summary>
/// Real Final Fantasy XIV gear and classes, sampled from the game's own sheets and committed
/// alongside the tests.
///
/// Generated fixtures rather than a live read, for one reason: CI has no game installed, and a
/// suite that only runs on a machine with FFXIV on it is a suite that stops running. The sample is
/// stratified so it covers every slot, every level band, and the flags the triage has rules about —
/// relics, Armoire eligibility, desynthesis, gear usable by every class — plus the specific items
/// from every bug that has been reported against this plugin.
///
/// What this buys over invented numbers: the item levels, equip levels and class categories are the
/// real, awkward ones. Gear whose item level does not rise with its equip level, jobs that share a
/// category with their base class, level-1 items flagged for all forty-three classes. Nobody writing
/// fixtures by hand produces those.
///
/// See CONTRIBUTING.md for how to regenerate them after a patch.
/// </summary>
internal static class RealGameData
{
    private sealed record ItemEntry(
        uint Id, string Name, string Slot, ushort ItemLevel, byte EquipLevel,
        [property: JsonPropertyName("jobIds")] uint[] JobIds,
        bool IsRelic, bool IsDesynthesisable, bool IsArmoireEligible);

    private sealed record ItemFile(string GameVersion, ItemEntry[] Items);

    private sealed record JobEntry(
        uint Id, string LookupAbbreviation, string DisplayAbbreviation, uint ParentId, int StartingLevel);

    private sealed record JobFile(string GameVersion, JobEntry[] Jobs);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static T Load<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"{fileName} could not be read.");
    }

    private static readonly ItemFile ItemData = Load<ItemFile>("real-items.json");
    private static readonly JobFile JobData = Load<JobFile>("real-jobs.json");

    /// <summary>The game version these fixtures were taken from.</summary>
    public static string GameVersion => ItemData.GameVersion;

    /// <summary>Real gear, as the triage sees it.</summary>
    public static IReadOnlyList<GearFacts> Items { get; } =
        [.. ItemData.Items.Select(i => new GearFacts(
            i.Id, i.Name, i.Slot, i.ItemLevel, i.EquipLevel, IconId: 1,
            i.JobIds, i.IsRelic, i.IsDesynthesisable, i.IsArmoireEligible))];

    /// <summary>
    /// The real forty-three classes, with both abbreviations and the level each actually begins at —
    /// the three facts that three separate bugs turned on.
    /// </summary>
    public static JobDirectory Jobs { get; } =
        new([.. JobData.Jobs.Select(j => new JobInfo(
            j.Id, j.LookupAbbreviation, j.DisplayAbbreviation, j.ParentId, j.StartingLevel))]);

    /// <summary>Looks a real item up by id, for a test that wants to name one.</summary>
    public static GearFacts Item(uint id) =>
        Items.FirstOrDefault(i => i.ItemId == id)
        ?? throw new KeyNotFoundException($"Item {id} is not in the sampled fixture.");
}
