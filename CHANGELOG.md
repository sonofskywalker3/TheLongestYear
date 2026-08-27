# Changelog

All notable changes to **The Longest Year** are documented here. This project
aims to follow [Semantic Versioning](https://semver.org/).

## 0.16.0 - 2026-08-27

1113 tests.

### Added

- **Ten independent difficulty dials**, in the mod's settings menu (GMCM) under **Difficulty**. There is no overall difficulty setting: turn up only what you want turned up. Each dial has four steps (Easy / Normal / Hard / Extreme) and every one starts on Normal, which is the balance the mod already shipped with. Changing nothing changes nothing. A change applies at your next loop, not straight away. Full list and what each one does: see the README or the mod page.
- **`tly_itemmodel <item or bundle>` console command.** Prints what the mod believes about an item: the earliest season it can exist, how much work it scores as, and the reasoning behind both. Pass a bundle name to see every ingredient and its due season. Read-only.
- **`tly_gatecheck` now explains itself.** When it names an ingredient blocking a season gate, it prints why that ingredient is dated the way it is.

### Changed

- **Every bundle now applies pressure at the season checkpoints.** Bundles that require *all* of the items they show used to take their per-item due dates from a hand-written table of 40 items. An ingredient outside that table had no due date at all, so a bundle whose ingredients were all absent could be ignored until the Winter check. Because the mod re-rolls the six fish bundles from a 52-item pool and the two metals bundles from an 11-item pool, most re-rolled boards had at least one bundle applying no pressure for three seasons, and roughly a quarter of fish boards and a third of metals boards were entirely free.

  The mod now derives this itself from the game's own data. For every fish and metal it works out the earliest season the item can exist and an effort score (fishing level, weather, time window, mine depth, smelting), then spreads a bundle's items across the four checkpoints easiest first, weighting harder items later. Because it is read from live game data rather than a list, it covers remixed boards and re-rolls.

- **This makes the year harder, at every difficulty including Normal.** It is a deliberate balance change, not a bug fix, and it is the reason to give this release a fresh look even if the previous one felt right. Measured across three boards and two difficulty configurations before release: no season gate is unsatisfiable, and no bundle is free all year.

- **A due date can never precede the season an item can first exist in.** The deadline is clamped to the item's earliest possible season, so an impossible gate cannot be expressed at all rather than being caught by review. A season pin that would demand an item earlier than it can exist is now rejected and logged instead of honoured.

### Known limits

- Bundles whose ingredients come from crops, forage, monster drops, artisan goods, cooking, artifacts, books, saplings, geode minerals or tapper goods still only come due in Winter. Those domains are not modelled yet, and the safe default is a late date rather than a guessed one. They are next.

## 0.15.0 - 2026-08-26

865 tests.

### Fixed

- **The Help Wanted board starts empty again.** Spring 1 was inheriting the quest from the day your run ended, gold reward included. Worse, it was rolled against your OLD progress, so it could ask for a fish or a monster the fresh farmer had no way to reach yet. A new year now opens with a clean board, exactly as a new save does.
- **The Saloon's Dish of the Day resets with the year.** Gus kept serving whatever he had cooked on the day the loop ended. A real first day has no dish yet, so neither does a rewound one; the new run's first dish arrives on day 2.
- **The Traveling Cart's year-one guarantee survives a rewind.** On saves created with the "year one completable" option, the first rewind used to switch that guarantee off permanently. It is re-rolled for the new run instead.

### Added

- **`tly_netstate` debug command.** Prints every world-state field the loop reset is responsible for, so a reset can be checked field by field instead of by eye. Read-only.

### Internal

- These three came out of a one-time audit of every piece of world state the rewind was leaving behind, rather than being found one player report at a time. That whole class of leak is closed now. Ruling table: `docs/superpowers/2026-08-26-networldstate-field-rulings.md`.
- Removed three no-op date assignments in the reset path, and synced the world state from the game's own statics after the calendar rewind so a night event cannot restore the pre-reset run seed.

## 0.14.2 - 2026-08-26

865 tests.

### Fixed

- **Shop Discount now changes the price on the shelf.** It used to take the money off at the till, so shops still showed full price - and worse, the game checks whether you can afford the FULL price before it charges you, so the discount never extended your buying power. Tool upgrades are deliberately not discounted, and neither are buildings or animals (they never were); the upgrade text says so now.
- **Festivals work again in later loops.** 0.14.1's once-per-day rule leaked across a rewind: because the calendar goes back to Spring 1, loop 2's Egg Festival landed on the same day number and got refused. A rewind means the festival has not happened yet, so every loop gets its festivals back. Once per day still holds inside a loop.

### Added

- **New Features section in the mod settings.** Turn individual parts of the mod off or tune them without editing config.json: festival time-flow, the one-minigame-per-day rule, theme re-rolls, donating tools and rings, the weekly goal JP multiplier, and starting gold. Changes apply straight away.
- **A short "Is this a bug?" section on the mod page**, covering the things reported most often that are working as designed: the cave prompt that replaced Demetrius' cutscene, the one-item Traveling Cart, and the once-per-day festival rule.

## 0.14.1 - 2026-08-26

Two fixes from emmalution's stream. 853 tests.

### Fixed

- **Festival minigames run once a day.** Because a TLY festival does not end your day, you could walk out of the festival and back in and the whole thing would start over, host and all. The Egg Hunt could be run three times in one afternoon, the Luau soup tasting the same way. Each festival's main event now happens once per day; the stalls, the shop and everyone at the festival still work on a repeat visit.
- **Weekly goals never ask for more than a bundle can take.** Bundles that only need some of their listed items could hand you three goals in a bundle that needs two. With 0.14.0 requiring a real donation per goal, that third one was impossible and the week could not be completed. A bundle is now only ever asked for what it can still accept.

## 0.14.0 - 2026-08-26

Fix release for the 0.12.17 shrine regression, plus two fixes from player reports. 839 tests.

### Fixed

- **The Junimo Shrine (JP perk) screen never opened on a Fail night** (Nexus 1123181, SincerelyZoey +
  SilencedLink). Regression from 0.12.17: the keep/reshuffle hold prompt put a question in front of the
  shrine, and the shrine was opened from inside that question’s answer callback. Vanilla still has the
  DialogueBox up at that moment, so the launcher refused to open over it and the night fell through to
  the reset with no shop shown (and no JP spent). The shrine open is now deferred a tick and drained by
  the day-28 watchdog, the same fix the hold re-ask and the pity offer already use. A shrine that still
  cannot open now logs a warning instead of failing silently. Banked JP was never lost.
- **Weekly theme goals could tick without you donating anything** (@ggrace67, via emmalution's
  stream). Vanilla marks every ingredient slot in a bundle as filled the moment that bundle
  completes, so in a bundle that only needs some of its listed items, finishing it with the others
  ticked your goal, paid the weekly JP and lifted the drawback for free. A goal now needs an actual
  deposit into that slot. Goals you have already finished this week are kept.
- **No way to get another pet after letting one go** (Nexus post, rose1729). Declining Keep Pet left
  you with no pet and no offer of one, ever: the rewind re-marks the pet cutscene as seen and puts
  the year back to 1, which is what vanilla's adoption option keys off. A loop that ends with no pet
  on the farm now re-opens the Adopt option at Marnie's counter, at 0 hearts like the animals.


## 0.13.0 - 2026-08-25

Season pity (opt-in Junimo offer after five fails at one season) plus the 2026-08-25 sweep fixes.
830 tests; live-smoked on a throwaway save (TODO.md tables). Engine (TLY Custom) boards only.

### Added
- **Season pity** (spec `docs/superpowers/specs/2026-08-25-season-pity-design.md`). Fails are counted per
  season gate. The first 5 fails at a season are standard difficulty; from the 6th, keeping the board
  lowers that season's quota by 10% per extra fail (floor 50%), and reshuffling leaves the hardest
  eligible items out of the roll (2 per extra fail; quality asks go first). Passing a season drops its
  count back to 5. Season Goals title shows "eased Nx". Config `PityEnabled`, `PityThreshold`,
  `PityQuotaStep`, `PityQuotaFloor`, `PityTrimPerStep` (GMCM section "Season pity"). Debug `tly_pity`.
  TLY Custom boards only.

- **The easing is an offer, not automatic.** After the keep/reshuffle question on a Fail night where
  the season has been failed more than the threshold, the Junimos ask whether to use their power to
  make the town's requests easier. Yes applies the easing for the path you chose (kept board: lower
  quota; reshuffled board: hardest items left out) and costs JP on the same curve as the hold (first
  free, then `PityCosts` 50/100/200/300, reset by declining). No means a standard board. Debug
  `tly_pity accept|decline`.

### Changed
- The Traveling Cart merchant no longer mentions the Junimos (only the farmer and the Wizard can see them).

### Fixed
- **Quality asks only on items that can carry quality** (Nexus 1122358 follow-ups: gold Fiber, gold
  River Jelly, silver Tea Leaves). The engine now derives which items the game itself gives quality
  to (crop harvests, rod-caught fish that are not jellies, spawned forage in a forage category) and
  never asks for silver/gold on anything else, including curated forage additions such as Tea Leaves
  and bush drops. `tly_genbundles` lists every quality ask on the board.
- **Keep Pet keeps every pet** (Nexus 1122901). A second pet from Marnie used to be the only one that
  survived a reset; all pets are snapshotted and restored (old single-pet saves migrate at their next reset).
- **No bundle or weekly goal asks for Pierre's year-2 crops until you can grow them.** Garlic and
  Artichoke stay out of every pool until you own Pierre's Special Order; Red Cabbage until you own
  that or the Cultivation: Red Cabbage upgrade. On run 1 the only source of those seeds is a shrine
  upgrade, so a Garlic weekly goal was unwinnable by construction (Jeff, 2026-08-25 smoke).
- **Traveling Cart cap is per day, not per view** (Nexus post, lexihope). Buying an item used to pull
  the next item in the merchant's list into the freed slot; the day's selection is now remembered, so a
  purchase leaves a gap until tomorrow. The Cart Whisperer preview locks in the same selection, so what
  it shows is what the cart sells.

## 0.12.18 - 2026-08-24

### Fixed
- **Void Salmon removed from the bundle pools** (Nexus 1122358 follow-up). `WitchSwamp` joins the built-in excluded location markers and `(O)795` the built-in excluded ids: the Witch's Swamp is behind the Dark Talisman quest, which is post-CC, so the 0.12.16 "hard but fair" ruling was wrong. Existing saves get the change at their next reset.

## 0.12.17 - 2026-08-24

Keep-bundles hold (spec `docs/superpowers/specs/2026-08-24-keep-bundles-hold-design.md`). 750 tests;
live-smoked on a throwaway save. Applies to Engine (TLY Custom) boards only; Vanilla boards are unaffected.

### Added

- New: on a Fail night the Junimos ask whether to hold the town's wishes (keep the same bundle board for the next loop) or let time reshuffle them. The first hold is free; holding again in a row costs 50, 100, 200, then 300 JP (config `BundleHoldCosts`). Reshuffling resets the price.
- New: the day-1 Junimo speech says up front that impossible-looking asks are expected and can be held across a rewind.
- Season Goals title shows how many times the board has been held.

### Changed
- Text: removed all em dashes from in-game strings.
- Debug: `tly_hold keep|reshuffle|status`.

## 0.12.16 — 2026-08-24

All four bugs from the 2026-08-24 feedback sweep, root-caused and fixed same day (covers
0.12.12 – 0.12.16). Verified by unit tests (724) and an agent-driven live playtest on the
deployed build.

### Fixed
- **Engine bundles rolled Ginger Island / Qi-gated items** (Nexus 1122358; SincerelyZoey,
  IshoMoogoo, gazumbrado). Location markers could not catch them — crops derive from Data/Crops
  (no location field) and the metals/cooking/geode pools scan all of Data/Objects. Structural
  built-in exclusions (`ItemPoolBuilder.BuiltInExcludedItemIds`) now vet Qi Fruit, Pineapple,
  Taro Root/Tuber, Banana, Mango, Ginger, Magma Cap, Radioactive Ore/Bar, Cinder Shard, Dragon
  Tooth, Fossilized Skull, the five island dishes and Piña Colada; BugLand (Mutant Bug Lair —
  Dark Talisman is post-CC) joins the excluded-location markers, so Slimejack is out. Void
  Salmon stays (hard, not impossible — design ruling). Algae/Seaweed can no longer receive
  silver/gold quality asks (the game never gives them quality). (0.12.12, 0.12.16)
- **Config-override trap:** SMAPI's ReadConfig replaces serialized list defaults wholesale, so
  exclusions that lived only in tuning defaults were inert on any install with a saved
  config.json. All structural exclusions moved into code; the tuning lists are pure extension
  points; regression-tested against emptied lists. (0.12.16)
- **Weekly themes asked for out-of-season fish** (Nexus 1122423; spenderg, lexihope — Pike in a
  Spring theme). The CcItem catalog treated every fish as year-round; the new `SpawnSeasonMap`
  feeds it real fish/crab-pot spawn seasons from the engine pools. (0.12.13)
- **Advanced Options: selecting Remixed soft-locked the OK button** (Nexus 1122619;
  SincerelyZoey). The patch located vanilla's dropdown apply-callback positionally, but AGO
  header rows use the Default element style — off by one: the wrong callback was replaced,
  vanilla's 2-entry capture stayed live and threw on Remixed (index 2). The callback is now
  found by closure inspection (`DelegateClosures`), which also un-breaks the silently-eaten
  Year1Completable checkbox. (0.12.14)
- **Junimo Shrine bought every affordable tier of an upgrade in one press** (Nexus 1122027;
  spenderg). One gamepad A press dispatches both `receiveGamePadButton` and a synthesized
  `receiveLeftClick` in the same tick; after the first buy the next tier slid into the same row
  slot and was bought too. Same-tick purchase guard. (0.12.15)

## 0.12.11 — 2026-08-21

Release candidate of the 0.12 line after the beta: the bundle-source choice, the cult-upgrade
repricing (measured), the curated quota ramps, and the small follow-ups from the beta feedback.
Covers 0.12.1 – 0.12.11.

### Added
- **`BundleSource: Engine | Vanilla` (0.12.9–0.12.11).** The new-game Advanced Options
  "Community Center Bundles" dropdown offers **TLY Custom** (default), **Normal** and **Remixed**.
  The choice is stored per save: TLY Custom = the engine writes its own board every loop; Normal /
  Remixed = the game's own board, regenerated the same way on every reset (`Game1.bundleType` is
  persisted in the mod's meta — the root cause of Nexus bug 1108030 "remixed comes back vanilla").
  Vanilla mode reads-and-classifies the live board and re-classifies on DayStarted when another
  mod rewrites it (Challenging Community Center Bundles swaps bundle values each morning). GMCM
  dropdown; a config flip takes effect at the next reset. Diagnostics `tly_bundlesource`.
- **Pierre's Special Order** (`pierre_year2_seeds`, 10,000 JP): Pierre stocks Garlic / Red Cabbage /
  Artichoke seeds from year 1 (Data/Shops edit while owned). (0.12.7)
- `tly_jpbudget` diagnostics: the maximum JP one loop's board can pay out — "donate ASAP" and
  "strong player" models plus a ceiling — with a per-season breakdown and impossible-gate
  detection. Measured on five loops: strong player 8.0–9.5k JP, fixed awards 1,933
  (`docs/superpowers/notes/2026-08-21-jp-budget.md`). (0.12.5–0.12.6)
- Advanced Options screenshot on the mod page (khauser13). (docs)

### Changed
- **Cult repricing (0.12.7, user ruling).** `cult_red_cabbage` 750 → 5,000 JP; `cult_starfruit`
  removed (the desert needs no RNG); the 10,000-JP Pierre upgrade is the sure thing.
- **Curated quota ramps (0.12.8).** Twelve remix/authored pick-X-of-Y bundles whose derived
  schedule demanded a donation before any item could exist (or was plainly harsh/lax) now have
  hand-set ramps: Winter Star `[0,0,0,2]`, Forager's `[0,0,2,2]`, Gil's Trophies `[0,0,1,2]`,
  Brewer's / Preserver's / Home Cook's Feast / Artifact `[0,1,2,4]`, Mineral `[0,1,3,4]`,
  Fish Farmer's `[0,0,1,2]`, Four Seasons Sampler `[1,3,4,5]`, Rare Crops `[0,0,1,1]`, Garden
  `[1,2,4,4]`. Gil's Trophies draws from the 7 year-1-feasible trophies (Slime Charmer, Napalm,
  Knight's Helmet, Arcane Hat dropped).
- **`EnableNonObjectDonations` governs the next board only (0.12.4).** The weapon/hat donation
  patches stay live while the live board has (W)/(H) slots, and the engine-manifest check tries
  the opposite flag before falling back — the beta caveat is gone.
- Empty weekly theme: hub card "Themed donations completed", HUD "Themed donations completed -
  drawback lifted." (0.12.2–0.12.3, Bumblewyn).
- Version scheme: plain semver, no prerelease tags (0.12.1).

### Fixed
- The CcItem catalog only covered a bundle's first X ingredients — the remaining Y−X items had no
  rarity/season data (weekly-theme sampler treated them as Common + year-round). Every concrete
  ingredient is catalogued now. (0.12.9)
- Grape (Summer forage, Fall crop) read as Fall-only; crop and forage seasons are unioned. (0.12.9)
- Boards read from the game (Vanilla mode, pre-engine saves, bundle mods) get the same
  obtainability clamp on Percentage ramps the engine path had. (0.12.9)

## 0.12.0-beta.1 — 2026-08-21

Public beta of the 0.12.0 line. Consolidates the 0.11.61–0.11.111 dev line: the
owned-bundle engine (three plans), the economy/clarity pass, and the bugfix pass
on everything reported against 0.11.60.

### Added
- **Owned-bundle engine (0.11.69–0.11.100).** TLY writes its own Community
  Center board at run-create and every reset, seeded per loop, from the vanilla
  + remix pools. Picked bundles re-roll their slot contents from pools derived
  from the game's own data (season-valid crops/forage, habitat-matched fish,
  monster loot, metals, artisan goods) — SVE-proof by construction. Eleven
  authored bundles join the pools (Artifact, Mineral, Book, Tapper's, Four
  Seasons Sampler, Orchard, Preserver's, Home Cook's Feast, Weatherman's,
  Gil's Trophies, Recycler's). Weapon/hat donations (`EnableNonObjectDonations`
  kill-switch). Vault asks +25%. `ExcludedLocationMarkers` config for SVE/Island
  exclusives. `tly_genbundles` / `tly_classify` / `tly_trophytest` diagnostics.
- **Economy/clarity (0.11.61–0.11.68).** Season-checkpoint JP award
  (150/250/400), donation JP single-pay, `xp_mult` upgrade family (5 skills ×
  ×2–×5 + the ×10 "Junimo Insight" capstone), hub line for the season
  multiplier.
- `LimitTravelingCartStock` (config + GMCM) — turn off the one-item Traveling
  Cart cap. The cap is explained in-game on the first cart visit and on the
  mod page.

### Fixed (0.11.101–0.11.111, the 0.11.60 bug reports)

### Fixed
- **Community Center completion ceremony never played** (Joja stayed open, Pierre
  stayed closed on Wednesdays, the JojaMart lightning never struck). The mod
  suppressed event 191393 thinking it was the Spring-5 CC intro; 191393 is the
  ceremony. The intro (611439) is what's suppressed now. Affected saves recover
  on the next sunny day you enter Town.
- **Museum rewards only came once per profile** (Ancient Seeds + recipe, the
  artifact statues, Singing Stone, geodes). 1.6 tracks those on the farmer's
  `specialItems` lists, which the reset now clears.
- **Caroline's Tea Sapling event didn't replay after a reset** — the replayable-
  cutscene scan now recognises letter-delivered unlocks (`mail` / `mailToday`).
- **Cultivation upgrades never fired for Mixed Seeds** (and Summer Seeds grew Red
  Cabbage instead). The patch now sits on the actual Mixed Seeds path.
- **It never rained, Rain Totems did nothing, CJB said "the game forces sun".**
  The schedule is now written for *tomorrow* each morning so totems, CJB and
  console weather set later in the day stick; and the schedule itself has
  vanilla-like density (Spring/Fall 5 rain + 2 wind, Summer 3 rain + 2 storm,
  Winter 10 snow) instead of two wet days a season.
- **Junimo Stash lost anything put in on day 28** — the chest is banked right
  before the world rewinds, not only on save.
- **Kept coop/barn came back without its hay hopper** — kept buildings are now
  initialised the way construction does it.
- **Kept fishing rod lost its bait/tackle** (and kept tools their enchantments,
  watering cans their water).
- **A fail-night overnight event (owl/UFO sound, meteorite, fairy…) could swallow
  the Junimo scene and skip the rewind entirely**, leaving you on Summer 1. Fail
  nights now skip the overnight event, and the scene re-arms if anything
  replaces it.

### Added
- `LimitTravelingCartStock` (config + GMCM) — turn off the one-item Traveling
  Cart cap if you'd rather have the full vanilla cart. The cap is now explained
  in-game the first time you visit the cart, and documented on the mod page.

## 0.11.60 — 2026-07-14

Localization release: the mod is now fully translatable. Consolidates the
0.11.45–0.11.60 dev line.

### Added
- **Full i18n support (0.11.46–0.11.60).** Every player-visible string moved to
  `i18n/default.json` (SMAPI translation framework): the upgrade catalog
  (hand-authored rows keyed by id, generated rows via token templates),
  themes/modifiers/category labels, all seven self-drawn menus, weekly/stash/
  shrine quest text (including the composed objective checklist), HUD messages
  and question dialogues (with explicit plural variants), GMCM options (live
  language switch), the onboarding mail, furniture display names (re-injected
  on locale change), the Day-1 intro speak lines, and the Day-28 cutscene.
  English output is byte-identical to 0.11.44. Guard tests fail the build on
  missing/orphaned keys or broken `{{tokens}}`. `docs/TRANSLATING.md` documents
  the translator workflow — a translation is now a single JSON file.

### Fixed
- **World-state keep/wipe audit (0.11.45).** A one-time audit of every
  world-level (`netWorldState`) field the loop reset touches; closed the
  remaining "survives the reset" leak class with an explicit keep/wipe ruling
  per field.

## 0.11.44 — 2026-07-13

The big fix release: weekly goals redesigned around real bundle slots, remixed
bundles fully supported, the loop reset made airtight, and new upgrades.
Consolidates the 0.11.1–0.11.44 dev line. Changes since 0.11.0:

### Fixed
- **Weekly goals redesigned: slot-based checklists (0.11.12–0.11.23).** Each goal
  now names a specific still-open bundle slot (item, stack, quality — e.g.
  "Parsnip x5 (gold) — Quality Crops") and ticks only when that exact slot
  completes in live CC state. Kills three reported bugs at once: a single item
  could clear a x5-stack goal; themes could demand items with no matching open
  slot (structurally impossible weeks); goals could ask for items already
  donated. Fewer open slots → shorter checklist; zero → no quest that week and
  the drawback auto-lifts. The 1.5× banking bonus is slot-strict. Mid-week saves
  migrate with a one-time goal re-roll.
- **Remixed bundles all count (0.11.11).** Bundles matching no classification
  rule were silently dropped from season checkpoints and weekly themes — the
  gate shrank on the RECOMMENDED remixed config, and one report won a loop with
  a bundle still open. Unknown pick-X-of-Y bundles now classify with a derived
  cumulative quota ramp (custom-bundle mods included); nothing is skipped.
- **Reset-leak audit — the loop reset is now airtight (0.11.24–0.11.28,
  0.11.37–0.11.40).** Museum donations and lost library books, worn
  boots/rings/trinkets (and the trinket slot itself), monster-slayer kill
  progress, consumed mine milestone chests, power books / mastery / prize
  tickets, and max health/stamina all rewind with the year. Run-scoped stats
  are now wiped by default with an explicit keep-list, so future game versions
  can't silently leak progression across loops.
- **Your clothes survive the loop (0.11.41).** Hat, shirt, and pants stay worn
  through a reset — they carry no stats, and the wipe left farmers in their
  underwear with no way back to their look. Boots, rings, and trinkets still reset.
- **Kept buildings rebuild where you put them (0.11.42, 0.11.44).** Coop, barn,
  and silo keeps snapshot their position before the reset and rebuild exactly
  there (footprint cleared of regenerated debris), matching the stable's
  behavior — previously they landed on fixed tiles, one of which hid the silo
  behind the farmhouse.
- **Green rain is back in summer (0.11.26).** The weather scheduler was
  overriding vanilla's green-rain day; it's now reserved like a festival day,
  storm/rain minimums still hold, and forecasts (TV + Weather Sage) show it.
- **A reset no longer drags the old day's weather into Spring 1 (0.11.43).**
  Resetting mid-storm left lightning flashes, a storm HUD icon, and serialized
  storm state on the new Spring 1; the reset now re-resolves the day's weather
  through the game's own day-start path.
- **The farm cave asks again each loop (0.11.1).** Entering the cave offers the
  mushrooms / fruit bats / decide-later choice fresh whenever unchosen, instead
  of replaying the Demetrius scene (which only ran once, locking the first pick
  in forever).
- **Big-chest mod compatibility (0.11.35–0.11.36, 0.11.39).** Better Chests and
  Unlimited Storage no longer inflate the 4-slot Junimo Stash into a full chest
  grid; BC also no longer bulk-stashes into it or carries it away.
- **Horse fixes (0.11.21).** The horse no longer asks to be renamed every
  morning after a loop reset.
- **Theme picker polish (0.11.20, 0.11.22–0.11.23).** A pick can no longer be
  lost to a stale deferred offer; the quest tip moved below the checklist.

### Added
- **Keep Silo upgrade (0.11.27)** — 150 JP, Buildings; requires building a silo
  that run. Hay does not carry over.
- **Cart Whisperer I–V (0.11.5–0.11.10)** — Foresight chain; on Traveling Cart
  days the shrine planning view flags which of the cart's stock can feed a
  Community Center bundle (each tier previews more slots, gated on Cart Stall).
- Unattended-verification debug tooling (0.11.30–0.11.34): `tly_loadsave`,
  `tly_classify`, title-screen command bridge.

### Changed
- All reset paths route through one shared finalizer (0.11.2), so debug resets
  exercise the exact production path.

Fixes from this week's beta reports, plus a donation-JP rebalance. Consolidates
the 0.10.1–0.10.5 dev line. Changes since 0.10.0:

### Fixed
- **Theme-picker soft lock (0.10.4).** Quitting on the first day of a new season
  before completing it could reload into a weekly theme picker with no options and
  no way to close it. The save was written before the month rollover ran, and the
  load path's blind season sync erased the mismatch that triggers the rollover —
  last month's theme picks survived, accumulated past four, and eventually excluded
  every theme from the weekly offer. The load path now performs the month rollover
  itself (clearing month state and consuming the day-28 pre-pick), and as a backstop
  an empty offer skips the week instead of opening an unclosable menu — which also
  self-heals already-affected saves.
- **Dupe drops keep their quality (0.10.5).** The extra-item weekly bonuses
  (mine_drops_up / all_drops_up / tree + clump + monster paths) cloned drops by id
  only, always at base quality. The debris diff now carries `Item.Quality` /
  `Debris.itemQuality` through to the clone. Fish and hand-picked forage dupes
  already carried quality.
- **Vault money slots can no longer mint JP (0.10.2).** The donation observer's
  per-slot diff could treat a paid Vault bundle's gold amount as an item count
  (up to ~26,000 JP for the 25,000g vault) when the menu rebuilt mid-session.
  Money ingredients are now excluded from the per-item path; the Vault pays only
  its intended gold-scaled award.

### Changed
- **Donation JP rebalance: single-item slot awards (0.10.3).** A completed bundle
  slot awards the rarity JP of ONE item regardless of the slot's required stack —
  99 wood pays Common×1, not Common×99. The stack is an acquisition cost, not a JP
  multiplier; season scaling, weekly bonus items, and JP Boost apply unchanged.
  Bundle, room, and weekly-goal completion bonuses are now the dominant JP source.
- **Replayable-cutscene detection generalized (0.10.1).** Unlock-granting cutscenes
  are auto-detected from `Data/Events` instead of a hardcoded id list, so other
  mods' unlock scenes (e.g. Stardew Valley Expanded's) re-fire each loop the same
  way vanilla's do. Adds the `tly_dumpreplayable` debug audit command.

## 0.10.0 — 2026-06-09

A stability pass on the season-end gate and loop reset, plus fixes from beta reports.
Consolidates the 0.9.7–0.9.41 dev line. Changes since 0.9.6:

- **Season-end gate, part 1 (0.9.20).** Finishing every goal no longer occasionally
  resets you anyway — the item-donation ledger is reconciled from the Community
  Center's bundle state at day's end, so a missed deposit can't read as a failure.
- **Season-end gate, part 2 (0.9.37).** Failing the 28th no longer advances you to the
  next season. Completing the bus-repair Vault on day 28 queued the overnight bus
  `WorldChangeEvent`, which raced the loop reset; the rewind-doomed scene is now
  suppressed on a fail and the cutscene defers behind it on a pass.
- **Double theme pick on reset (0.9.25).** The reset presented the weekly theme picker
  twice and discarded the first pick — now persisted before the deferred reload.
- **Remix-aware Vault gate (0.9.26).** The bus-repair money bundles are renumbered under
  remixed bundles; indices + gold are now derived from live bundle data, so the gate
  can be satisfied. Season Goals also restyles the bus-repair line as a real list row
  (0.9.28–29).
- **Artisan goods keep value through the Junimo Stash (0.9.19).** Smoked/preserved fish
  and all flavored goods (wine, jelly, aged roe, honey, bait…) preserve identity +
  price across a reset.
- **Villagers stay out of the abandoned CC during a run (0.9.21).**
- **Mine elevator locks on reset (0.9.38).** Floors reached last loop are no longer
  accessible unless the keep-elevator upgrade was bought (cap-not-grant).
- **Weekly goals name the egg color (0.9.43).** A "Large Egg"/"Egg" goal shows
  "(Brown)" or "(White)" in the quest log — the two colors are distinct CC items, so
  the goal names which it wants instead of leaving the player to guess.
- **In-progress Clint tool upgrade no longer survives a reset (0.9.30)** as a free upgrade.
- **Removed the stale vanilla "Rat Problem" quest during a run (0.9.41).**
- **Week-1 special-weather guarantee (0.9.18).** Each season is guaranteed a special
  weather day in week 1, replacing vanilla's always-on day-3 rain.
- **Clearer Junimo Shrine wording** — the planning view states JP is spent on reset/win.

## 0.9.1–0.9.6 (earlier betas, shipped)

- **0.9.6 — SMAPI update notifications.** Added the Nexus update key to the manifest,
  so SMAPI now tells you in its console when a new version of The Longest Year is
  available. (Also wires up automatic Nexus uploads on each GitHub release — no
  player-facing change.)
- **0.9.5 — Fixed: loading a non-TLY save fired the intro cutscene.** The dormant
  gate from 0.9.3 bailed correctly, but the intro / day-28 cutscene drivers (and a
  warp tracker) are attached at startup with their own update loops and bypassed it,
  so the Lewis→Junimo intro still played on a save TLY didn't start. They now respect
  the per-save activation gate.
- **0.9.4 — Fixed: the Community Center bulletin board (Mixed room) did nothing.**
  Vanilla gates the bulletin board behind three completed bundles (unlike the other
  five rooms, which open immediately); TLY revealed the note but never patched that
  gate, so pressing it was a no-op. It now opens from day 1 like the rest.
- **0.9.3 — Safety: TLY stays fully dormant on saves it didn't start.** Loading a
  normal (non-TLY) save with the mod installed used to activate the full roguelite
  layer — including the day-28 world reset. Now only starting a NEW game begins a run;
  any other save is left completely untouched (no effects, HUD, or reset loop).
  Existing runs migrate automatically.
- **0.9.2 — Fixed: the weekly theme picker was lost when starting a new loop from
  the win screen.** The "Start a new loop" choice is a question dialogue; its answer
  callback ran the reset and tried to open the planning hub while that dialogue was
  still the active menu, so the open was refused — and the week was marked "offered"
  before the open was confirmed, so it never re-fired. The hub now marks the week
  presented only on a confirmed open and retries the deferred open each tick once the
  menu surface clears. Also hardens against the other "menu busy" cases.
- **0.9.1 — Win-screen copy** reworded to "You have restored the Community Center.
  The valley is saved!" (The jarring win → JP-shrine transition is deferred to the
  real 1.0 ending — see `TODO.md`.)

## 0.9.0 — 2026-06-01

First public beta. Feature-complete for v1 ("prove it's fun & stable on PC").
The focus for this beta is feedback on **difficulty, pricing, and pacing**.

### The loop
- Roguelite year-loop over the Community Center restoration: per-season donation
  minimums; falling short unwinds the year to Spring 1; completing the Center
  within a year breaks the loop.
- **Junimo Points** earned from donations (scaled by rarity and a per-season
  multiplier), banked across loops.
- **Junimo Shrine** JP shop, surfaced on every loop reset and on a win, with
  upgrades that carry strength forward: skill levels, tool tiers, recipes
  (Cookbook/Craftbook), buildings, backpack, starting gold, a kept pet, and more.
- **Weekly themes** — each week grants a paired bonus + liability; chosen at the
  weekly planning hub.
- **Season Goals tracker** above the CC fireplace; **Junimo Stash** chest and
  **Cookbook/Craftbook** carryover surfaces on the farm.
- Continue-after-victory: keep playing a won run or start a fresh loop.

### New-game intro
- A two-scene intro plays before you take control: Lewis on the farm porch, then
  a Junimo inside the Community Center, who frames the loop in the land-spirits'
  own terms (community, sharing the land's bounty, and what does — and doesn't —
  carry across a reset). Implemented as a single engine-played event that moves
  between locations, then opens the theme picker.
- The vanilla intro is skipped and its toggle hidden; the farm type is forced to
  Standard. Both are managed on the character-creation screen.

### Quality of life
- Season Goals menu auto-completes its intro quest on first open and sorts
  completed bundles to the bottom.
- The starter parsnip gift box is granted only on the first loop, not re-dropped
  on every reset.
- `forage_yield_up` grants its bonus on pickup (Gatherer-style), with no
  duplicate forage spawned overnight.

### Known limitations
- PC only; Standard farm only; new saves only; multiplayer untested.
- Intro cutscene and dialogue are a first pass.

### Debug commands (console)
`tly_addjp`, `tly_addmoney`, `tly_buyupgrade`, `tly_reset`, `tly_replayintro`,
`tly_openshop`, `tly_openhub`, `tly_set{board,cookbook,craftbook,stash}`, and
others. Intended for testing/setup, not normal play.
