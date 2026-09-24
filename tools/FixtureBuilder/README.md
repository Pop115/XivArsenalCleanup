# FixtureBuilder

Refreshes `ArsenalCleanup.Tests/TestData/real-items.json` from an installed copy of the game.

The tests run against real gear — real item levels, equip levels, defence and class categories —
because invented numbers are too well behaved to catch anything. Until now the sample was made by
hand and there was no way to remake it; this is that way.

```
dotnet run --project tools/FixtureBuilder -- "<game root>" ArsenalCleanup.Tests/TestData/real-items.json
```

`<game root>` is the folder containing `game/sqpack` — usually
`C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn`.

Every id already in the fixture is looked up again and written back, so a refresh after a patch
changes the facts and never the stratification. To add gear — a new job's equipment, or the items
from a bug worth keeping — pass the ids after the fixture path:

```
dotnet run --project tools/FixtureBuilder -- "<game root>" ArsenalCleanup.Tests/TestData/real-items.json 50742 50745
```

Then run the suite. `RealGameDataTests` is the gate: it fails if the new sample has stopped covering
a slot, a flag the triage has a rule for, the level range of the game, or the specific items from a
reported bug. A sample that no longer represents the game is worse than an obviously stale one.

The class list in `real-jobs.json` is not rebuilt here. It carries both abbreviations — the English
one the game's sheets are keyed by and the translated one the player reads — which means it has to
come from a client in the language the plugin is being checked against.

This tool is not part of `ArsenalCleanup.slnx` and never runs on CI: it needs the game installed,
which CI does not have.
