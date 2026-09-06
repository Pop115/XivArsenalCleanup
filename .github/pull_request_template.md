<!--
Keep this short. The commit messages carry the detail; this is for whatever a reviewer needs that
the diff does not say on its own.
-->

## What this changes

## Why

<!-- If it fixes a wrong verdict, name the piece and what the plugin said about it. A specific item
     is worth more than a description of the rule. -->

## How it was checked

- [ ] `dotnet test ArsenalCleanup.slnx --configuration Release` passes
- [ ] A test fails before this change and passes after, where that applies
- [ ] Tried in game, if it touches anything Dalamud renders or reads

<!-- CI runs formatting, build with warnings as errors, tests, a test-count floor and a coverage
     floor. If a gate fails, the fix is the code rather than the gate. -->
