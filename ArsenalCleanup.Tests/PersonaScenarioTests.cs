using System.Collections.Generic;
using System.Linq;
using ArsenalCleanup.Triage;
using ArsenalCleanup.Windows;
using Xunit;

namespace ArsenalCleanup.Tests;

/// <summary>
/// Five players, each walked from what their character owns to every word the plugin puts in front
/// of them.
///
/// These differ from the rule-by-rule suites in what they assert: not "the verdict is Obsolete", but
/// "the sentence this person reads names the piece that replaced theirs, and the count in the info
/// bar matches what the window lists". A rule can be individually right and collectively useless,
/// and that is the failure these are here to catch.
///
/// Each persona carries the real numbers from a situation the plugin has actually been wrong about,
/// or an edge that would break it: a character with nothing, a shared class lineage, a non-English
/// client, an item the game has no data for.
/// </summary>
public sealed class PersonaScenarioTests
{
    private const uint Gladiator = 1, Pugilist = 2, Archer = 5, Paladin = 19, Monk = 20, Bard = 23,
                       Carpenter = 8, Culinarian = 15, Dancer = 38, Sage = 40, Rogue = 29,
                       Ninja = 30, Gunbreaker = 37, Viper = 41;

    /// <summary>A French client, because that is where the abbreviation bug lived.</summary>
    private static readonly JobDirectory FrenchClient = new([
        new JobInfo(Gladiator, "GLA", "GLA", Gladiator),
        new JobInfo(Pugilist, "PGL", "PGL", Pugilist),
        new JobInfo(Archer, "ARC", "ARC", Archer),
        new JobInfo(Carpenter, "CRP", "MEN", Carpenter),
        new JobInfo(Culinarian, "CUL", "CUI", Culinarian),
        new JobInfo(Paladin, "PLD", "PLD", Gladiator),
        new JobInfo(Monk, "MNK", "MOI", Pugilist),
        new JobInfo(Bard, "BRD", "BRD", Archer),
        new JobInfo(Dancer, "DNC", "DNS", Dancer, StartingLevel: 70),
        new JobInfo(Sage, "SGE", "SAG", Sage, StartingLevel: 70),
        new JobInfo(Rogue, "ROG", "SUR", Rogue),
        new JobInfo(Ninja, "NIN", "NIN", Rogue),
        new JobInfo(Gunbreaker, "GNB", "PSB", Gunbreaker, StartingLevel: 60),
        new JobInfo(Viper, "VPR", "VPR", Viper, StartingLevel: 80),
    ]);

    private static GearFacts Item(
        uint id, string name, string slot, ushort itemLevel, byte equipLevel, uint[] jobs,
        bool relic = false, bool desynthesisable = false, bool armoire = false) =>
        new(id, name, slot, itemLevel, equipLevel, IconId: 1, jobs, relic, desynthesisable, armoire);

    private static OwnedPiece Owns(GearFacts facts, bool worn = false, int position = 0) =>
        new(facts.ItemId, facts.Slot, position, worn, facts);

    private static Dictionary<uint, int> Levels(params (uint Job, int Level)[] levels)
    {
        var map = new Dictionary<uint, int>();
        foreach (var (job, level) in levels) map[job] = level;
        return map;
    }

    private static string Reason(TriageResult result, string name) =>
        Presentation.Reason(result.Pieces.First(p => p.Facts.Name == name), FrenchClient);

    private static TriagedPiece Row(TriageResult result, string name) =>
        result.Pieces.First(p => p.Facts.Name == name);

    /// <summary>
    /// **Léna, three hours into the game.** Level 14 Gladiator, has never touched another class, owns
    /// her starting sword, one quest ring usable by every class, and a spare shield.
    ///
    /// What must happen: the plugin stays silent. She has nothing to clear out, and being told to
    /// "keep this until level 1" — the bug that untouched classes at level 0 used to produce — would
    /// be her first impression of the plugin.
    /// </summary>
    [Fact]
    public void Lena_ThreeHoursIn_IsToldNothingNeedsDoing()
    {
        var sword = Item(1, "Weathered Shortsword", "main", 4, 1, [Gladiator, Paladin]);
        var ring = Item(2, "Silver Ring", "ring", 1, 1, [.. FrenchClient.Jobs.Select(j => j.Id)]);
        var shield = Item(3, "Square Maple Shield", "offhand", 4, 1, [Gladiator, Paladin]);

        var result = ArsenalTriage.Run(
            [Owns(sword, worn: true), Owns(ring), Owns(shield, worn: true)],
            Levels((Gladiator, 14)));

        // Nothing to act on, so the info bar shows nothing at all.
        Assert.Empty(result.Actionable);
        Assert.Equal(0, result.Count(DisposalMethod.Discard));

        // And no sentence anywhere tells her to wait for a level she is already past.
        foreach (var piece in result.Pieces)
        {
            var reason = Presentation.Reason(piece, FrenchClient);
            Assert.DoesNotContain("level 1, and none", reason, System.StringComparison.Ordinal);
            Assert.NotEmpty(reason);
        }

        Assert.Equal("every class", Presentation.JobList(Row(result, "Silver Ring").Jobs, FrenchClient));
    }

    /// <summary>
    /// **Marc, levelling Bard, at the exact case that started this project.** Archer and Bard share
    /// one experience bar and he is level 41 on it. His chest holds a Survival Bow (item level 34,
    /// wearable from 32) and a Magitek Bow (item level 130, wearable from 50).
    ///
    /// What must happen: the low bow is his best *today* and is kept; the high one is kept and
    /// labelled with the level that unlocks it, naming ARC and BRD. Comparing item levels alone would
    /// bin the only bow he can hold and leave him weaponless for nine levels.
    /// </summary>
    [Fact]
    public void Marc_LevellingBard_KeepsTheOnlyBowHeCanActuallyHold()
    {
        var survival = Item(1, "Survival Bow", "main", 34, 32, [Archer, Bard]);
        var magitek = Item(2, "Magitek Bow", "main", 130, 50, [Archer, Bard]);

        var result = ArsenalTriage.Run(
            [Owns(survival, worn: true), Owns(magitek, position: 3)],
            Levels((Archer, 41), (Bard, 41)));

        Assert.Equal(Verdict.KeptBest, Row(result, "Survival Bow").Verdict);
        Assert.Equal(Verdict.KeptUntilLevel, Row(result, "Magitek Bow").Verdict);
        Assert.Empty(result.Actionable);

        // The sentence has to say which of his classes is waiting, and on what.
        var waiting = Reason(result, "Magitek Bow");
        Assert.Contains("ARC", waiting);
        Assert.Contains("BRD", waiting);
        Assert.Contains("50", waiting);

        // Hovering the bow in his chest gives him the same answer as the window.
        Assert.Equal(Verdict.KeptUntilLevel, Assert.Single(result.PiecesOf(2)).Verdict);
    }

    /// <summary>
    /// **Sofia, at end game with a full chest.** Twelve main hands for Paladin at level 100: ten
    /// ordinary raid weapons, one Armoire-eligible glamour piece, and her relic. She also wears one
    /// Cesti and keeps an identical spare.
    ///
    /// What must happen: a short list, the disposal method chosen to lose the least, the relic
    /// protected but explained, and the worn copy told apart from the spare.
    /// </summary>
    [Fact]
    public void Sofia_AtEndGame_GetsAShortListThatLosesTheLeast()
    {
        var raid = Enumerable.Range(1, 10)
            .Select(i => Item((uint)i, $"Raid Blade {i}", "main", (ushort)(400 + (i * 10)), 90, [Paladin], desynthesisable: true))
            .ToList();
        var glamour = Item(50, "Ceremonial Sword", "main", 100, 50, [Paladin], armoire: true);
        var relic = Item(60, "Manderville Greatsword", "main", 645, 90, [Paladin], relic: true);
        // Her current best, and the only thing that outclasses the relic.
        var current = Item(65, "Malefic", "main", 690, 100, [Paladin]);
        var cesti = Item(70, "Cesti", "hand", 19, 19, [Pugilist, Monk], desynthesisable: true);

        var owned = raid.Select((f, i) => Owns(f, position: i)).ToList();
        owned.Add(Owns(glamour, position: 20));
        owned.Add(Owns(relic, position: 21));
        owned.Add(Owns(current, worn: true));
        owned.Add(Owns(cesti, worn: true));
        owned.Add(Owns(cesti, position: 30));

        var result = ArsenalTriage.Run(owned, Levels((Paladin, 100), (Pugilist, 50), (Monk, 50)));

        // The weapon she is wearing is the keeper; every raid blade below it is desynthesisable.
        Assert.Equal(Verdict.KeptBest, Row(result, "Malefic").Verdict);
        Assert.Equal(DisposalMethod.Desynthesis, Row(result, "Raid Blade 1").Disposal);
        Assert.Equal(DisposalMethod.Desynthesis, Row(result, "Raid Blade 10").Disposal);
        // Named after the best she owns, not the first blade that happened to beat it.
        Assert.Contains("Malefic", Reason(result, "Raid Blade 1"));

        // The glamour piece goes to the Armoire rather than the bin: nothing is lost that way.
        Assert.Equal(DisposalMethod.Armoire, Row(result, "Ceremonial Sword").Disposal);

        // The relic is never given a disposal method, but she is told what outclassed it.
        var relicRow = Row(result, "Manderville Greatsword");
        Assert.Equal(Verdict.NeedsReview, relicRow.Verdict);
        Assert.Equal(DisposalMethod.None, relicRow.Disposal);
        Assert.Contains("Malefic", Reason(result, "Manderville Greatsword"));
        Assert.Contains("Relic", Reason(result, "Manderville Greatsword"));

        // Two Cesti: hovering one shows both, and only the spare can be acted on without undressing.
        var bothCesti = result.PiecesOf(70);
        Assert.Equal(2, bothCesti.Count);
        Assert.Single(bothCesti, p => p.Piece.IsEquipped);
    }

    /// <summary>
    /// **Kenji, on a French client.** He plays Dancer and Sage, which his game calls DNS and SAG. He
    /// also owns a piece for a class this build has never heard of — the job a future patch adds.
    ///
    /// What must happen: his gear is found at all (the abbreviation bug made 26 of 43 classes own
    /// nothing), the wording uses the abbreviations his game uses, and the unknown class degrades to
    /// something legible instead of crashing a tooltip.
    /// </summary>
    [Fact]
    public void Kenji_OnAFrenchClient_SeesHisOwnAbbreviations()
    {
        var oldChakrams = Item(1, "Chakrams usés", "main", 300, 70, [Dancer], desynthesisable: true);
        var newChakrams = Item(2, "Chakrams neufs", "main", 500, 70, [Dancer]);
        var nouliths = Item(3, "Nouliths", "main", 400, 70, [Sage]);
        var futureJobGear = Item(4, "Arme inconnue", "main", 100, 1, [99]);

        var result = ArsenalTriage.Run(
            [Owns(oldChakrams), Owns(newChakrams), Owns(nouliths), Owns(futureJobGear)],
            Levels((Dancer, 90), (Sage, 90), (99, 50)));

        // Found at all — which is what the bug broke.
        Assert.Equal(Verdict.Obsolete, Row(result, "Chakrams usés").Verdict);
        Assert.Contains("Chakrams neufs", Reason(result, "Chakrams usés"));

        // Named the way his game names them.
        Assert.Contains("DNS", Reason(result, "Chakrams neufs"));
        Assert.Contains("SAG", Reason(result, "Nouliths"));
        Assert.DoesNotContain("DNC", Reason(result, "Chakrams neufs"));

        // A class from a patch this build predates reads as odd, never as a crash.
        Assert.Equal("#99", Presentation.JobList([99], FrenchClient));
        Assert.NotEmpty(Reason(result, "Arme inconnue"));
    }

    /// <summary>
    /// **Robin, a crafter, with a chest the plugin cannot fully understand.** Carpenter and Culinarian
    /// tools, a soul crystal, an item whose id the game's sheet has no row for — a mod, a patch the
    /// catalogue predates, a corrupted read — and a piece that resolves to no class at all.
    ///
    /// What must happen: nothing throws, nothing unexplained is quietly suggested for the bin, and
    /// every row still carries a sentence a human can read. This is the persona that stands in for
    /// "the inventory contained something nobody predicted".
    /// </summary>
    [Fact]
    public void Robin_WithDataThePluginCannotRead_IsNeverLeftGuessing()
    {
        var oldSaw = Item(1, "Scie usée", "main", 100, 50, [Carpenter], desynthesisable: true);
        var newSaw = Item(2, "Scie neuve", "main", 200, 50, [Carpenter]);
        var skillet = Item(3, "Poêle à frire de Cielacier", "main", 440, 70, [Culinarian], relic: true);
        var betterSkillet = Item(4, "Poêle du saint de la main", "main", 500, 80, [Culinarian]);
        var crystal = Item(5, "Cristal de moine", "soul_crystal", 30, 30, [Monk]);
        var jobless = Item(6, "Objet sans classe", "body", 100, 50, []);
        var unknown = new OwnedPiece(9999, "head", 0, false, null);

        var result = ArsenalTriage.Run(
            [Owns(oldSaw), Owns(newSaw), Owns(skillet), Owns(betterSkillet), Owns(crystal),
             Owns(jobless), unknown],
            Levels((Carpenter, 90), (Culinarian, 90), (Monk, 50)));

        // The ordinary case still works around the broken ones.
        Assert.Equal(Verdict.Obsolete, Row(result, "Scie usée").Verdict);
        Assert.Contains("MEN", Reason(result, "Scie neuve"));

        // The relic is protected and explained, exactly as for a weapon.
        Assert.Equal(Verdict.NeedsReview, Row(result, "Poêle à frire de Cielacier").Verdict);
        Assert.Contains("Poêle du saint de la main", Reason(result, "Poêle à frire de Cielacier"));

        // The soul crystal is set aside rather than compared against anything.
        Assert.Single(result.SoulCrystals);
        Assert.DoesNotContain(result.Pieces, p => p.Facts.Slot == "soul_crystal");

        // Neither unreadable piece is ever suggested for disposal, and both say why.
        foreach (var name in new[] { "Objet sans classe", "#9999" })
        {
            var row = result.Pieces.First(p => p.Facts.Name == name);
            Assert.Equal(Verdict.NeedsReview, row.Verdict);
            Assert.Equal(DisposalMethod.None, row.Disposal);
            Assert.NotEmpty(Presentation.Reason(row, FrenchClient));
        }

        // And the count the info bar would show matches what the window would list.
        Assert.Equal(result.Actionable.Count(), result.Pieces.Count(p =>
            p.Verdict is Verdict.Obsolete or Verdict.NeedsReview));
    }
    /// <summary>
    /// **Pop, with a pair of level-1 gloves usable by every class.** "Gants d'Ehcatl" really are
    /// item level 6, wearable from level 1, and flagged for all classes. He has never touched
    /// Gunbreaker or Viper, and owns proper gloves for both.
    ///
    /// What must happen: the gloves are not offered as "your best" for classes that cannot exist at
    /// level 1. Gunbreaker begins at 60 and Viper at 80 — a character who has never started them
    /// would begin there, so their real gloves are already within reach and the level-1 pair is not.
    /// Reading an untouched class as level 1 made those two claim a pair of starter gloves.
    /// </summary>
    [Fact]
    public void Pop_WithStarterGlovesForEveryClass_IsNotToldTheyAreBestForAClassThatStartsAt60()
    {
        var starter = Item(8568, "Gants d'Ehcatl", "hand", 6, 1,
            [Rogue, Ninja, Gunbreaker, Viper, Pugilist, Monk]);
        var tankGloves = Item(2, "Gants de fantassin", "hand", 130, 50, [Gunbreaker]);
        var viperGloves = Item(3, "Gants de rôdeur", "hand", 600, 80, [Viper]);

        // Rogue and Ninja untouched too, but they share a bar that really does begin at 1.
        var levels = JobLevelTable.WithLineageFilledIn(
            Levels((Rogue, 0), (Ninja, 0), (Gunbreaker, 0), (Viper, 0), (Pugilist, 0), (Monk, 0)),
            FrenchClient.Lineages());

        var result = ArsenalTriage.Run(
            [Owns(starter), Owns(tankGloves), Owns(viperGloves)], levels);

        var glovesReason = Reason(result, "Gants d'Ehcatl");
        Assert.DoesNotContain("PSB", glovesReason);
        Assert.DoesNotContain("VPR", glovesReason);

        // A class whose bar genuinely starts at 1 may still claim them, which is correct: a level-1
        // Rogue would wear exactly these.
        Assert.Contains("SUR", glovesReason);

        // And the gear those two classes would actually begin with is what they are told to keep —
        // it shares the slot with the starter pair, so it is the best of a group rather than a lone
        // piece.
        Assert.Equal(Verdict.KeptBest, Row(result, "Gants de fantassin").Verdict);
        Assert.Contains("PSB", Reason(result, "Gants de fantassin"));
        Assert.Equal(Verdict.KeptBest, Row(result, "Gants de rôdeur").Verdict);
        Assert.Contains("VPR", Reason(result, "Gants de rôdeur"));
    }
}
