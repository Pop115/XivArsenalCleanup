using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

// Refreshes ArsenalCleanup's real-gear fixture from an installed copy of the game. See README.md.
//
//   dotnet run --project tools/FixtureBuilder -- <game root> <fixture.json> [item ids to add...]
//
// The sample it rewrites is the sample it read: every id already in the fixture is looked up again,
// so a refresh after a patch changes the facts and never the stratification. Ids on the command
// line are added to it, which is how gear for a new job gets in.
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: <game root> <fixture.json> [item ids to add...]");
    return 1;
}

var gameRoot = args[0];
var fixturePath = args[1];
var extraIds = args[2..].Select(uint.Parse);

var game = new GameData(Path.Combine(gameRoot, "game", "sqpack"), new LuminaOptions
{
    DefaultExcelLanguage = Language.English,
});

var version = File.ReadAllText(Path.Combine(gameRoot, "game", "ffxivgame.ver")).Trim();

// Names are read in English so the fixtures read the same for everyone working on the plugin.
var items = game.Excel.GetSheet<Item>()!;

// Relic detection is not: it reads the player's own language, and the bracketed tag the rule keys
// on is not used in every one of them. The French sheet tags Eureka and Zodiac gear where the
// English one writes flavour text, so a fixture built from English descriptions would quietly stop
// covering the relic guard at all.
var localised = game.Excel.GetSheet<Item>(Language.French, null)!;

var classJobs = game.Excel.GetSheet<ClassJob>()!;
var categories = game.Excel.GetSheet<RawRow>(null, "ClassJobCategory")!;
var armoire = game.Excel.GetSheet<Cabinet>()!.Select(c => c.Item.RowId).Where(id => id != 0).ToHashSet();

var jobIds = classJobs
    .Where(job => job.RowId != 0 && job.Abbreviation.ExtractText().Length > 0)
    .Select(job => job.RowId)
    .ToList();

// The same rule the plugin itself falls back on: one column per class, in ClassJob row order, after
// a single leading Adventurer column. Reading by position rather than by name is what lets this
// build a fixture for a job the game-data library has not caught up with yet.
List<uint> JobsFor(uint categoryRowId)
{
    if (!categories.TryGetRow(categoryRowId, out var row)) return [];

    var allowed = new List<uint>();
    foreach (var id in jobIds)
    {
        var column = (int)id + 1;
        if (column < row.Columns.Count && row.ReadColumn(column) is true) allowed.Add(id);
    }

    return allowed;
}

static string? SlotFor(EquipSlotCategory category) =>
    category.MainHand > 0 ? "main"
    : category.OffHand > 0 ? "offhand"
    : category.Head > 0 ? "head"
    : category.Body > 0 ? "body"
    : category.Gloves > 0 ? "hand"
    : category.Legs > 0 ? "legs"
    : category.Feet > 0 ? "feet"
    : category.Ears > 0 ? "ear"
    : category.Neck > 0 ? "neck"
    : category.Wrists > 0 ? "wrist"
    : category.FingerL > 0 || category.FingerR > 0 ? "ring"
    : category.SoulCrystal > 0 ? "soul_crystal"
    : null;

// RelicDetection.LooksLikeRelic, which cannot be referenced from here without dragging the plugin's
// Dalamud dependencies into a tool that has no game process to load them.
static bool LooksLikeRelic(string description) => description.Length > 0 && description[0] == '[';

var existing = JsonNode.Parse(File.ReadAllText(fixturePath))!["items"]!.AsArray()
    .Select(item => (uint)item!["id"]!);

var entries = new List<Entry>();
var skipped = new List<uint>();

foreach (var id in existing.Concat(extraIds).Distinct().Order())
{
    if (!items.TryGetRow(id, out var item)
        || item.EquipSlotCategory.ValueNullable is not { } slotCategory
        || SlotFor(slotCategory) is not { } slot)
    {
        skipped.Add(id);
        continue;
    }

    entries.Add(new Entry(
        id,
        item.Name.ExtractText(),
        slot,
        (ushort)item.LevelItem.RowId,
        item.LevelEquip,
        [.. JobsFor(item.ClassJobCategory.RowId)],
        (ushort)item.DefensePhys,
        (ushort)item.DefenseMag,
        LooksLikeRelic(localised.GetRow(id).Description.ExtractText()),
        item.Desynth > 0,
        armoire.Contains(id)));
}

var fixture = new FixtureFile(
    "A stratified sample of real Final Fantasy XIV item data, for tests. Generated from the game's "
    + "own sheets; see CONTRIBUTING.md. Not exhaustive by design.",
    version,
    [.. entries]);

File.WriteAllText(fixturePath, JsonSerializer.Serialize(fixture, new JsonSerializerOptions
{
    WriteIndented = true,
    // Matches what is already committed, so a refresh shows up as a diff of facts and not of layout.
    IndentSize = 1,
    NewLine = "\n",
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
}));

Console.WriteLine($"{entries.Count} item(s) written to {fixturePath}, from game {version}.");
Console.WriteLine($"{entries.Count(e => e.IsRelic)} relic(s), "
    + $"{entries.Count(e => e.DefencePhysical > 0)} with defence, "
    + $"{entries.Count(e => e.JobIds.Length == 0)} usable by no class.");
if (skipped.Count > 0)
{
    Console.WriteLine($"skipped {skipped.Count} id(s) with no row or no slot: {string.Join(", ", skipped)}");
}

return 0;

/// <summary>One item, exactly as the fixture stores it.</summary>
internal sealed record Entry(
    uint Id, string Name, string Slot, ushort ItemLevel, byte EquipLevel,
    uint[] JobIds, ushort DefencePhysical, ushort DefenceMagical,
    bool IsRelic, bool IsDesynthesisable, bool IsArmoireEligible);

internal sealed record FixtureFile(
    [property: JsonPropertyName("_comment")] string Comment,
    string GameVersion,
    Entry[] Items);
