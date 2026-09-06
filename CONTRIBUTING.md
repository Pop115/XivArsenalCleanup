# Contributing to ArsenalCleanup

Thanks for taking a look. This is a small, deliberately boring codebase, and the aim is to keep it
that way: the interesting part is the decision about whether a piece of gear is still worth keeping,
and everything else exists to get out of its way.

## Getting a build

You need:

- The **.NET 10 SDK**.
- **Dalamud's assemblies.** If XIVLauncher is installed and the game has been launched with Dalamud
  at least once, they are already at `%AppData%\XIVLauncher\addon\Hooks\dev\` and nothing else is
  needed. Otherwise, unpack
  [latest.zip](https://goatcorp.github.io/dalamud-distrib/latest.zip) somewhere and point
  `DALAMUD_HOME` at that folder — the plugin project and the test project both honour it, and so
  does CI.

```bash
dotnet build ArsenalCleanup.slnx --configuration Release
dotnet test ArsenalCleanup.slnx --configuration Release
```

To try it in game: `/xlsettings` → **Experimental** → add the full path to
`ArsenalCleanup/bin/Release/ArsenalCleanup.dll` to **Dev Plugin Locations**, then enable it under
`/xlplugins` → **Dev Tools**. After a rebuild, disable and re-enable it: Dalamud does not reload a
DLL it has already loaded.

## What CI checks

Every push and pull request runs the same four gates, on Windows:

1. `dotnet format --verify-no-changes` — formatting is settled by `.editorconfig`, so it never has
   to be discussed in review.
2. `dotnet build` with **warnings as errors** and the .NET analysers at `Recommended`. If the build
   is clean locally it will be clean there.
3. `dotnet test` with coverage.
4. A coverage floor: 90% of lines, 85% of branches.

If a gate fails, the fix is the code, not the gate.

## How the code is arranged

```
ArsenalCleanup/
  Plugin.cs           Dalamud wiring. Holds no decisions.
  ArmoryReader.cs     Which containers to read, and what to skip.
  ArmorySlot.cs       Container and equipped-index → slot name.
  ExportWriter.cs     File naming and writing.
  Triage/             Whether a piece is worth keeping. Pure, and the heart of the plugin.
  GameData/           The game's sheets and player state → plain data the triage can chew on.
  Windows/            ImGui. Presentation.cs holds the wording and colours, and is pure.
```

The rule that keeps this testable: **anything that decides something lives in a pure function**, and
anything that touches Dalamud is a thin adapter marked `[ExcludeFromCodeCoverage]`. That attribute
is a statement that the type contains no logic worth testing — if you find yourself adding an `if`
to one of them, that `if` belongs somewhere else.

The same rule is why `IInventorySource` exists: the game sits behind a one-method interface, so the
collection rules can be exercised against a fake inventory.

## Tests

Every rule has a test, and every test says what behaviour it protects rather than what line it
touches — the names are sentences on purpose, and underscores are what keeps them readable.

When a test needs a piece of gear, build it with the helpers in `TriageFixtures`: the defaults are
deliberately unremarkable, so whatever a test spells out is the thing it is actually about.

If you fix a bug, the test that comes with it should fail before the fix and pass after. Where the
bug came from a real situation, say so in a comment — several tests here carry the exact item names
and levels that exposed the problem, and that context is worth more than the assertion.

## Style

- Comments explain **why**, not what. The code already says what.
- No raw identifier ever reaches the screen: everything the player reads goes through
  `Presentation`, which has a readable fallback for values it has never seen.
- Prefer boring, obvious code. This plugin decides whether someone should throw away gear they may
  have spent months on; being clever is not worth the risk.
