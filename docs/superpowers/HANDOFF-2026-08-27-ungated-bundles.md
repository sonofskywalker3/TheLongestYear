# Handoff: bundles that no season gate ever asks for

**For a fresh agent with no prior context. Read this whole file before touching anything.**

## 1. The problem, precisely

Every Community Center bundle should apply pressure at the season checkpoints: by Spring 28,
Summer 28 and Fall 28 the player must have donated a certain amount of it, or the year fails and
the Junimos rewind. A few bundles apply **no pressure at all** and are only needed for the final
Winter 28 win check, so a player can ignore them for three seasons.

Found by `tly_gatecheck` on live boards, 2026-08-27. Confirmed cases:

- **Orchard** (the mod's own authored bundle) - requires 5, shows 5, no curated quota. Ungated.
- **Helper's** (vanilla) - requires 2, shows 2. Ungated.

This is NOT widespread. Ten of the mod's eleven authored bundles are gated, and the twelve vanilla
bundles the pin table was written for all gate correctly. Do not "fix" what is already working.

## 2. Why it happens

`BundleClassifier.Classify` sorts every bundle into one of three kinds, and each kind gates
differently:

| Kind | How its gate works | Gated? |
|---|---|---|
| **Seasonal** - name matches "Spring Foraging", "Fall Crops" etc. | all ingredients due at its named season | always |
| **Percentage** - requires FEWER than it shows (X &lt; Y), or has a curated quota | a 4-entry cumulative ramp, [Spring, Summer, Fall, Winter] | always |
| **PerItem** - requires everything it shows (X &gt;= Y) and has no quota | each ingredient due at its own pin, from the item pin table | **only if its items are pinned** |

So a bundle falls through the cracks when BOTH are true: it requires everything it shows, AND none
of its ingredients appear in `GameplayConfig.DefaultItemSeasonPins`. PerItem gating is entirely
driven by that table, so with no pins there is literally nothing for the gate to check, and
`DemandAtSeason` returns 0 for every season.

The pin table covers exactly twelve bundles, listed in its own comments: Construction,
Blacksmith's, Geologist's, River Fish, Lake Fish, Ocean Fish, Night Fishing, Specialty Fish, Dye,
Field Research, Fodder, Enchanter's. Anything else that lands on PerItem is ungated.

Note a Percentage bundle with X &lt; Y and no curated quota is fine: `BundleClassifier`'s
`DerivedDefaultQuota` invents a ramp for it. That is why Book, Tapper's, Weatherman's and
Recycler's are gated despite having no entry in the quota table. The hole is specifically X &gt;= Y.

## 3. Read these first

1. `src/TheLongestYear.Core/BundleClassifier.cs` - the three-kind decision, and `DerivedDefaultQuota`.
2. `src/TheLongestYear.Core/BundleRequirement.cs` - `IsSatisfiedAtSeasonEnd`, the actual gate.
3. `src/TheLongestYear.Core/GameplayConfig.cs` - `DefaultItemSeasonPins` and `DefaultBundleQuotas`,
   both curated with per-entry reasoning in comments. Read the comments; a lot of design argument
   is recorded there.
4. `docs/engine-bundle-catalogue.md` - every bundle the engine can produce. Regenerate with
   `tly_dumpbundles`.
5. `docs/superpowers/specs/2026-08-21-curated-quota-ramps-design.md` - the last pass over exactly
   this area, including why several bundles were deliberately left lean early.

## 4. The decision Jeff has to make first, before any code

**Closing this hole makes the game HARDER at Normal**, because bundles that currently apply no
checkpoint pressure would start applying some. That directly contradicts the invariant the whole
difficulty-modifier feature rests on: *Normal resolves to exactly today's shipping balance*, which
has a test asserting it. Adding gates is a balance change, not a bug fix, even though it started as
a bug report.

So brainstorm with Jeff before building. Ask him:

- Should the new gates apply at every difficulty, or should this be the thing that finally
  distinguishes them?
- Orchard is the mod's own bundle and needs 5 of the 5 saplings it shows. Should it instead show
  more than it needs (say 5 of 6), which fixes it automatically via the derived ramp and is a
  one-line change to `AuthoredBundles.cs`? Or should it keep 5-of-5 and gain a curated quota?
- For vanilla PerItem bundles outside the pin table, does he want the pin table extended (precise,
  laborious, needs him to rate items), or a derived fallback ramp (automatic, blunter)?

**Do not pick for him.** He is the designer and he rules on balance. Explain the mechanics in plain
terms before offering options.

## 5. Likely shape of the fix

Two candidates, not mutually exclusive:

- **A derived fallback for unpinned PerItem bundles.** When a bundle classifies as PerItem and NONE
  of its ingredients are pinned, give it a derived ramp the way `DerivedDefaultQuota` already does
  for unknown Percentage bundles. Automatic, covers vanilla and modded bundles alike, and needs no
  curation. Blunter, and it will change Normal.
- **Targeted data.** Give Orchard a quota entry, and pin the handful of vanilla items that leave
  their bundles bare. Precise and reviewable, but it only fixes what someone thought to list, and a
  remix draw or another bundle mod can reintroduce the hole.

A partial-pin case exists too and needs a ruling: a PerItem bundle where SOME ingredients are
pinned is gated only on those. Whether that is intended or a smaller version of the same hole is a
question for Jeff.

## 6. The hard constraint

**Never create an impossible gate.** A gate demanding more than the world can supply by that season
bricks the run: the player cannot pass, loses the year, and loses it again every loop. This has
already happened once. Purple Mushroom was pinned Winter while the mod's own Fall forage pool
offered it, so a Fall Foraging bundle that drew it could never be completed by its own Fall gate.
Fixed 2026-08-27 by repinning it to Fall.

`tly_gatecheck` exists precisely to catch this. **Run it before and after your change, on several
boards, at Normal and at Hard.** It prints, per bundle and season, what the gate demands against
what can actually exist by then, flags IMPOSSIBLE and FREE, and names the blocking ingredients.
Its known limit, stated in its own output: it checks CALENDAR feasibility only. An item that exists
in Spring but needs a keg, a fish pond, a bus repair or a tool upgrade counts as obtainable.

Relevant safety net already in the code: `GeneratedBundleSet.ClampRampForObtainability` pulls a
Percentage ramp down to what the pins say is obtainable. **Seasonal and PerItem bundles get no such
clamp**, which is how the Purple Mushroom case slipped through. If you add gates, consider whether
they need the same clamp.

## 7. Project state and workflow

- Repo: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`
- Branch: **`feat/difficulty-modifiers`**, 34 commits, not merged, **never pushed** and must not be.
- Tests: **1047 passing.** `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
- Build: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`
- `STATUS.md` and `TODO.md` carry the wider picture. The difficulty feature is unreleased and
  largely unplayed.

Workflow Jeff expects: brainstorm first, then a spec in `docs/superpowers/specs/`, then a plan in
`docs/superpowers/plans/`, then TDD with small single-purpose commits. Update `README.md` AND
`docs/nexus-description.bbcode` together and keep them content-identical, differing only in markup.
No em dashes. No `/sdcard/` paths. Do not bump `manifest.json`'s `Version` on this branch.

**Driving the game** (this cost real time; read it):

- Deploy with `pwsh -NoProfile -File tools/deploy.ps1`. A running game LOCKS the DLL, so a plain
  build fails to deploy.
- The live log is `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. The copy in the repo root
  is stale.
- Send console commands with `tools/send-smapi-command.ps1` (does not steal focus).
- **An unfocused Stardew is a PAUSED Stardew**, so queued commands never run. `tools/game.ps1
  -Focus` aborts when Windows refuses the foreground; a real synthesized mouse click works. There
  is a working `focus-by-click.ps1` in the session scratchpad; the technique is to find the window
  by title containing "Stardew Valley" AND class `SDL_app`, `SetCursorPos` over an empty part of
  it, then `mouse_event` down and up.
- **A reset RENAMES the save folder** (the name embeds `uniqueIDForThisGame`, which the reset
  re-seeds). Re-read the folder list after any reset before loading.
- Do not try to open .md files on Jeff's desktop; it silently fails.
