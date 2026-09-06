# Changelog

Notable changes, newest first. Versions match the `AssemblyVersion` in the plugin manifest, and a
release is cut by pushing an `arsenalcleanup-v*` tag.

## 0.4.0

The plugin stands on its own.

### Added

- **A window of its own.** The plugin now decides whether each piece is still worth keeping, in
  game, and shows the answer: what can go and how best to part with it, pieces worth keeping hidden
  until asked for, slot tabs in the game's own order, worn gear ahead of the chest.
- Item levels, equip levels, job restrictions, desynthesis and Armoire eligibility now come from the
  game's own sheets, and job levels from the player state. Nothing has to be downloaded or kept in
  step with patches.
- `/arsenalcleanup` opens the window; `/arsenalcleanup export` writes the JSON export.
- **A verdict panel that follows the cursor.** Hover a piece anywhere the game shows one and it
  says whether to keep it and how to part with it — the point of running this in game rather than
  reading a list somewhere else. Switchable in the settings.
- **A server info bar entry** showing how much is left to deal with, hidden when there is nothing,
  and opening the window when clicked.
- Item icons in the window: a player recognises a piece by its icon long before they finish
  reading its name.
- A release workflow, and a Dalamud plugin repository manifest, so the plugin can be installed
  without building it.

### Fixed

- **Over half the game's classes were invisible on a non-English client.** Class abbreviations were
  read in the player's language and then used to look up the game's ClassJobCategory columns, which
  are named after the fixed English ones. On a French client 26 of the 43 differ — Dancer is DNS,
  Sage is SAG, Monk is MOI — so those classes matched no column, owned no gear as far as the triage
  was concerned, and their wardrobe was judged against a fraction of itself. Lookups now use the
  English abbreviation; the player still reads their own.
- **Classes are judged from the level they actually begin at.** An untouched class was assumed to
  begin at 1, so a pair of level-1 gloves flagged for all classes came out as "your best" for
  Gunbreaker and Viper — classes that cannot exist below 60 and 80. The level now comes from the
  game's own `ClassJob.StartingLevel`, so a job with no base class begins where it unlocks, and one
  that shares a class's experience bar begins where that bar does.
- **Gear for every class no longer waits on a class nobody has started.** A class never touched
  reports level 0, so a level-1 item usable by all classes came out "keep until this job reaches
  level 1". Every class starts at 1 the moment it is picked up, so an unstarted one is judged at 1.
- **The same answer whatever order the inventory arrives in.** Where two classes reached the same
  conclusion, which one was reported depended on read order; and two different items sharing a
  display name could be folded into one row. Found by the property tests, not by a player.

### Changed

- **Reasons name the classes they are about.** "Keep until this job reaches level 50" is accurate
  and useless when nine classes can wear the piece; it now reads "Keep for PGL, MOI: wearable from
  level 50, and none of them are there yet", collapsing to "every class" when it really is all of
  them. Wording moved out of the decision logic into one presentation file, which is also what would
  make translating the plugin a single-file job.

- The replacement shown for a superseded piece is now the best candidate owned, not the first one
  found. The verdict never depended on this, but the name shown is what a player reads to decide.
- Manifest description and tags now describe the triage rather than the export.

### Kept working

- `/arsenalexport` and `/arsenalexportconfig` still write the export and open the settings, hidden
  from the command list.

## 0.3.0

- Renamed from ArsenalExport to ArsenalCleanup, and moved to its own repository.
- MIT licence, contributing guide, `.editorconfig`, and CI: formatting, build with warnings as
  errors, tests, and a coverage floor.

## 0.2.0

- **Worn gear is exported.** It lives in its own container and appears in no Armoury tab, so an
  export built from the chest alone was missing, for every slot, the piece most likely to be the
  best one owned.
- Export field names moved to English, and the document gained a `format_version` so a consumer can
  refuse a file it does not understand instead of reading missing fields as defaults.
- The export folder defaults to the plugin's own config directory rather than a hardcoded path.

## 0.1.0

- First version: reads the twelve Armoury Chest containers through Dalamud's `IGameInventory` and
  writes a JSON export keyed by numeric item id.
