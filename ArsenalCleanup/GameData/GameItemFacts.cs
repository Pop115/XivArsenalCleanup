using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ArsenalCleanup.Triage;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace ArsenalCleanup.GameData;

/// <summary>
/// Turns the game's own sheets into the plain facts the triage works from.
///
/// This is the one place that reads Lumina, which is why it is excluded from coverage: a typed
/// sheet row cannot be constructed outside a loaded game. Everything with a rule in it —
/// slot mapping, relic detection — lives in a pure function this class merely calls.
///
/// Reading the sheets directly is what lets this plugin stand alone: there is no catalogue file to
/// generate, ship or keep in step with the game's patches. The sheets are the patch.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class GameItemFacts
{
    private readonly IDataManager data;
    private readonly Dictionary<uint, GearFacts?> cache = [];
    private readonly HashSet<uint> armoireEligible;

    public GameItemFacts(IDataManager data)
    {
        this.data = data;

        // The Cabinet sheet is the game's own list of what the Armoire can store permanently. It is
        // small (a few thousand rows) and never changes at runtime, so it is read once.
        armoireEligible = [.. data.GetExcelSheet<Cabinet>()!
            .Select(row => row.Item.RowId)
            .Where(id => id != 0)];
    }

    /// <summary>
    /// Facts for one item id, or null when the sheet has no such row or the item occupies no slot
    /// this plugin triages. Cached: an armoury of several hundred pieces asks for the same ids
    /// repeatedly, and the answer cannot change while the game is running.
    /// </summary>
    public GearFacts? For(uint itemId)
    {
        if (cache.TryGetValue(itemId, out var cached)) return cached;

        var facts = Resolve(itemId);
        cache[itemId] = facts;
        return facts;
    }

    private GearFacts? Resolve(uint itemId)
    {
        var sheet = data.GetExcelSheet<Item>();
        if (sheet is null || !sheet.TryGetRow(itemId, out var item)) return null;

        var category = item.EquipSlotCategory.ValueNullable;
        if (category is null) return null;

        var slot = EquipSlotResolver.Resolve(new EquipSlotFlags(
            category.Value.MainHand, category.Value.OffHand, category.Value.Head, category.Value.Body,
            category.Value.Gloves, category.Value.Waist, category.Value.Legs, category.Value.Feet,
            category.Value.Ears, category.Value.Neck, category.Value.Wrists, category.Value.FingerL,
            category.Value.FingerR, category.Value.SoulCrystal));
        if (slot is null) return null;

        return new GearFacts(
            ItemId: itemId,
            Name: item.Name.ExtractText(),
            Slot: slot,
            ItemLevel: (ushort)item.LevelItem.RowId,
            EquipLevel: item.LevelEquip,
            IconId: item.Icon,
            JobIds: JobIdsFor(item),
            IsRelic: RelicDetection.LooksLikeRelic(item.Description.ExtractText()),
            IsDesynthesisable: item.Desynth > 0,
            IsArmoireEligible: armoireEligible.Contains(itemId));
    }

    /// <summary>
    /// Every ClassJob row id allowed to equip this item.
    ///
    /// ClassJobCategory carries one boolean column per job abbreviation, so the job rows are walked
    /// and each is asked whether its own abbreviation is set — which keeps this correct when a new
    /// job is added to the game, unlike a hand-maintained list of column names.
    /// </summary>
    private List<uint> JobIdsFor(Item item)
    {
        var category = item.ClassJobCategory.ValueNullable;
        if (category is null) return [];

        var jobs = data.GetExcelSheet<ClassJob>();
        if (jobs is null) return [];

        var allowed = new List<uint>();
        foreach (var job in jobs)
        {
            var abbreviation = job.Abbreviation.ExtractText();
            if (abbreviation.Length == 0) continue;
            if (ClassJobCategoryFlags.IsSet(category.Value, abbreviation)) allowed.Add(job.RowId);
        }
        return allowed;
    }
}
