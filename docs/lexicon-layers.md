# The path lexicon and mod layers

## What the lexicon is

The lexicon holds the answers to "how, and from which week, can the player obtain this item?":
every item's routes, which maps and shops open when, and the availability weeks the board uses.
Working these out from the game's data is a cold build, and on 1.0.27 it cost about 6.6 seconds
on every load. From 1.1 the answers for the base game ship with the mod
(`assets/lexicon/vanilla.json`, one variant for a normal save and one for a save that owns the
`pierre_year2_seeds` upgrade). On load, TLY hashes the game data that feeds the build and checks
the hashes against the shipped answers. If they all match, and the file was written by the running
TLY version, the shipped answers are served and nothing is built. If some rows differ (a mod changed them, or a game update), the answers are
built cold once and written to a small disk cache (`data/lexicon/`, the three most recent
entries), so the next load of the same data reads the cache instead.

A layer is how a supported mod brings its own shipped answers. A mod's items cannot be placed from
game data alone when a framework spawns them in code TLY cannot read (a Custom Bush fruit, a
SpaceCore spawn group), so a layer holds hand-written routes and rulings
(`<UniqueID>.inputs.json`) and the answers that result, stored as a difference from vanilla's
(`<UniqueID>.answers.json`). Two mods together can change each other's answers (one adds a seed
to the other's shop), so each pair of supported mods installed together needs its own file
(`<A>+<B>.answers.json`), even if it is empty. All of these live in `assets/lexicon/layers/`.
A mod with an inputs file but no applied answers file (none written yet, or dropped for a version mismatch)
is "not mapped yet": nothing is served as shipped, so the answers are computed with its hand-written routes.

## Where an answer comes from

The resolver takes the first of these that applies:

1. **Shipped.** Vanilla's answers with every active layer's delta laid over them, when the live
   data rows all match the hashes stored with those answers and the answers are for this version:
   `vanilla.json`'s `ModVersion` must be the running TLY version (the manifest `Version`), and a
   layer's answers are laid on only when the installed mod is the version they were checked
   against (see the layer WARN below).
2. **Cache.** An entry in `data/lexicon/` made earlier from the same game data and the same layer
   files (the layer file text is part of the cache key, so editing a layer never serves a stale entry).
3. **Computed.** A cold build, then written to the cache.

Read the SMAPI log:

- `Lexicon: Shipped (a1b2c3d4), 5321 items, 42 ms.` The origin, the first 8 characters of the key,
  the routed item count and the time. A `Computed` line ends with the reason, for example
  `— 12 data rows differ from the shipped answers (Custom, FlashShifter)` or
  `— variant 'pierre_year2_seeds' not in the shipped answers` or
  `— shipped answers are for TLY 1.1.0, running 1.1.1` (the file was not regenerated after a version
  bump; see "Every release" below).
- `Lexicon layers: vanilla, <id>, ...; mods not mapped yet (A); pairs not mapped yet (A+B), so these answers are computed.`
  Logged at the first save load of the session (layers are read once per session), not at start-up, and only when layers are active.
- WARN `Lexicon layer <id>: checked against <version>, installed <version>; its generated answers
  are not used, so that mod's content is computed; its hand-written routes and rulings are still in
  use. Re-check the layer`. The mod was updated since the layer was mapped. Generated answers are
  version-locked: `<id>.answers.json` and every `A+B.answers.json` naming that mod are dropped, so
  the answers are computed (a dropped pair file also leaves the pair unmapped). The hand-written
  `<id>.inputs.json` (extra routes, year and week rulings) still applies.
- WARN `Lexicon layer <id>: hand-written inputs checked against <version>, installed <version>; they
  are still in use. Re-check the layer`. The answers files match the installed mod but the inputs
  file's `CheckedVersion` does not: the answers still apply; bring the inputs file's version up to date.
- Trace `Load timing: pools … ms, effort … ms, availability … ms, obtainability read … ms build … ms,
  year sort … ms.` (needs SMAPI's trace level.) The build and year sort figures are near zero when the
  answers were served.

On the development machine: 42 ms Shipped against 6,584 ms on 1.0.27; 643 ms Computed with
Stardew Valley Expanded installed (that mod set has no layer yet); 202 to 217 ms from the cache.

## Adding a supported mod

1. **A clean game.** Install only vanilla, the mod and its frameworks. Disable Cart Catalog and any
   other mod that adds items or shops. Turn on the debug bridge (`EnableDebugCommandBridge` in
   `config.json`; see `HEADLESS_DRIVING.md`).
2. **See what is missing.** Start a new farm (`tly_newgame standard skipintro`) and run
   `tly_lexicon gaps`. It writes `lexicon-gaps.md` in the mod folder: items with no route, unreached
   maps and unresolved sources, grouped by owner. Read the owner section for your mod. The owner
   name is a heuristic (a prefix of the item id), so an item can land under the wrong owner or under
   "unattributed"; check anything odd by hand.
3. **Write the inputs file.** Create `assets/lexicon/layers/<UniqueID>.inputs.json`, using the
   mod's manifest `UniqueID`:

   ```json
   {
     "UniqueId": "Author.ExampleCrops",
     "CheckedVersion": "1.6.3",
     "Notes": "Custom Bush fruit is not in Data/Objects spawn rules; mapped by hand.",
     "ExtraSources": {
       "(O)Author.ExampleCrops_Olive": [
         {
           "Kind": "Crop",
           "Lands": "=29x29,+0x83",
           "Reliability": "Dependable",
           "Conditions": { "Requires": ["bush:Author.ExampleCrops_OliveBush"] },
           "Detail": "Custom Bush fruit, bush bears from week 5"
         }
       ]
     },
     "YearRulings": { "(O)Author.ExampleCrops_Pear": "YearTwo" },
     "WeekRulings": { "(O)Author.ExampleCrops_Nutmeg": 9 }
   }
   ```

   - **`ExtraSources`** adds routes the game data does not describe, by qualified item id. Each entry
     is an `ObtainSource`: `Kind` (`Forage`, `Crop`, `FruitTree`, `Shop`, `Machine`, `Fish`, ...; the
     `SourceKind` list), `Lands`, `Reliability` (`Dependable` for a spawn or shop row, `Chance` for
     luck), `Conditions` (write it on every hand-written source; if missing it reads as no conditions. `Requires` is not informational: `mines:floor N` and `location:SkullCave` add digging days (`MineDepth.WithTravel`) to a route that has no `Inputs`. `YearTwo`, `GingerIsland`, `RainOnly`,
     `FewDays`, `Unresolved`, `OwnedOnly`, `Skill`/`SkillLevel` change how the route counts), and a
     short `Detail` for the debug output. `Kind`, `Lands` and `Detail` are required: a source missing any of them makes the whole file skipped with a WARN. `Setup` (a list of `{ "Name", "Days" }`) and `Inputs` are optional (empty when missing).
   - **`Lands`** is the day table over start days 1 to 112: for each day the player could start the
     route, the day the item lands (0 = never). It is written as comma-separated runs that must cover
     exactly 112 days. `=Lxn` is a plateau: `n` start days that all land on day `L`. `+kxn` is a
     shift: `n` start days that each land `k` days after the start (`+0x112` is "available every day
     of the year"; `+0x83` is a stretch of 83 such days). So `"=29x29,+0x83"` means: starting on
     any of the first 29 days, the item lands on day 29; from day 29 on, it is there the same day. The
     example above is a bush that bears from day 29 (week 5); adjust to the mod's real timing.
   - **`YearRulings`** decide the Traveling Cart's year 2 slot: `YearOne`, `YearTwo` or `NeverSell`.
   - **`WeekRulings`** pin an item's availability week (1 to 16). A ruling can only move an item LATER than TLY's own derived week, or place an item nothing placed; an earlier one is refused and logged `week override to N REJECTED, earlier than derived floor week W`. There are no map-week or pond-chance fields in an inputs file: a map only a script enters stays unknown, so date its items with `WeekRulings`. The config's
     `AvailabilityWeekOverrides` win over a layer's ruling per id; two layers that rule one id
     differently produce a WARN naming both.
   - **`CheckedVersion`** is the mod version you mapped against. A different installed version gives
     the layer WARN above. The answers files record their own checked versions
     (`CheckedVersions`, `"A@1.2.0"` or `"A@1.2.0+B@3.0.0"`), written by `tly_lexicon write`; those
     decide whether the answers are used.
4. **Write the answers.** Layers are read once per session, at the first save load, so a newly written layer file takes effect only after a game restart. Load the farm with the inputs file in place (restart the game so it is
   read), then run `tly_lexicon write <UniqueID>`. This cold-builds with the layer's extra sources
   and writes `<UniqueID>.answers.json`: the part that differs from vanilla, for this save's variant
   (`base`, or `pierre_year2_seeds` when the save owns that upgrade). It writes into the DEPLOYED
   `Mods\TheLongestYearssets\lexicon\layers\` folder, not the repo: copy the file back into
   `src/TheLongestYear/assets/lexicon/layers/` and delete it from the Mods folder afterwards, or it applies to your own games. Then repeat for the other
   variant: on a farm that owns Pierre's year 2 line (`tly_addjp 10000` first, since the upgrade costs JP, then `tly_buyupgrade pierre_year2_seeds`, then
   `debug sleep` so it saves, then reload), run `tly_lexicon write <UniqueID>` again; the file keeps
   both. A layer that maps only `base` leaves Pierre-owning saves computing live, because a variant
   a layer does not name is dropped from the shipped set.
5. **Check it.** Restart the game (layers are read once per session), then load the farm. `tly_lexicon status` must say `origin: shipped`, and
   `tly_lexicon diff` must report 0 differences (it cold-builds and compares to what was served; on a
   computed load it is trivially empty, so check on a shipped one).
6. **Every release.** `vanilla.json` records the TLY version that wrote it and is served only to
   that version, so every release regenerates it: bump `Version` in `manifest.json` first, then run
   `tly_lexicon write vanilla` on a base farm and again on a Pierre-upgrade farm, then build the
   release. Skip it and the file is ignored in game (every load computes).
   `ShippedModVersionIsTheManifestVersion` fails until the file's `ModVersion` matches the manifest.
   A supported mod's layer is regenerated the same way whenever that mod updates.
7. **Keep it current.** After any change to a TLY rule that feeds the build, regenerate `vanilla.json`
   (the tests in `VanillaLexiconCoverageTests` that rebuild it offline from the inputs fixtures fail
   until you do: the obtainability and year-sort routes of both variants and every availability
   answer) and every layer's `.answers.json`. After a change to what the game reads, dump the
   fixtures again with `tly_lexicon dumpinputs <obtainability file> <availability file>` on an
   unmodded game (`vanilla_obtainability_inputs.json` and `vanilla_availability_inputs.json` from a
   fresh farm, `vanilla_obtainability_inputs_pierre.json` from one that owns Pierre's year 2 line)
   and copy them to `tests/TheLongestYear.Tests/Fixtures/`. After a mod update, review the layer's WARN, re-run `gaps`, adjust the inputs
   file and its `CheckedVersion`, and write the answers again.
8. **Interactions.** With both mods installed (and only those), run `tly_lexicon write <A>+<B>` (in
   either order; ids are sorted). An empty delta still counts as mapped: write it so the pair is
   known to change nothing. Without the pair's file, the pair is not served as shipped; its answers
   are computed live and the start-up line lists it under "pairs not mapped yet".

## Known limits

- A layer cannot express a mod REMOVING a vanilla row. A mod set that does so computes live: correct,
  just not shipped.
- A delta cannot remove an `Unresolved`, map, shop or availability entry; a later delta can only add one or overwrite an existing key.
- Owner names in `gaps` and in the INFO line are a prefix heuristic, not a record of which mod
  changed a row.
- `vanilla.json` costs about 250 ms to read on a session's first load.
- The availability answers also key on the save's wild-tree set, so some loads rebuild availability
  (about 150 to 500 ms), which is what every load cost before 1.1.

## Next: Cornucopia More Crops

A local working note (not in this repository) lists what TLY could not place with Cornucopia More
Crops. Those are the likely `ExtraSources` entries:

- Custom Bush fruit: Juniper Berries, Raspberry, Olive, White Grape, Bay Leaves, Black Currants,
  Elderberries, Gooseberry, Shiitake, Chicken of the Woods, Vanilla (and Olive Oil and Sparkling Wine
  after them).
- SpaceCore spawn groups: Peppercorn.
- Seeds sold only at Sandy's, whose seasons end before the desert opens: Agave, Aloe, Bamboo, Blue
  Agave, Lemongrass, Sugarcane.
- Year 2 lines on Pierre: the rare seeds and their crops (Zucchini, Asparagus, Barley and others), and
  the Nectarine, Pear and Persimmon saplings. These are correct by the data, so they may need
  `YearRulings` rather than a route.

Stardew Valley Expanded comes after it. The `tly_lexicon gaps` output on a game with SVE 1.15.11
gave 404 gaps (205 items with no route, 154 unreached maps, 45 unresolved sources): 221 attributed
to vanilla, 110 under Custom, 53 under FlashShifter and 20 unattributed.
