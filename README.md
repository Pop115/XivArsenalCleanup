# ArsenalCleanup

[![CI](https://github.com/Pop115/XivArsenalCleanup/actions/workflows/arsenal-cleanup.yml/badge.svg)](https://github.com/Pop115/XivArsenalCleanup/actions/workflows/arsenal-cleanup.yml)

A Dalamud plugin that tells you which gear in your Armoury Chest you can stop carrying around.

It reads your Armoury Chest **and the gear you are wearing**, compares each piece against everything
else you own for the same slot, and sorts the lot into what to keep and what to let go — taking your
job levels into account, so it never suggests parting with the only weapon you can actually hold.

Everything happens in game. There is no companion app, no catalogue file to download, and no export
to feed to anything: the plugin reads the game's own item data directly, so it stays correct across
patches on its own.

## Where you see it

Three surfaces, none of which need you to leave the game or read a file.

**Hover any piece.** In your Armoury Chest, your bags, or your character sheet — a small panel
follows the cursor and says whether that piece is still worth keeping and what to do with it. This
is the answer to "what can I recycle?": you are already looking at the item, so the plugin meets you
there. Turn it off in the settings if you would rather it stayed quiet.

**`/arsenalcleanup`** opens the full window: everything sorted by slot, in the same order the game
lays out your chest, with icons, verdicts and reasons. Also on the Open button in Dalamud's plugin
installer.

**The server info bar** shows `Arsenal 131` when there is something to deal with, and nothing at
all when there is not. Click it to open the window.

## What it tells you

For each piece:

| Verdict | Meaning |
|---|---|
| **Best you can wear** | Nothing you own beats it for that slot and job, at your level. |
| **Only one you own** | Nothing to compare it against. Kept whatever its level. |
| **Keep — level not reached** | Better than what you use now, but you cannot equip it yet. |
| **Replaced** | Something strictly better is already equippable. Safe to let go. |
| **Check yourself** | A relic, or something judged on incomplete information. Never an automatic call. |

And for anything replaced, *how* to part with it, best option first:

- **Store in the Armoire** — kept forever at no cost, appearance and all.
- **Desynthesise** — destroyed, but you get materials back.
- **Discard** — nothing comes back. The last resort.

Relics are never suggested for disposal, however clear the comparison. They are still shown, with
what would have replaced them, so the decision is yours rather than hidden.

## Why the job level matters

Because comparing item levels alone gets it wrong, and confidently so.

A level-50 bow at item level 130 looks strictly better than a level-32 bow at item level 34. If your
Archer is level 41, throwing the second one away leaves you without a bow for the next nine levels.
This plugin only calls a piece replaced when the thing replacing it is one you can equip *today*.

The same goes for gear you have not grown into yet: it is kept, and labelled with the level that
will unlock it, rather than lumped in with everything else you are keeping.

## Why two rings matter

Because a character wears two of them, and every other slot holds one.

"Something better exists" is a reason to part with a hat. It is not a reason to part with a ring:
the better one goes on the hand it was already going on, and the ring it beat is still the one on
the other hand. So a ring is only spare once *two* better ones can be worn — and when that happens
the plugin names the weaker of the two, because "even this beats it" is the part worth reading.

## Why Beastmaster is judged on defence

Because item level is a good proxy for "better" and a bad one for "survives".

For nearly every job the game scales a piece's whole stat line from its item level, so the highest
one wins and nothing else needs saying. Beastmaster's content is survived rather than raced, and
there the same comparison picks a flimsier outfit with better numbers on it — which is exactly what
the game's own **recommended gear** button does, and why its answer is worth ignoring there.

So for Beastmaster a piece only replaces another when it gives up neither kind of defence *and* no
item level. Two pieces that each win on a different count are left for you to choose between, which
is the honest answer and the one that stops a sturdy coat being flagged for a smarter-looking one.
Nothing changes for any other job, and nothing changes for weapons or the plainest accessories,
which carry no defence for the rule to prefer.

Gear only Beastmaster can wear is cleared out on that basis. Gear it shares with the rest of the
game is kept whenever any class still wants it — the rule already in place — so there the defence
comparison can only ever protect a piece, never condemn one.

## Why worn gear matters

Gear on your active job lives in its own container, not in any Armoury tab. A tool that reads only
the chest is missing, for every slot, the piece most likely to be the best one you own — and will
happily crown a strictly worse one "best available" without flagging anything.

So the plugin reads both, and marks worn pieces as worn: you have to take one off before you can
store, desynthesise or discard it. A worn piece is judged like any other, which means it can come out
as *replaced* — you can be wearing the worse of two.

## Usage

- `/arsenalcleanup` — opens the window.
- `/arsenalcleanup export` — writes a JSON export instead of opening the window.
- `/arsenalcleanupconfig` — settings (export folder, and whether the hover panel appears).

`/arsenalexport` and `/arsenalexportconfig`, the names this plugin used before it was renamed, still
work and still do what they always did. They are hidden from the command list so they do not read as
separate features.

## Installing

Not on the official Dalamud plugin repository, so it installs from this one:

1. In game, `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Add `https://raw.githubusercontent.com/Pop115/XivArsenalCleanup/main/repo.json` and save.
3. `/xlplugins` → search for **Arsenal Cleanup** → Install.

Updates then arrive the same way as for any other plugin.

> That URL only resolves while the repository is public. If it 404s, the repository is private and
> the plugin has to be built and loaded as a dev plugin instead — see
> [CONTRIBUTING.md](CONTRIBUTING.md), which is also where to start if you want to change something.

## The JSON export

The window is the point of the plugin, but an export is there for anyone wiring it into a tool of
their own. Exports go to the folder set in the settings window; leave it empty and they land in an
`exports` subfolder of the plugin's own Dalamud config directory, which the window spells out.

One file per character per day, named `yyyy-MM-dd-Character-Name-arsenal.json`. Re-exporting the same
character on the same day replaces that day's file rather than piling up near-identical snapshots;
previous days are left alone.

```json
{
  "format_version": 1,
  "character": "Hero Example",
  "exported_at": "2026-09-06T18:30:00+02:00",
  "pieces": [
    { "slot": "main", "item_id": 31472, "quantity": 1, "is_hq": false,
      "is_collectable": false, "position": 0, "is_equipped": false },
    { "slot": "main", "item_id": 1688, "quantity": 1, "is_hq": false,
      "is_collectable": false, "position": 0, "is_equipped": true }
  ]
}
```

| Field | Meaning |
|---|---|
| `format_version` | Schema version. Check it and refuse what you do not understand — reading an older file field by field yields `undefined` everywhere instead of one legible error. |
| `slot` | One of `main`, `offhand`, `head`, `body`, `hand`, `legs`, `feet`, `ear`, `neck`, `wrist`, `ring`, `soul_crystal`. A stable ASCII key, never display text: it does not change with the client's language. |
| `item_id` | Base item id, HQ/collectable offset already removed. Joins directly against the game's `Item` sheet. |
| `quantity`, `is_hq`, `is_collectable` | As reported by the game. |
| `position` | Raw slot index inside the piece's own container, so a consumer can reproduce the layout you actually see. |
| `is_equipped` | True when the piece was on your character, false when it was in the chest. |

Two entries can share a slot, a position and even an item id: one worn, one stored. They are two
distinct physical objects, and only the stored one can be acted on without unequipping first.

## How it works

Dalamud's official `IGameInventory` API for what you own, `IDataManager` for the game's item sheets,
and `IPlayerState` for your job levels. No network capture, no undocumented memory access, nothing
written outside the folder you choose.

Which classes may wear a piece comes from the game's `ClassJobCategory` sheet, which has one column
per class. Those columns are normally read by name — and a patch ships a new job before the library
this plugin reads sheets with has a name for it, which is how Beastmaster's own equipment came out
as "check yourself" for a patch cycle. Any class without a name is now read by position instead: the
columns run in class order, and that layout is checked against every class that *does* have a name,
on every row of the sheet, before it is trusted.

Equipped gear is one flat 14-entry array whose order is the game's own (the same order as
FFXIVClientStructs' `RaptureGearsetModule.GearsetItemIndex`, from main hand at 0 to soul crystal at
13). Index 5 is the belt slot, removed from the game years ago; it is skipped rather than exported
under a guessed name.

A relic is recognised by its description opening with a bracketed tag — `[Manderville Weapon]`,
`[Antique Weapon]` — the convention Square Enix has used on every relic line, in every language.
The obvious alternatives are worse: "unique" and "untradable" are true of nearly all endgame gear
and would protect everything, drowning the result rather than guarding anything.

## Language

Item names and class abbreviations follow your game client: on a French client the plugin says DNS
for Dancer and SAG for Sage, because that is what the rest of your game says. The plugin's own
wording is English for now — all of it lives in one file, so translating it is a contained job
rather than a hunt through the decision logic.

## Known limitations

Each of these is tracked as an issue; the links are where to say it matters to you.

- [Melded materia is invisible to the triage](https://github.com/Pop115/XivArsenalCleanup/issues/1).
  Desynthesising a melded piece destroys the materia, and the plugin does not yet know it is there.
  **The one limitation that can cost you something.**
- [Glamour plates and gearsets are not taken into account](https://github.com/Pop115/XivArsenalCleanup/issues/2):
  a piece kept only for its appearance reads as replaced. The Armoire suggestion exists partly for
  that reason.
- [The window does not notice when you act on its advice](https://github.com/Pop115/XivArsenalCleanup/issues/3)
  until you press Refresh.
- [Retainer and saddlebag inventories are not read](https://github.com/Pop115/XivArsenalCleanup/issues/5).
- [The window covers whoever is logged in](https://github.com/Pop115/XivArsenalCleanup/issues/6);
  there is no account-wide view.

## Licence

MIT — see [LICENSE](LICENSE).
