# The Longest Year - Status

**Last updated:** 2026-08-29 (Standard-board year sim, 0.16.147)
**Branch:** `master`; 0.16.25 PUSHED (2026-08-27); 0.16.146 PUSHED (2026-08-29); **0.16.147 committed LOCALLY ONLY, not pushed, not released**
**Tests:** 1754 passing, 0 failing
**Build:** clean (mod assembly builds Release); 0.16.145 deployed to the game and desktop-driven (Jeff out, desktop authorised); 0.16.146 is docs only
**Last public release:** 0.16.17 (SVE smoke finding recorded in TODO.md "SVE board audit")

## 2026-08-29 (afternoon): sim standardP3 was NOT the Standard board (custom, seed loop 100); corrected

**CORRECTION (same day):** the run was labelled standardP3 but the sim's `tly_reset` built an
`engine manifest (loop 100, seed loop 100)` board. `tly_bundlesource Vanilla Default` only sets the
config in memory; the relaunch before the sim reloaded `config.json` (`BundleSource: Engine`) and a
reset re-stamps the source from config (`RunController.cs:584`). So this is a second CUSTOM-board
year (seed loop 100), not Standard. The numbers below are still valid for what they are. The real
Standard run is the next section; the deployed `config.json` now says `"BundleSource": "Normal"`
(backup `config.json.bak-before-Normal` in the scratchpad).
`bash tools/sim-year.sh minimal standardP3` on build 0.16.145, minimal player: donates only the gate
demand, a quarter per week. Transcript: `docs/superpowers/notes/2026-08-29-sim-standardP3.txt`.
No WARN or ERROR lines in the whole run. Earlier today the same board was also driven to the Winter
28 win by hand (`tly_playseason` at Winter 8, 106 slots).

**Every gate passed.** Ledger at each season end 26 / 48 / 71 / 97 slots (quarter 4 flipped 6 / 5 /
5 / 6; per-season plan 26 / 22 / 23 / 26). Compare custom P2 last night on seed loop 3: 22 / 50 / 79 / 100;
this board (seed loop 100) front-loads Spring and ends lighter. Audit: `no impossible gates. 30
tight, 0 never gated. 2 stretch lines, 4 without a hard item, 5 Spring tight.` Stretch: Engineer's
`[stretch: Iridium Ore Summer]` (the one the seed audit kept finding) and Field Research `[stretch:
Coconut Summer]`. No hard item: Exotic Foraging, Mineral, Four Seasons Sampler, Orchard. Spring
tight: Four Seasons Sampler, Quality Fish, River Fish, Spring Crops, Spring Foraging.

Askable goals by week (Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki): Spring 3/3/3/2/5/3/0/0, 3/4/4/0/5/0/0/1,
3/5/2/0/5/0/0/1, 2/4/0/0/5/0/0/0; Summer 4/3/3/3/5/4/1/2, 5/4/3/2/5/1/1/2, 2/5/2/2/5/1/1/2,
2/1/1/1/5/1/0/1; Fall 3/4/2/2/5/3/1/1, 4/5/2/0/5/0/1/1, 0/5/3/0/5/0/1/1, 0/4/0/0/4/0/1/0; Winter
4/2/2/2/5/2/1/1, 4/2/3/0/5/0/1/1, 2/3/3/0/5/0/1/1, 3/0/3/0/5/0/0/0. Same shape as custom: Mixed
always 5, Mining and Spelunking thin outside week 1 of a season (Summer is the exception, 3/2/2/1),
Artisan and Kitchen 0 to 2 all year. Fishing dies in Spring week 4 and Fall week 12; Foraging in
Fall weeks 11 and 12.

Judgement rows (7): Apple week 9 (Fodder), Prize Ticket week 2 and Mystery Box week 3 (Helper's),
Winter Root week 13 (Four Seasons Sampler), Snow Yam week 13 (Winter Foraging), Cave Carrot week 1
(Exotic Foraging), Tea Leaves week 4 (Spring Crops). **Unknown items (2), gate treats as Winter,
Jeff to rule:** Hay `(O)178` in Fodder; Treasure Appraisal Guide `(O)Book_Artifact` in Field Research.

## 2026-08-29: per-slot ledger mirrored from the CC board, 0.16.135 to 0.16.146

Jeff's #1 priority (TODO, 2026-08-28; found live on emmalution's stream). Spec
`docs/superpowers/specs/2026-08-29-per-slot-ledger-design.md`, plan
`docs/superpowers/plans/2026-08-29-per-slot-ledger.md`, ten tasks, one commit each, 1754 tests green.
Committed LOCALLY ONLY, not pushed, not released.

**What changed.** `RunState.DonatedSlots` (bundle index, ingredient index, id) replaces the flat
`DonatedItemIds` list, which stays on the class as a legacy field so old saves deserialize.
`BundleRequirement` carries `BundleIndex` and a positional `Slots` list (duplicates kept; category
slots skipped without renumbering), and `MissingForSeason(season, ledger)` is the one method the
gate, the Season Goals page and `tly_gateneeds` all read. `CreatePerItem` / `CreateSeasonal` take
`NumberOfSlots` from the slot count, so vanilla Construction (Wood, Wood, Stone, Hardwood) is 4 of
4, not 3 of 3 (a second defect the TODO write-up did not name). `ItemDonationSync.Reconcile` is a
whole-replace mirror of the board's per-slot state and runs on save load (the migration), before the
Season Goals page, before the day-end gate and inside `tly_playseason`. Debug donations
(`tly_donate`, `tly_testdonate`, `tly_playseason`) fill the vanilla slot first through
`CcSlotWriter`, because a ledger-only write would be wiped by the next mirror. Rulings: mirror, not a
second "ledger AND board" check (a page saying complete while the gate fails is the worst outcome);
one slot one goal (Construction's second Wood slot is its own weekly goal once the first is filled;
`SlotPoolBuilder` already did this, now pinned by a test).

**Live checks over the bridge, build 0.16.143, throwaway save `None_447665404` (rotated by the
reset), log lines quoted:**

- Load of last night's fully donated board: `Ledger mirrored from the CC board: 101 slot(s) filled.`
  (the migration path; the old id list was ignored).
- `tly_reset` to a fresh board: `Ledger mirrored from the CC board: 0 slot(s) filled.`, then
  `tly_gateneeds: Spring day 1: 18 bundle(s) still owed before Summer 1, 0 slot(s) filled on the board.`
  with one line per bundle (`Construction (PerItem, 0/4 filled): needs 2 before Summer 1: Clay, Stone`).
- `tly_donate (O)709` (Hardwood): `Donated '(O)709' into bundle 13 slot 4. Ledger 1 slot(s).`
  Tapper's left the owed list, Construction stayed `0/4`, `17 bundle(s) still owed`. A second
  `tly_donate (O)709`: `No open slot wants '(O)709'. Nothing donated.` The custom board never asks an
  item twice (0.16.30), so the shared-item case cannot be reproduced on it; it is unit-tested
  (`One_deposit_credits_one_bundle_not_every_bundle_listing_the_id`) and applies to the vanilla and
  remixed boards.
- `tly_runstate`: `slots filled=1`.
- `tly_playseason`: `21 slot(s) flipped, vault 1/1`, `Spring gate WOULD PASS. Ledger 22 slot(s).`;
  `tly_gateneeds`: `0 bundle(s) still owed before Summer 1, 22 slot(s) filled on the board.`;
  `tly_setday 28` and `debug sleep`: `Month cleared (Spring). Advancing.`

**Second pass, desktop authorised (Jeff out), build 0.16.145 (`tly_seasongoals` opens the page),
Standard vanilla board via `tly_bundlesource Vanilla Default` + `tly_reset`, save `None_447693385`
lineage. Everything the first pass could not reach, all from the log and PrintWindow screenshots:**

- Construction (Wood, Wood, Stone, Hardwood): `tly_donate (O)388` -> `bundle 17 slot 0`, `(O)390` ->
  `slot 2`, `(O)709` -> `slot 3`, `Ledger 3 slot(s)`; the page shows **Construction (Foraging) 3/4**;
  a second `tly_donate (O)388` -> `bundle 17 slot 1`, `Ledger 4 slot(s)`, page **4/4 checkpoint
  met**; a third Wood: `No open slot wants '(O)388'`.
- Shared item, Parsnip `(O)24` (Spring Crops and Quality Crops both list it): first donate ->
  `bundle 0 slot 0`, `tly_gateneeds` then shows `Spring Crops 1/4 needs 3` AND `Quality Crops 0/3
  needs 1: Parsnip, ...` (not credited there); second donate -> `bundle 3 slot 0`, Quality Crops
  satisfied; third refused. Page: **Spring Crops 1/4 needs 3** (Green Bean, Cauliflower, Potato
  left), **Quality Crops 1/3 checkpoint met**.
- The whole page, scrolled through (Bus Repair 0/1, Exotic Foraging 0/5, Animal 0/5, Artisan 0/6
  needs 2, Crab Pot 0/5, the fish bundles, Adventurer's, Geologist's needs 2, Chef's, Dye, Field
  Research): every row matches the `tly_gateneeds` line for that bundle.
- The Winter win on the Standard board: `debug season winter`, `tly_playseason` (`101 slot(s)
  flipped, vault 4/4`, and it logged `Construction (PerItem): donated Wood ((O)388) slot 1`, the
  doubled slot), `tly_gateneeds: Winter day 8: 0 bundle(s) still owed before the win, 106 slot(s)
  filled on the board`, `tly_setday 28`, `debug sleep`: `Day-28 cutscene: opening the Win Junimo
  scene`. The win needs every slot on the board, Construction's second Wood included.
- Gotcha found on the way (runbook updated): Escape does not close the Season Goals page (click its
  X); a page left open across `tly_reset` keeps the old run's rows and `tly_seasongoals` is refused
  with `Cannot open menu: another menu is already open`. The four identical "4/4" screenshots that
  looked like a counting bug were that stale instance.

## 2026-08-29: the obtainable board, 0.16.85 to 0.16.134

Spec `docs/superpowers/specs/2026-08-28-obtainable-board-design.md`, five plans
`docs/superpowers/plans/2026-08-28-obtainable-board-1-model.md` through `-5-sims.md`, commits
83c192b (0.16.85) to 1669975 (0.16.133) plus this review fix wave (0.16.134), every plan
reviewed, 1741 tests green.
Per-plan ledgers with every ruling: `.superpowers/sdd/2026-08-28-obtainable-board-*/progress.md`.
Committed LOCALLY ONLY, not pushed, not released.

**Plan 1, the two-week model (0.16.85 to 0.16.96).** Every item now carries two weeks: a pacing week
(the week a normal player realistically reaches it) and a hard week (the earliest week it is possible
at all). `ItemAvailabilityModel` is built with a `WeekMode` (Pacing, HardGates, HardAll) taken from
the difficulty step, and answers `Week` for gates and `GoalWeek` for goals from that mode, so a Hard
board gates on the hard week while Normal paces. Rule E's tiers became absolute effort bands instead
of per-board quartiles; `SeasonNeed` and `BonusItemSampler` arithmetic follow; goals no longer look
half a season ahead of the gate (they follow the gate exactly); goal ceilings are flat 5 in every
season. The hand-written `DefaultItemSeasonPins` table is retired down to three rows (Red Mushroom,
Sea Urchin, Woodskip). New rules: tapper goods from Data/WildTrees TapItems at Foraging 4 plus the
row's nights, fishing trash (167 to 172) at week 1, machine weeks from recipe price and friendship
(`UnlockWeeks`), book weeks from a table with the year-2 and drop-only books out of the pool.

**Plan 2, stretch gates and the hard-item rule (0.16.97 to 0.16.107).** The Spring foothold is gone.
In its place, `StretchRule`: a bundle that gains nothing new in a season reaches two weeks past that
season, swapping a stretch item in if it holds none, and the gate then demands that line. Stretch is
a pacing mechanism only, so `StretchRule.Applies` returns false on HardGates and HardAll (on Hard the
gates already demand by hard week). Separately, every rolled bundle of 4 or more slots must hold at
least one genuinely hard item (effort 6 and up); that rule keeps applying on Hard and Extreme and is
exempt only on Easy. Both surface in `tly_gatecheck` (`[stretch: item season]`, `[no hard item]`) and
on the weekly cards. `BundleRequirement` carries `StretchLines`, recomputed from the model whenever
requirements are rebuilt.

**Plan 3, full pools with no fixed lists (0.16.108 to 0.16.119).** Every TLY Custom bundle keeps its
name, room and pick count but rolls its slots from the full pool of its kind; the mixed-kind bundles
roll from a named recipe in the new `BundlePoolRecipes` table (ordered parts, each a source and a
count). `PoolDomainClassifier` returns `Recipe` for any non-money bundle it cannot place in a legacy
domain, and an Other-majority bundle rolls from its own vanilla ids only and audits `[no recipe]`
until Jeff names one. String-id vanilla items (Goby, the jellies, Broccoli, Moss, Mystery Box,
Book_*) weigh like vanilla, 3, since vanilla is now "any id without a dot". Pool additions at weight
1: Stonefish, Ice Pip, Lava Eel, the five legendaries, and the year-2 crops Garlic, Red Cabbage and
Artichoke (Easy still excludes the year-2 crops). A legendary drawn into a 4-of-4 fish bundle is
mandatory for that bundle, and the rewind now clears legendary catches so it can be caught again.

**Plan 4, the Garden Pot keep and two Boosts (0.16.120 to 0.16.125, plus the fix wave at 0.16.128).** A permanent Garden Pot recipe
keep at the Junimo Shrine, 750 JP, `Obtainability` category, granted back after the rewind like the
book keeps. Two in-loop Boosts, bought at the farm's planning shrine (`ShrinePreviewMenu` gains a
Boosts section): Year-Two Seeds, 75 JP, makes Mixed Seeds roll the season's year-2 crop at 5 percent
for the current week; Sneak Peek, 100 JP, makes the Queen of Sauce air the year-2 episode for the
season (it grants both the year-1 and the year-2 recipe, so no year-1 recipe is lost). Year-2 crops
carry hard weeks 2 / 6 / 10 and pacing weeks Garlic 4, Red Cabbage 7, Artichoke 11.

**Plan 5, diagnostics and sims (0.16.126 to 0.16.133, interleaved with plan 4's fix wave at 0.16.128).** `tly_playseason quarter <k>` donates the
season's gate share a quarter a week, round-robin across bundles (one open demanded slot per bundle
per pass) before the prefix cut; `tly_reset <seedLoop>` and `tly_genbundles <seedLoop>
[custom|standard|remixed]` pin the board seed and roll vanilla boards through the same audit;
`tools/sim-year.sh <mode> <label> [seedLoop]` prints all 16 weeks, the gate audit, the judgement rows
and the unknown items. Runbook `docs/HEADLESS_DRIVING.md`.

### Sims P2 (minimal player) and Q2 (goal-completing player), both on seed loop 3, build 0.16.132

Gate ledger per season, then the season verdict. Read the four quarter figures as the quarter's
CUMULATIVE POSITION IN THE SEASON'S DONATION PLAN, not as slots actually flipped: the sim's plan is
computed once from the season baseline and each quarter donates a prefix of it, so a quarter whose
prefix was already in the ledger flipped nothing while still reporting its position. Q2 Spring
quarter 3 is exactly that case: it reported 15 and flipped 0, because the goal deposits had already
covered those steps. 0.16.134 splits the two numbers in the log line (flipped, donated this season,
plan position of plan size) and lets a quarter reach past already-donated steps, so a later rerun
will not repeat this ambiguity.

| Season | P2 quarters | P2 verdict | Q2 quarters | Q2 verdict |
|---|---|---|---|---|
| Spring | 6 / 11 / 17 / 22 | WOULD PASS, ledger 22 | 5 / 10 / 15 / 20 | WOULD PASS, ledger 23 |
| Summer | 7 / 14 / 21 / 28 | WOULD PASS, ledger 50 | 6 / 12 / 18 / 24 | WOULD PASS, ledger 53 |
| Fall | 8 / 15 / 22 / 29 | WOULD PASS, ledger 79 | 6 / 11 / 16 / 21 | WOULD PASS, ledger 79 |
| Winter | 6 / 11 / 16 / 21 | WOULD PASS, ledger 100 | 4 / 8 / 12 / 16 | WOULD PASS, ledger 101 |

Askable goals by week, P2 (Foraging / Farming / Fishing / Mining / Mixed / Spelunking / Artisan / Kitchen):

| Week | Fo | Fa | Fi | Mi | Mx | Sp | Ar | Ki |
|---|---|---|---|---|---|---|---|---|
| Spring 1 | 2 | 2 | 4 | 0 | 5 | 1 | 0 | 1 |
| Spring 2 | 3 | 3 | 5 | 0 | 5 | 0 | 1 | 1 |
| Spring 3 | 1 | 4 | 4 | 0 | 5 | 0 | 1 | 1 |
| Spring 4 | 1 | 2 | 0 | 0 | 5 | 0 | 1 | 1 |
| Summer 5 | 4 | 3 | 3 | 2 | 5 | 2 | 2 | 0 |
| Summer 6 | 4 | 5 | 4 | 0 | 5 | 0 | 2 | 2 |
| Summer 7 | 3 | 5 | 0 | 0 | 5 | 0 | 3 | 1 |
| Summer 8 | 2 | 5 | 0 | 0 | 5 | 0 | 1 | 1 |
| Fall 9 | 3 | 3 | 3 | 2 | 5 | 2 | 2 | 2 |
| Fall 10 | 4 | 5 | 3 | 0 | 5 | 0 | 3 | 2 |
| Fall 11 | 3 | 5 | 2 | 0 | 5 | 0 | 3 | 2 |
| Fall 12 | 3 | 0 | 0 | 0 | 5 | 0 | 1 | 2 |
| Winter 13 | 3 | 3 | 2 | 2 | 5 | 1 | 2 | 2 |
| Winter 14 | 3 | 3 | 2 | 0 | 5 | 0 | 2 | 1 |
| Winter 15 | 1 | 4 | 2 | 0 | 5 | 0 | 2 | 1 |
| Winter 16 | 1 | 2 | 1 | 0 | 4 | 0 | 1 | 0 |

Askable goals by week, Q2 (same columns):

| Week | Fo | Fa | Fi | Mi | Mx | Sp | Ar | Ki |
|---|---|---|---|---|---|---|---|---|
| Spring 1 | 2 | 2 | 4 | 0 | 5 | 1 | 0 | 1 |
| Spring 2 | 3 | 1 | 5 | 0 | 5 | 0 | 1 | 1 |
| Spring 3 | 0 | 2 | 3 | 0 | 5 | 0 | 1 | 1 |
| Spring 4 | 0 | 2 | 0 | 0 | 4 | 0 | 1 | 1 |
| Summer 5 | 4 | 3 | 3 | 2 | 5 | 2 | 2 | 0 |
| Summer 6 | 4 | 5 | 2 | 0 | 5 | 0 | 2 | 2 |
| Summer 7 | 0 | 5 | 0 | 0 | 5 | 0 | 3 | 1 |
| Summer 8 | 0 | 2 | 0 | 0 | 2 | 0 | 0 | 0 |
| Fall 9 | 3 | 3 | 3 | 2 | 5 | 2 | 2 | 2 |
| Fall 10 | 3 | 2 | 3 | 0 | 5 | 0 | 1 | 2 |
| Fall 11 | 3 | 0 | 1 | 0 | 5 | 0 | 1 | 2 |
| Fall 12 | 0 | 0 | 0 | 0 | 2 | 0 | 1 | 1 |
| Winter 13 | 3 | 3 | 2 | 2 | 5 | 1 | 2 | 2 |
| Winter 14 | 3 | 2 | 2 | 0 | 5 | 0 | 1 | 0 |
| Winter 15 | 1 | 0 | 2 | 0 | 3 | 0 | 0 | 0 |
| Winter 16 | 1 | 0 | 1 | 0 | 2 | 0 | 0 | 0 |

Week 4 of a season is alive again in both runs (it was 0 across the board before the round-robin
fix). Mining is still thin: 2 askable in week 1 of Summer, Fall and Winter, 0 everywhere else, 0 all
Spring. Spelunking is 1 or 2 in the first week of a season and 0 otherwise. Both look like a
pool-size problem, not a donation-order one. Full sim outputs and board dumps are in the scratchpad
(`simP2.txt`, `simQ2.txt`, `board-P2.md`, `board-Q2.md`); the two dumps are byte-identical except the
`loop seed` header line, so the seed pin fixes the board but not the loop's own RNG seed.

Because of that, P2 and Q2 share the BOARD but not the theme offers: the weekly theme roll reads the
run seed, which the pin does not fix. So the askable differences between the two tables are not
attributable to the goal deposits alone; part of the gap is simply a different offer sequence. Any
conclusion of the form "goal deposits drain the pool by week 4" needs a run where both sims share the
run seed as well.

### Gate audit (identical in both runs)

`tly_gatecheck RESULT: no impossible gates. 26 tight (demands everything obtainable by then), 0
bundle(s) never gated.` and `tly_gatecheck RESULT: 1 stretch line(s), 5 without a hard item, 2 Spring
tight.` Unknown items: 0 (was 1, Crispy Bass, fixed by the Placeable filter in 0.16.132). Tag lines:
`[stretch: Battery Pack Summer]` on Construction; `[no hard item]` on Four Seasons Sampler, Tapper's,
Garden, Orchard and Weatherman's; `[spring tight]` on Garden and Forager's. No `[no recipe]` line on
either board. Vault gate unchanged: 1 money bundle by Spring 28, 2 by Summer, 3 by Fall, 4 by Winter
(2,500g / 5,000g / 10,000g / 25,000g), satisfied outright by owning `keep_bus_unlocked`. The audit
checks calendar feasibility only: an item that exists in Spring but needs a keg, a fish pond or a
tool upgrade still counts as obtainable there.

### Judgement rows (9, unchanged by the fixes)

Rows placed by Jeff's own ruling rather than a game-data fact. They gate and appear on cards like any
other rule, but are worth a second look:

- Skeleton Mask `(H)8`, week 3, in Gil's Trophies
- Vampire Ring `(O)522`, week 4, in Gil's Trophies
- Burglar's Ring `(O)526`, week 6, in Gil's Trophies
- Insect Head `(W)13`, week 3, in Gil's Trophies
- Mystery Box `(O)MysteryBox`, week 3, in Helper's
- Prize Ticket `(O)PrizeTicket`, week 2, in Helper's
- Moss `(O)Moss`, week 1, in Tapper's
- Cave Carrot `(O)78`, week 1, in Exotic Foraging
- Snow Yam `(O)416`, week 13, in Exotic Foraging

### Vanilla boards through the same audit (`tly_genbundles <seed> standard|remixed`, seeds 0 to 9)

Standard is ONE board, not ten: it reads `Data/Bundles` verbatim and never touches the seed, so the
ten "seeds" all audited the same board and the determinism self-check compared it to itself (it was
vacuous there). 0.16.134 says so in the command output and skips that self-check for standard.

| Seeds | Sp/Su/Fa/Wi demanded | season share % | tight | impossible | stretch | no hard item | spring tight |
|---|---|---|---|---|---|---|---|
| 0 to 9, one board (the seed is ignored) | 25/52/83/105 | 9/20/31/40 | 32 | 0 | 0 | 7 | 2 |

Remixed (bundle contents randomized per seed):

| Loop | Sp/Su/Fa/Wi demanded | season share % | tight | impossible | stretch | no hard item | spring tight |
|---|---|---|---|---|---|---|---|
| 0 | 22/46/74/93 | 9/20/31/40 | 30 | 0 | 0 | 7 | 2 |
| 1 | 22/46/75/95 | 9/19/32/40 | 31 | 0 | 0 | 9 | 2 |
| 2 | 20/42/68/89 | 9/19/31/41 | 33 | 0 | 1 | 6 | 2 |
| 3 | 23/48/75/96 | 10/20/31/40 | 31 | 0 | 0 | 7 | 2 |
| 4 | 19/44/71/93 | 8/19/31/41 | 33 | 0 | 1 | 7 | 2 |
| 5 | 21/46/72/92 | 9/20/31/40 | 29 | 0 | 1 | 6 | 2 |
| 6 | 23/44/71/89 | 10/19/31/39 | 30 | 0 | 1 | 5 | 2 |
| 7 | 21/45/73/93 | 9/19/31/40 | 28 | 0 | 0 | 6 | 2 |
| 8 | 25/46/73/91 | 11/20/31/39 | 28 | 0 | 0 | 6 | 2 |
| 9 | 24/47/72/93 | 10/20/31/39 | 32 | 0 | 1 | 6 | 2 |

Zero IMPOSSIBLE bundles in all 20 runs, which is 11 distinct boards: 1 standard (the same board ten
times) plus 10 remixed. Every remixed stretch line is the same one: Engineer's PerItem,
`[stretch: Iridium Ore Summer]` (seeds 2, 4, 5, 6, 9), which looks like a structural artifact of that
one vanilla recipe rather than anything the stretch rule does wrong. It is NOT the same line Custom
raises: Custom's single stretch line on this board is `[stretch: Battery Pack Summer]` on
Construction, a different bundle and a different item, so the two are not one shared cause.

Vanilla demand is heavier than Custom's 21/50/79/100 at every season, but the two numbers are not the
same measurement: 21/50/79/100 is the CUMULATIVE LEDGER the sim ended each season with (items actually
donated, including goal deposits and anything donated beyond the gate), while the vanilla rows count
what the GATE DEMANDS by each season's day 28. The honest comparison is demand against demand; the
ledger figure runs one or two items above the demand it satisfies. Vanilla is also tighter overall
(standard 32 tight, remixed 28 to 33, against Custom's 26); season shares match Custom's back-loaded
curve closely. No-hard-item counts run higher in vanilla (6 to 9) than Custom's 5. Summary: scratchpad `vanilla-boards-summary.md`.

### Open for Jeff: rule on these or look at them

Collected from the five plan ledgers' `Ruling:` lines:

- **The difficulty step.** No single overall difficulty setting exists (there are ten dials), so the
  **ItemRarity** dial was made "the step" everywhere: it drives `WeekModes.For`, the stretch rule and
  the pools. Renaming the driver dial is a one-line change if Jeff wants a different one.
- **Dehydrator week 3 by cost.** The cost table Jeff adopted beats the cave route; the spec prose
  said 6 before the table existed. If wrong, Dehydrator goods are three weeks early on Normal.
- **BlackberryWeek stays 10** (a bush calendar fact; the ground-forage rule already yields 9 and the
  earliest wins).
- **Field Research rolls forage quality.** The `RecipeRollDomain` tie-break left as is, flagged.
- **Winter Star and Fish Farmer's roll from narrow pools.** Accepted, flagged.
- **Five recipe rows are approximations** because `PoolItem` carries no name or tags: Fodder (grains
  by id plus the fruit category, no category -75, so it never asks for a non-grain vegetable), Wild
  Medicine (mushroom ids plus category -81), Children's (fixed sweet list), Enchanter's (essences by
  id, the ' Essence' suffix), Chef's ingredient half (Crops, Forage, Egg, Milk, AnimalProduct plus
  five staples), Field Research shells (fixed list). Jeff sees the rolled boards in the genbundles run.
- **P2 and Q2 do not share a run seed.** `tly_reset <seedLoop>` pins the BOARD only; the weekly theme
  offers roll off the run seed, which the pin does not fix. The two sims' askable tables therefore
  differ for two reasons at once (goal deposits and a different offer sequence), and nothing in the
  current data separates them. If Jeff wants the goal-deposit effect measured, the sims need a run
  seed pin too.
- **Mining and Spelunking are thin all year.** The Boiler Room is three bundles and filler is one per
  bundle per week, so those two themes have almost nothing askable outside the first week of a season.
- **Year-Two Seeds has no live proof.** There is no plant debug command, so it rests on the Trace hook
  plus unit tests; Jeff should plant Mixed Seeds once with the trace on to see the 5 percent roll.
- **The shrine Boosts menu is unexercised over the bridge.** The draw and click path needs one human
  look at the planning shrine.
- **Garlic pacing moved 3 to 4** (75 JP is about three weekly bonuses, so run 1 may not have it at 3);
  Red Cabbage 7 and Artichoke 11 stand. If wrong, Garlic is a week early or late on cards.
- **The deployed `config.json` was edited for the sims.** `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\config.json`
  is Jeff's live file and a deploy does not
  overwrite it; its `ThemeFillerBySeason` was still the pre-0.16.82 `[0, 1, 2, 99]` and is now the
  current default `[99, 99, 99, 99]`. It is NOT in any commit; a backup of the old file is in the
  scratchpad as `config.json.bak`.
- **Existing engine saves demote once on first launch.** The model now decides board bytes, so a save
  made on an older board fails the SaveLoaded manifest re-derivation and falls back to the legacy
  read path (WARN in the log), the same as every earlier pool change. The board on disk stays valid
  until the next reset.

**2026-08-28: activity themes (Spelunking, Artisan, Kitchen) and the theme-week economy, 0.16.42 to
0.16.67.** Spec `docs/superpowers/specs/2026-08-27-activity-themes-design.md`, plan
`docs/superpowers/plans/2026-08-28-activity-themes.md`, 28 tasks, one behaviour per commit.
- Phase 1 (0.16.42 to 0.16.55): effort rules derived from game data for gems and minerals (mine
  node floors), geodes, monster drops, artifacts, animal products, artisan goods, fish ponds,
  cooked dishes, crops and forage (`Core/Availability/*Availability.cs`, composed by
  `EffortComposer`); `tly_dumpeffort` writes `item-effort-model.md` (gitignored, copy to `docs/`
  for review); `tly_itemmodel` prints source and tier. **Decision: these rules produce effort
  only.** No season floor moves, so no day-28 gate changes; an effort-only id still floors at
  Winter for gates exactly as before.
- Phase 2 (0.16.56 to 0.16.62): the eight-member `Theme` enum, `ItemKind` classifier and
  `ThemeDomains`; rule A (`BundleRequirement.DueItemsFor`, the 0.16.41 stopgap folded into the
  filler tier); rule B (`ThemeFillerBySeason` config, default 0/0/1/99 since 0.16.73, one filler per bundle);
  rule E (effort quartile tiers x season weights, `GoalWeighting`); rule C (`SelectionService`
  offers only themes with 2+ askable goals, weighted by count, room themes as the floor; the hub,
  console pick and re-roll all go through `RunController.OfferFor`); rule D (weekly bonus paid
  per goal, `BonusSlot.Paid` guard, `hud.goal-paid`); `tly_themepool [theme]`.
- Phase 3 (0.16.63 to 0.16.67): the five effects. `MonsterThemePatches` (monster drops doubled
  10%, monster damage +25%), `MachineSpeedPatch` (0.75x / 1.25x on `Object.OutputMachine`, rounded
  to 10 min), `CookedFoodWeakPatch` (three postfixes on the consumption methods, category -7),
  `AnimalDoubleProductPatch` (records in `RunState.DoubleProduceToday`, cleared on DayEnding).
- Not done here: the real-play simulation (Jeff's call; `tools/sim-season.sh` and
  `tly_playseason` untouched) and the in-game effect confirmations from the spec's live list.
  The spec's Dinosaur Egg vs Diamond tiering claim does not hold under its own formula (Dino Egg
  min 3 from Mountain spots, Diamond 5): the review document shows the real numbers.
- `TODO.md` carries an uncommitted "2026-08-28 brainstorm batch" block that is not from this
  build and was left alone; its "SPEC APPROVED, NOT PLANNED: activity themes" heading is now out
  of date (built, not real-play tested).

**Last updated:** 2026-08-28 late (20-loop bundle and season-gate audit, 0.16.32 to 0.16.36)
**Branch:** `master`; 0.16.25 PUSHED (2026-08-27); **0.16.26 to 0.16.36 committed LOCALLY ONLY, not pushed, not released**
**Tests:** 1200 passing, 0 failing
**Build:** clean (mod assembly builds Release); 0.16.36 deployed to the game
**Last public release:** 0.16.17 (SVE smoke finding recorded in TODO.md "SVE board audit")

**2026-08-28 late: 20-loop audit of the 0.16.26 to 0.16.31 pool fixes, report in
`docs/superpowers/AUDIT-2026-08-29-bundle-loops.md`.** 20 diagnostic boards (`tly_genbundles 0..19`),
6 real `tly_reset` cycles (Runs 59 to 63), weekly goals for all four seasons on every live board
plus a real season advance on Run 62. All five of last night's fixes observed working (no wrong-water
fish, no any-season item in a season-named bundle, Night Fishing night-only, festival fish gated to
their season, Trash gone from the fish pool); `tly_gatecheck` found no impossible gate on any live
board. Found and fixed, one commit each, verified in-game after redeploy:
- 0.16.32 diagnostics: `tly_genbundles` lists every slot, logs gates, runs the gate audit; `tly_goals`.
- 0.16.33 PerItem weekly goals pass the obtainability predicate (Sturgeon offered in Fall, Rainbow Trout in Winter).
- 0.16.34 weekly-goal forage seasons from the engine forage pool (Chanterelle and Purple Mushroom offered in Summer; Ginger Island rows).
- 0.16.35 Jeff's request: at most one fruit-tree fruit per theme's weekly goal list.
- 0.16.36 weekly goals honour location floors (Scorpion Carp offered in Summer).
Open, need Jeff's ruling (details in the report): Red Mushroom sits in the Spring forage pool but is
pinned Summer, so loop 16's Spring Foraging audits IMPOSSIBLE; Night Fishing's "one market fish" is
Sea Cucumber or Octopus because the real trio is `ExcludeFromRandomSale`; seasonal forage bundles
repeat items on 14 of 20 boards because their pools are 6 or 7 items and the fixed lists take them
first. `docs/engine-bundle-catalogue.md` regenerated (it is gitignored, so it lives locally only).
Throwaway save is now `None_447607703` (left in place).

**2026-08-28 bundle pool fixes (two player reports: Flounder on three bundles incl. a lake bundle,
Mussel on four foraging bundles; then Salmon as a Spring weekly goal and Sea Cucumber due before
Summer 1).** Root cause traced against the game's own Data/Locations (dumped via a scratch .NET
loader): the pools treated three non-fishing keys (`Temp` = Festival of Ice map, `fishingGame` =
Fair minigame, `Default` = trash table) as habitats, and Night Market / SquidFest rows carry no
season. Landed, one behavior per commit:
- 0.16.26 ignore non-habitat location keys (fixes ocean fish in Lake Fish, river fish in Ocean Fish,
  Salmon reading as all-year, Trash in the fish pool).
- 0.16.27 season-named bundles ask only season-specific items; Winter Root + Snow Yam join Winter.
- 0.16.28 Night Fishing = fish not catchable before 6pm, plus at most one Night Market fish (Jeff's rule).
- 0.16.29 passive-festival spawns take the festival's season from Data/PassiveFestivals (Sea Cucumber
  Fall/Winter, Squid Winter); season tokens must be season names (Enum.TryParse accepted "0600").
- 0.16.30 no item asked twice across the board (fills run tightest pool first, each avoids what
  earlier bundles ask; repeats only when a pool would run dry).
- 0.16.31 `tly_dumpbundles` catalogue wording describes the fish rules.
**Not yet verified in-game.** Next: fresh reset on a throwaway clone, `tly_genbundles`, `tly_gatecheck`,
regenerate `docs/engine-bundle-catalogue.md` via `tly_dumpbundles`. Known consequence: a save mid-loop
on an older board fails the SaveLoaded manifest re-derivation and falls back to the legacy read path
(WARN in log), same as every earlier pool change; the board on disk stays valid until the next reset.

**Decision 2026-08-27 late (TODO walk with Jeff):** next build is **keep wallet items + Stardrops via
per-item JP keeps at the shrine** (same shape as the book keeps). Brainstorm, then spec, then plan; see
`docs/superpowers/HANDOFF-2026-08-27-wallet-stardrops.md`. TODO headings were caught up with the
0.14.0 / 0.14.2 / 0.16.0 / 0.16.17 releases (1123181, rose1729, difficulty, books, deja-vu all shipped).
The settings screenshots (Features, Difficulty) are confirmed live on Nexus (gallery + description) and
GitHub. The Egg Hunt per-loop guard is still open: 0.14.1 only guards once per DAY.

## 2026-08-28 (evening): the even year, 0.16.73 to 0.16.83

Spec `docs/superpowers/specs/2026-08-28-even-year-availability-design.md`, plan
`docs/superpowers/plans/2026-08-28-even-year-availability.md`. Every item on a board now has a
first week it can exist (mines 30 floors a week, Skull Cavern from Fall, machines by skill level,
animals by building tier, crops by first harvest, forage by first spawn plus location); goals may
name an item only from that week; per-item deadlines clamp to the gate season; pick-X-of-Y ramps
derive from their own items (curated table retired); every re-rolled season-less bundle keeps a
Spring foothold; goal ceilings 5/5/5/6 budgeted over the weeks left; the goals may run half a
season ahead of the gate and no further; `tly_dumpavailability` lists every item with Week, Gate,
Placed and ends with the Unknown items Jeff rules on (memory `tly-sim-list-unknowns-each-run`).
Jeff's rule (2026-08-28): the floor only stops an item showing up too early; nothing may force
one to show up. Headless sims (`tools/sim-year.sh`), gates as cumulative required slots:

| Sim | Build | Player | Gates Sp/Su/Fa/Wi | Winter weeks 1 and 2 (Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki) | Unknown |
|---|---|---|---|---|---|
| G | 0.16.78 | gate-only | 19/40/67/98 (19/41/68/100%) | 3/2/3/3/6/1/1/3 then 4/3/4/3/6/1/1/3 | 20 |
| H | 0.16.78 | goal-completing | 19/50/82/103 | 2/1/2/1/4/0/1/0 (board nearly done: 84 of 96 by Fall 28) | 17 |
| L | 0.16.81 | goal-completing | 23/45/73/102 | 3/4/3/1/6/0/2/2 then 4/0/4/1/6/0/0/1 | 6 |
| M | 0.16.81 | gate-only | 24/46/79/98 | 3/4/3/2/6/2/2/2 then 4/5/4/2/6/2/2/2 | 0 |
| N | 0.16.82 | goal-completing | 21/49/74/96 | 1/1/3/1/6/0/0/0 then the same (22 lines left, all Winter-only) | 1 (Pickles) |

Weeks 3 and 4 of every season carry goals for both players since 0.16.82. Open question for
Jeff: a goal-completing player's Winter is two themes wide (Mixed and Fishing) because 80 goals a
year on a 96-line board leaves 22 Winter-only lines; a 4/4/4/5 ceiling would leave more, at the
cost of thinner weeks earlier. Sims I, J and K were invalid (a task-stopped sim kept running and
poisoned the next two; see HEADLESS_DRIVING). Not pushed.

## Keep wallet items + Stardrops (0.16.19 to 0.16.25): built, unit-tested, LIVE SMOKE PASSED 2026-08-27 20:43 to 20:50

Spec `docs/superpowers/specs/2026-08-27-keep-wallet-stardrops-design.md`, plan
`docs/superpowers/plans/2026-08-27-keep-wallet-stardrops.md`. Eighteen Carryover rows
(`keep_wallet_*` x11, `keep_stardrop_*` x7, 6,950 JP) reach-gated on `mail:<flag>`,
`event:<id>` or `stardrop_mines`; `RunBaseline.KeptMailFlags` / `KeptEventIds` /
`KeptStardropCount`; `FarmerReset` re-adds the flags after the mail wipe, re-marks kept power
events after the re-seed, and sets max stamina to 270 + 34 per kept Stardrop. Bear's Knowledge
(2120303) and Spring Onion Mastery (3910979) joined `EventGatingTables.Default.ReplayableEventIds`,
so they no longer survive a rewind unless bought (they used to, for free). Debug: `tly_wallet`.
CHANGELOG `## Unreleased` written; README and Nexus description got the Shrine feature line (What's
New waits for the release).

**Live smoke (throwaway save None_447549305, loaded Spring 2 via `tly_loadsave`, driven with
`send-smapi-command.ps1` + `game.ps1`; screenshots `test-output/wallet-0*.png`):**

| Step | Result |
|---|---|
| `tly_wallet HasSkullKey`, `stardrop:fair`, `event:2120303`, `event:3910979`; `tly_wallet` | `HasSkullKey+HasUnlockedSkullDoor-`, `CF_Fair+`, bear `seen`, spring onion `seen`, `maxStamina=304` | PASS |
| `tly_addjp 2000`; buy `keep_wallet_skullkey` (750), `keep_stardrop_fair` (500), `keep_wallet_bearsknowledge` (150) | all three "Purchased", JP 2888 left | PASS |
| `tly_openshop`, Carryover tab | buyable rows first: "Keep Spring Onion Mastery, Cost: 150 JP" (earned, unbought); owned Bear / Skull Key / Stardrop (Fair) at the end; the other fourteen wallet/Stardrop rows hidden (`wallet-02`, `-03`) | PASS |
| `tly_reset` (Spring 2 -> Spring 1, reset log) | `FarmerReset: ... wallet=[HasSkullKey,HasUnlockedSkullDoor,CF_Fair], events=[2120303], stardrops=1 ... eventsReseeded=5 (of 8 seen-ever)` | PASS |
| `tly_wallet` after the reset | `HasSkullKey+HasUnlockedSkullDoor+` (door now open too), `CF_Fair+`, bear `seen`, spring onion `unseen`, `maxStamina=304` | PASS |
| Shrine after the reset | Spring Onion row gone (scene unseen again), Bear / Skull Key / Stardrop (Fair) still Owned (`wallet-06`, `-07`) | PASS |

Not exercised live: the Fair stall refusing a second Stardrop (needs Fall 16; the CF_Fair gate is
vanilla's own check, `Utility.cs:5848`), and the wallet tab of the inventory menu (the E / Escape
key presses were not received by the game, a known driving gotcha; the probe covers the flags).

## netWorldState keep/wipe audit (0.14.8): LIVE SMOKE PASSED 2026-08-27 19:09 to 19:20

Save None_447546774 (renamed to None_447549305 by the reset), driven from the SMAPI console and
`game.ps1`. Baseline taken on Spring 1, then slept to Spring 5 so the leak preconditions existed
(board quest "Delivery: Robin" 300g, dish Parsnip Soup x1, Y1 cart guarantee armed at 5 via the new
`tly_netstate army1 5`, ticking to 4 by day 5), then `tly_reset` (reset #56, run 57). Screenshots
in `test-output/smoke-*.png`.

| # | Check | Before reset (Spring 5) | After reset (Spring 1) | Result |
|---|---|---|---|---|
| 1 | Help Wanted board empty on Spring 1 | Board opened "Help Wanted: Amethyst for Robin, 300g" | Board opened "Nothing is posted today."; probe `QuestOfTheDay = null` | PASS |
| 2 | No Dish of the Day on Spring 1 | Probe `DishOfTheDay = Parsnip Soup x1` | Gus's full stock: Beer, Salad, Bread, Spaghetti, Pizza, Coffee, 4 recipes; no dish row; probe null. Spring 2 probe: `Glazed Yams x2` (first dish arrives day 2) | PASS |
| 3 | JP, upgrades, stash, pet, horse, buildings survive `UpdateFromGame1()` | `tly_meta`: JP=2288, 4 stash items, 35 upgrades | `tly_meta` line byte-identical; log: Rex + Mochi restored with two bowls, stable + horse restored, Coop/Barn/Silo placed; farm screenshot shows all of it | PASS |
| 4 | Spring 1 weather matches the new run's schedule | schedule tomorrow=Rain (Spring 6) | HUD sun icon; probe live/netWorldState/schedule all Sun, tomorrow Sun; log `Weather: scheduled Sun for Spring 2`; hub forecast 2..7 = Sun Rain Rain Sun Petals Sun; Spring 2 probe tomorrow=Rain, log `scheduled Rain for Spring 3` | PASS |
| 5 | Traveling Cart Y1 guarantee re-rolls per loop | `VisitsUntilY1Guarantee = 4` (armed at 5, decremented once) | `= 8` off the new uniqueID 447549305 (vanilla range 2..30); an unarmed save (-1) is left alone | PASS |

Nothing failed, so no code fix. The one code change is 0.16.18: `tly_netstate` now prints a
`[weather]` line (live Game1 flags, netWorldState Default weather, scheduler pick for today and
tomorrow) and accepts `army1 <n>` to arm the Y1 guarantee in memory, because the throwaway save
has it at -1 and the reset deliberately leaves -1 alone.

**Driving notes from this smoke (added to the gotchas):** quest board = stand at Town (42,56),
`debug fd farmer 0`, `game.ps1 -RightClick 960,470` (from (42,57) the click misses). Escape does
NOT close the board menu; click its X at (1642,168). The planning hub opens on top of everything
after a reset; pick a theme (Mixed at (1210,530)) before anything else. Gus is not at the bar on
Spring 1 at 1pm: `debug wct Gus Saloon 14 17`, stand at (14,20) facing up, `-RightClick 928,709`
opens his stock; the dish of the day is the FIRST row when present (ItemQueryResolver
DISH_OF_THE_DAY, no counter sprite). His "Can you smell that? It's the Coffee" greeting names a
random stock item, not the dish. Shop scroll arrows: down (1640,835), up (1640,235).

## Deja-vu villager dialogue (0.16.13 to 0.16.17): built, unit-tested, LIVE SMOKE PASSED

Spec `docs/superpowers/specs/2026-08-27-deja-vu-dialogue-design.md`, lines (all approved by Jeff)
`...-deja-vu-dialogue-lines.md`, plan `docs/superpowers/plans/2026-08-27-deja-vu-dialogue.md`.
Nightly rollup (talk +1, gift +3, heart event +10) into `MetaState.VillagerFamiliarity`; threshold
60, 6% per talk, tier 2 at 180, one line per villager per loop, one per 7 days town-wide, never in
loop 1; postfix on `NPC.checkForNewCurrentDialogue` prepends the line; GMCM "Deja-vu dialogue"
toggle; `tly_dejavu status|set|force|reset`.

**Live smoke PASSED 2026-08-27 17:26 to 17:46 (save None_447540453, loop 54, driven with game.ps1):**

| Step | Result |
|---|---|
| Spring 1, `tly_dejavu set Pierre 200` + `force Pierre`, talk to Pierre | Introduction line played ("Hey, it's Mr. Clone..."), NO deja-vu line, force still armed (guard works) |
| `debug sleep` | `Familiarity rollup: +1 across 1 villagers` (Pierre 200 -> 201); save carries `VillagerFamiliarity` |
| Spring 2, talk to Pierre (warped beside the farmer) | "You're my best customer. Have been for... hm. How long, exactly?" in Pierre's portrait box; log `Deja-vu: Pierre tier 2 on day 2 (forced)`; status `shownThisLoop=[Pierre] lastDay=2`, Pierre eligible=False |
| `set George 200`, `reset`, `force George`, Spring 2 talk at his chair | Introduction line; log "George is playing the 'Introduction' event line; not touching it" |
| Spring 3, talk to George | "...You're all right. Don't let it go to your head."; log `Deja-vu: George tier 2 on day 3 (forced)` |
| Talk to George again | His own daily line ("Alex is my grandson...") plays, so the ordinary line survives underneath ours |

Findings: (1) vanilla clears the stack when it plays an Introduction, so nothing else can play that
day (vanilla, not ours); (2) a villager WARPED off his schedule drops location lines flagged
`removeOnNextMove` (NPC.cs 4263), so smoke on someone at his natural spot (George's chair). Not
exercised live: the real 6% roll and the weekly cap (unit-tested).

**Driving notes that cost time (now in TODO gotchas):** talk = `game.ps1 -RightClick x,y` on the
villager (new switch, left click uses the tool); the farmer must FACE the villager
(`debug fd farmer 0`); `debug wct <npc> <loc> <x> <y>` needs a location, `debug warpcharactertome`
puts the NPC on the farmer's tile; keyboard walks did not move the farmer; indoor maps do not centre
the camera, tile (tx,ty) in JoshHouse is at screen (1215+(tx-16)*64, 870+(ty-22)*64).

## Keep power books (0.16.9 to 0.16.12): built, unit-tested, LIVE SMOKE PASSED

**Live smoke PASSED 2026-08-27 16:47 (0.16.12 deployed, save None_447536393, SMAPI console only):**

| Step | Result |
|---|---|
| `tly_readbook Book_Speed` then `tly_readbook` | `Book_Speed=1`, all other 18 books 0 |
| `tly_addjp 1000` + `tly_buyupgrade keep_book_speed` | "Purchased 'keep_book_speed' (Keep Way Of The Wind pt. 1) for 750 JP" (name from ItemRegistry) |
| `tly_readbook Book_Defense` (read, NOT bought) then `tly_reset` | `FarmerReset: ... books=[Book_Speed] ... dialogueEvents=[Introduction:6]` |
| `tly_readbook` after the reset | `Book_Speed=1`, `Book_Defense=0` (unbought book wiped), rest 0 |

Not eyeballed: the shrine row itself (no desktop driving needed; the purchase log shows the resolved name).

Jeff's brainstorm ruling (2026-08-27): per book, bought at the shrine. Nineteen `keep_book_*`
Carryover rows, reach-gated on having read the book this loop, 150 / 350 / 500 to 750 JP.
`StatResetRules` unchanged (wipe-by-default); `FarmerReset` re-grants bought flags from
`RunBaseline.KeptBookStats`. Spec + plan in `docs/superpowers/{specs,plans}/2026-08-27-keep-power-books*`.

**Docs:** CHANGELOG `## Unreleased` covers 0.16.8 (first-meeting dialogue) and the books; README and
Nexus Shrine feature line updated identically. "What's New" still says 0.16.7 until the release
number is chosen.

**Next:** Part 2 of the same brief, Deja-vu villager dialogue (TODO.md `[1.0.0]` entry, credit
u/Gribbleby), brainstorm first.

## Previous state (2026-08-27 afternoon)

**Last updated:** 2026-08-27 (derived item availability model, Phase 1, built and smoked)
**Branch:** `feat/difficulty-modifiers`, 52 commits ahead of `master`, **LOCAL ONLY (not pushed, not merged)**
**Tests:** 1113 passing, 0 failing
**Build:** clean
**Last public release:** 0.15.0

## THE PERITEM GATE BASELINE HAS SHIFTED. The next release notes must say so.

Bundles that require every item they show used to take their per-item due dates from a
40-entry hand table (`GameplayConfig.DefaultItemSeasonPins`). Anything outside it had no due date
and applied no pressure until the Winter win check. Because the engine re-rolls the six fish
bundles from a 52-item pool and the two metals bundles from an 11-item pool, most re-rolled boards
were partly or wholly ungated.

Phase 1 replaces that with a model the engine derives from the game's own data: per item an
earliest-possible season and an effort score; per bundle, deadlines spread across the four
checkpoints by effort and clamped upward to each item's floor so an impossible gate cannot be
expressed.

**This makes the game harder at every difficulty, deliberately** (Jeff's ruling, 2026-08-27).
`DifficultyResolverTests.Normal_Resolves_To_Todays_Config_Values` still passes because it asserts
difficulty dial values, not gate outcomes, but "Normal equals the 0.12 shipping balance" is no
longer true of season gates.

Measured on three live boards, two configurations including a full Hard sweep: no impossible
gates, 0 never-gated bundles, 66 ids derived, 0 curated pins rejected. Numbers and per-bundle
detail are in the plan's Results section.

- Spec: `docs/superpowers/specs/2026-08-27-derived-item-availability-design.md`
- Plan + results: `docs/superpowers/plans/2026-08-27-derived-item-availability-phase-1.md`

**Phases 2 to 4 are not built.** Orchard, Tapper's, Forest, Spirit's Eve, Home Cook's and Wild
Medicine still wait for Winter, because their ingredients come from domains Phase 1 does not model
(crops, forage, monster drops, artisan goods, cooking, artifacts, books, saplings, geode minerals,
tapper goods). Each later phase needs its own plan.

**Live smoke PASSED 2026-08-27 (0.16.1-0.16.7, Rodger throwaway save, driven from the SMAPI console):**
`tly_addpet Cat Mochi 1` + `tly_addpet Dog Rex 0` + `tly_fixbridge` + `tly_stashrod`, then `tly_reset`.
After the reset: both pets restored, Mochi owns the default bowl (53,7), a second bowl placed at (51,7)
and assigned to Rex (screenshot: two bowls on the fence pads); Beach tile (58,13) back to 284 with the
Action property present and bridgeFixed=false; the stashed Iridium Rod still has 20 bait, a spinner
and Auto-Hook. Loading the rotated save also placed the missing bowl on DayStarted (the 0.16.4
self-heal). One fix came out of it: vanilla isBuildable reads tile properties off
Game1.currentLocation (the farmhouse during a reset), so 0.16.7 checks the Farm map directly.

**Live smoke PASSED 2026-08-27 (0.16.8, villager first-contact dialogue):** after `tly_reset` the
FarmerReset summary logs `dialogueEvents=[Introduction:6]`, and Pierre greets the player with his
Introduction line ("Hey, it's Mr. Clone, the new farmer! I'm Pierre...") on reset #52. Driving note:
left-click on an NPC only talks when the selected hotbar slot is EMPTY; with furniture selected the
click tries to place it. `tools/game.ps1 -Key x` is now supported. 0.16.8 is local only, not released.

## NEXT SESSION: difficulty modifiers need an in-game smoke, then a merge decision

Jeff brainstormed this the night of 2026-08-26 and said "write the spec, plan, and build" before
going to bed. All 16 planned tasks are done and committed on `feat/difficulty-modifiers`. Nothing
is pushed and nothing is merged to `master`: both are Jeff's call.

- Spec: `docs/superpowers/specs/2026-08-26-difficulty-modifiers-design.md`
- Plan: `docs/superpowers/plans/2026-08-26-difficulty-modifiers.md`

**What it is:** ten independent Easy/Normal/Hard/Extreme dials in a new GMCM "Difficulty" section.
No overall tier (Jeff killed that mid-brainstorm). Everything defaults to Normal, which resolves to
today's exact config values, so an untouched save is unchanged. A change applies at the NEXT reset,
because the resolved profile is stamped onto the save and every consumer reads the stamp.

**Nobody has seen any of it run.** The whole thing is unit-tested and builds, but it has never been
loaded in the game. What needs smoking, in rough priority order:

1. `tly_difficulty` on a loaded save prints sensible output and says whether the stamp or live
   config is in force.
2. GMCM shows the Difficulty section with ten dropdowns, and a change survives a save/reload.
3. Set stack size + required slots to Hard, `tly_reset`, and check the board actually changed:
   `tly_genbundles` should show bigger stacks and higher pick-X counts.
4. **The Vanilla post-pass is the riskiest change here.** `BundleSource=Vanilla` previously wrote
   NOTHING at reset; it now rewrites the board when any ask-side dial is off Normal. On a Vanilla
   save, reset at Hard and confirm the CC menu still opens, ingredient ITEMS are unchanged, and
   stacks/pick-X moved.
5. Set everything back to Normal, reset, and confirm a board identical to a pre-branch one.

**Known gap, minor:** a brand-new VANILLA-source save has no stamp until its first reset, so a GMCM
change during loop 1 of such a save applies immediately rather than next loop. Self-corrects at the
first reset. Engine saves stamp during fresh-run generation, so they do not have this.

**Resolved 2026-08-27 on deploy (was flagged overnight as an open question).** The ten steps
serialize into config.json as readable NAMES, not integers: the deployed
`Mods/TheLongestYear/config.json` shows `"StackSize": "Normal"` and so on for all ten. The
overnight worry, based on `StringEnumConverter` not appearing in StardewModdingAPI.dll, was wrong.
No fix needed and no ruling required.

**Deliberate deviation from the spec, recorded:** the spec describes the rarity bias as applying
inside the sampler. It is applied to `ItemPools` before generation instead, and the stack/quality
modifiers are applied by scaling the tuning block. Same effect, and it meant `BundleSlotFiller` and
`AuthoredBundleComposer` needed no edits at all.

**Also parked this session:** Impossible mode (post-1.0), written up in `TODO.md`.

**Two things NOT done, both waiting on Jeff:**
- No manifest version bump (branch rule: only the release line bumps).
- No "What's New" entry in the README or Nexus description, because the release number is not
  decided. The Difficulty section itself is written into both, content-identical.

## Previous state

**Last updated:** 2026-08-26 evening (0.14.0, 0.14.1 and 0.14.2 all released today)
**Branch:** `master`, pushed
**Tests:** 865 passing, 0 failing
**Build:** clean; 0.14.2 deployed to PC Mods
**Last public release:** 0.14.2 (2026-08-26: GitHub v0.14.2, Nexus file via workflow, page version
+ description + changelog synced, FAQ live)

Today, driven by finding that **emmalution (82.7K subs) has been streaming the mod since 16 July**:

- **0.14.0** — the Junimo Shrine never opened on a Fail night (Nexus 1123181, a 0.12.17 regression
  that killed meta-progression); weekly goals could tick without a donation; no way to get another
  pet after declining Keep Pet.
- **0.14.1** — festival main events run once per day (the Egg Hunt and the Luau soup could be
  repeated by leaving and re-entering); weekly goals capped to what a bundle can still accept.
- **0.14.2** — Shop Discount discounts the price rather than the payment (tool upgrades exempt);
  **fixed a bug shipped in 0.14.1** where the once-per-day festival stamp survived a rewind and
  blocked festivals in every later loop; new GMCM "Features" section; mod-page FAQ.

Playtest tooling was rebuilt: `tools/game.ps1` + `tools/screenshot.ps1` (the old pair lived in
gitignored `test-output/`). An unfocused game is a PAUSED game, and SetForegroundWindow fails
silently, which is why keyboard input never reached the farmer. Both handled; screenshots are
cropped to the client area so image pixels are click coordinates.

## NEXT SESSION: run the netWorldState audit

Jeff wants a fresh agent on this tonight. The brief is self-contained in
`docs/superpowers/HANDOFF-2026-08-26-networldstate-audit.md` - enumerate every NetWorldState
field, rule each keep or wipe against the reset philosophy, implement the wipes, smoke it.
Difficulty setting is also queued but Jeff is brainstorming it tomorrow; do not design it alone.

## Current state (2026-08-25 afternoon): 0.13.0 released, fully closed

Shipped on top of the merge below, all live-smoked on the Rodger throwaway save (TODO.md tables):
the year-2 crop gate (Garlic/Artichoke need Pierre's Special Order, Red Cabbage that or Cultivation),
the merchant's Junimo line removed, and **season pity as an opt-in offer** (second Fail-night question
after keep/reshuffle; `PityCosts` curve like the hold; `tly_pity accept|decline`; the offer is deferred
one tick because a nested question inside the hold callback gets torn down by answerDialogue).
Bug 1122901 (Keep pet) left OPEN on purpose: the reply asks a multi-pet tester to confirm on 0.13.0.
Bug 1122358 stays Fixed; reply asks the reporters to run a loop on 0.13.0 and report any leftover
impossible ask. Chrome-extension gotcha: after a long session the automation bridge went stale even
though chat worked; `/mcp` reconnect was not enough, killing and relaunching Chrome fixed it.

**Open (new, 2026-08-25 12:41 post by rose1729):** did NOT keep the pet at the end of loop 1 and was
never offered a pet again in loops 2/3. Likely the reset leaves a vanilla pet-adoption flag set
(check `MarniePetAdoption` handling in the reset path); needs a code check + reply. Not yet answered.

**Next:** rose1729's pet-offer question; watch the 0.13.0 replies; 1113831 Day-3 crash still silent.

## Previous state (2026-08-25): v0.13.0 on master, not released, three fixes not yet live-smoked

Merged `worktree-fixes-0-13-0` (plan `docs/superpowers/plans/2026-08-25-0-13-0-fixes.md`, 11 commits,
subagent-driven with per-task reviews + final review) on top of the season pity merge:
- **Quality-ask vetting v2** (Nexus 1122358 follow-ups): `ItemPools.QualityEligibleIds` derived from
  Data/Crops (skipping `HarvestMaxQuality == 0`, i.e. Fiber), rod-caught non-jelly fish, and spawned
  forage passing the game's isForage category test; `BundleSlotFiller.RollQuality` refuses quality on
  anything else; `tly_genbundles` prints "quality asks:" per bundle. Curated additions (Tea Leaves,
  Red/Purple Mushroom) never carry quality (accepted).
- **Keep Pet keeps every pet** (Nexus 1122901): `MetaState.PetStates` list, legacy `PetState`
  migrates at the next reset, restore tiles stagger west from (54,8).
- **Traveling Cart cap per day** (lexihope): `CartDayStock` remembers the day's ids on
  `RunState.CartStockDay/Ids`; `CartSlotLimitPatch` filters later builds; recipes keyed `#Recipe`.
Reply drafts for all three: `release-notes/2026-08-25-replies-draft.md` (post only on "yes, push").

**Next:** live smoke of the three fixes on the Rodger save (`tly_genbundles` quality-asks lines: no
771 / jellies / 815; buy from the cart then reopen it, the slot stays empty; reset with two pets,
both come back), then README + Nexus "What's New in 0.13.0" (identical content), CHANGELOG
`## Unreleased` -> `## 0.13.0`, release on "yes, push", post the three replies, flip 1122901 to Fixed.

## Previous state (2026-08-25): v0.12.19 on master, season pity merged, not released

Merged `worktree-season-pity` (spec `docs/superpowers/specs/2026-08-25-season-pity-design.md`, plan
`docs/superpowers/plans/2026-08-25-season-pity.md`, 15 commits, subagent-driven with per-task reviews
and a final whole-branch review). Per-season fail counter (`MetaState.SeasonFailCounts`); first 5 fails
at a season are standard; from the 6th, KEEP lowers that season's quota 10%/step (floor 50%) via a
`BoardEaseSeason/Steps` stamp read back on load, RESHUFFLE trims the 2 hardest eligible items/step via
`BoardTrimSeason/Steps` (both stamps keep reloads byte-identical to the reset). Passing a season drops
its count to 5; Winter never gets the keep-path ease. `tly_pity status|set`, GMCM "Season pity" section,
eased Fail-night prompt (+ Winter variant), "eased Nx" title. Rules in `Core/SeasonPity.cs`,
`SeasonEase.cs`, `ItemHardness.cs`, `PityTrim.cs`; trim inside `BundleSlotFiller.Fill`.

**Live smoke PASSED 2026-08-25** (table in TODO.md): eased prompt, keep stamps the ease and the reset
applies it, reload clean, reshuffle trims (Blacksmith's 11 -> 7) and clears the ease, reload clean,
`tly_genbundles` determinism OK. Not eyeballed: the "eased Nx" title (book not placed). Not exercised
live: the real day-28 RecordFail/RecordPass path (unit-tested).

**Next:** README + Nexus "What's New in 0.12.19" (identical content, TLY Custom only), CHANGELOG
`## Unreleased` -> version, then the release on "yes, push" (`release.ps1`, then the Nexus page via
Claude-in-Chrome).

## Previous state (2026-08-25): 0.12.18 released, fully closed

0.12.17 (hold feature) and 0.12.18 (Void Salmon out: WitchSwamp joins the built-in excluded
location markers and `(O)795` the built-in excluded ids, since the Witch's Swamp is behind the
post-CC Dark Talisman quest; Jeff's "hard but fair" ruling from 0.12.16 reversed) went out
back-to-back. Nexus description = README (What's New in 0.12.18 incl. the Void Salmon apology),
changelog entry added, version 0.12.18. Bug 1122358 got a follow-up reply with the apology
(status stays Fixed). Release mechanics note: `release.ps1` step 3 (Playwright description
sync) is retired; run it with `-SkipNexusDesc` and do the Nexus page via Claude-in-Chrome.

**Next:** the 0.13.x DerivePins brainstorm (TODO.md); open Nexus bug 1113831 (Day-3 crash, silent).

## Previous state (2026-08-24 evening): v0.12.17 on master, keep-bundles hold done, not released

Merged `feat/keep-bundles-hold` (spec `docs/superpowers/specs/2026-08-24-keep-bundles-hold-design.md`,
plan `docs/superpowers/plans/2026-08-24-keep-bundles-hold.md`). Fail night now asks, before the shrine,
whether to keep the same bundle board next loop (first hold free, then 50/100/200/300 JP via
`GameplayConfig.BundleHoldCosts`, counter resets on reshuffle). State: `MetaState.BundleSeedLoop`,
`ConsecutiveHolds`, `HoldChoiceMadeForReset`; rules in `Core/BundleHold.cs` + `BundleHoldPricing.cs`;
both seed call sites use `EffectiveBundleSeedLoop`. Day-1 CC speech gained `event.intro.junimo-9b`;
Season Goals title shows "held Nx"; `tly_hold keep|reshuffle|status` debug command; every em dash removed
from player-facing strings (house rule: never use em dashes in anything for Jeff). Live-smoked on the
Rodger throwaway save (TODO.md table): free/paid hold, reload from title, reshuffle, full Fail-night chain,
too-little-JP re-ask (fixed to defer one tick). Not eyeballed: the held title and the intro line.

**Next:** release 0.12.17 as a normal patch release (or a minor if Jeff declares it): write README +
Nexus "What's New" (identical content), move the CHANGELOG `## Unreleased` entry under the version,
`release.ps1 -SkipNexusDesc` + Claude-in-Chrome description/version/changelog, all only on "yes, push".
Then the 0.13.x DerivePins brainstorm parked in TODO.md (escalating per-season likelihood, pity counter).
Open Nexus bug: 1113831 Day-3 crash (Needs more info, silent). 1117543 muting closed Not a bug today.

## Previous state (2026-08-21 night) — 0.12.11 release candidate

Everything in `HANDOFF-2026-08-21-pre-0.12-release-work.md` is done and smoked on the deployed build:
A1 screenshot, A2 empty-theme card (no fix needed), A3 `EnableNonObjectDonations` next-board rule
(v0.12.4), B5 `tly_jpbudget` + 5-loop measurement (v0.12.5–6), B6 cult repricing per ruling (v0.12.7:
starfruit gone, red cabbage 5k, Pierre's Special Order 10k — smoked at Pierre's), A4 twelve curated ramps
+ trophy trim (v0.12.8), C7 `BundleSource` Engine|Vanilla with the TLY Custom / Normal / Remixed dropdown
(v0.12.9–11 — smoked: Engine → Vanilla/Default → Vanilla/Remixed → Engine resets all classify correctly,
dropdown eyeballed). Release docs written (README ≡ Nexus What's New, CHANGELOG 0.12.11, Nexus changelog
file). **Next: user says "yes, push" → `release.ps1 -SkipNexusDesc`, description/version sync + changelog
paste via Claude-in-Chrome, upload `release-notes/advanced-options-tly-custom.png` to the gallery and
replace the `[img]` placeholder, verify live.**

## Previous state (2026-08-21 midday) — post-sweep bugfix pass, ready for smoke + beta decision

The 07-17→08-21 sweep surfaced nine 0.11.60 bug threads (see `TODO.md` "6th sweep" table for the
full root-cause/fix matrix). All are fixed on master as one-commit-each v0.11.101–110 — CC ceremony
id swap, museum `specialItems` wipe, `mail`-granted event replay, Mixed Seeds retarget, weather
rewrite (totems/CJB survive; vanilla-like density), stash banked pre-wipe, kept-building
`InitializeIndoor`, kept-tool state transplant, fail-night FarmEvent suppression + scene watchdog,
plus the Cart Stall cap toggle/flavour/docs. The remix-bundles thread (the loudest one) is already
moot on master because the engine writes the board.

**Released as 0.12.0-beta.1 on 2026-08-21** (user call: ship master, no backport). **v0.12.1 smoke PASSED 2026-08-21**
(TLY Custom dropdown + every bugfix from the sweep re-verified on a real loop reset — TODO has the table). **Next:** watch the beta
feedback; answer the two PRIVATE bug reports (see TODO); decide the Standard-vs-engine bundle opt-out; the
Normal-bar PoolTuning playtest loop + cult repricing remain the gate for a non-beta 0.12.0.

## Previous state (2026-07-20) — beta-release decision point

All three 0.12.0 engine plans are shipped (v0.11.61→v0.11.100): authored bundles (11 defs
incl. Gil's Trophies with Warrior Ring), weapon/hat donations (`EnableNonObjectDonations`),
Vault engine-owned +25%, SVE compat pass. Final review passed after 2 trivial fixes
(v0.11.99/100). `TODO.md` is the live source of truth — see its "0.12.0 ENGINE PLAN 3 of 3"
entry for full detail.

**Assessed 2026-07-20: ready for a public BETA with two gates:**
1. **One human check outstanding** — a live CC click-through of a weapon/hat donation into a
   trophy bundle (`tly_trophytest` proved match/accept programmatically; no human has run the
   real menu flow). Riskiest untested surface; 10 min on the already-deployed PC build.
2. **Version framing** — 0.12.0 is reserved for after the Normal-bar `PoolTuning` playtest
   loop + cult repricing decision. Ship the beta as **0.12.0-beta.1** (or 0.11.100 marked
   beta/optional on Nexus), NOT as 0.12.0. Beta feedback feeds the tuning pass.

Release-note caveat to include: flipping `EnableNonObjectDonations` mid-loop can strand an
in-flight trophy bundle until the next reset (known, documented).

Release mechanics: `gh release create` → publish-nexus workflow (TLY flow verified live by
0.11.60; `file_id` 7502657); description sync via `release.ps1`; Nexus changelog = manual
browser paste. **No push/release without explicit "yes, push."**

---

## Historical — v1 snapshot (2026-05-27, after Plan 07)

**Status then:** v1 ready for first meaningful playtest (328 tests).

## What v1 means

Per the original design spec §14, v1 = "MVP — prove it's fun & stable on PC." Everything below
either ships in v1 or is explicitly deferred.

## Done

| Plan | Branch / commits | Shipped |
|---|---|---|
| **Plan 01 — Foundation** | merged | Core types: `MetaState`, `RunState`, `MetaStore`, `GameplayConfig`, `Calendar`, `Theme`/`Season`/`Rarity` enums, `JpSettings`. |
| **Plan 02 — Contracts** | merged | `RunManager`, `GateEvaluator`, `SelectionService`, `BundleCatalogBuilder`, `BundleGate`, theme/season classification, solvable-partition contract generator. |
| **Plan 03 — Lifecycle / reset** | merged | `WorldResetService` (in-place reset via `Game1.loadForNewGame`), `SaveBackup`, `WorldStateProbe` (leak test), `CommunityCenterUnlock`, `CcLocationAccessiblePatch`. |
| **Plan 04 — Donations + JP** | merged | `DonationService`, `DonationObserver` (Harmony-patched), `BundleCatalogBuilder` (catalog from `Data/Bundles`), `JpCalculator`, `UpgradePurchase` rule, `VaultRules`. |
| **Plan 05 — UI** | `feat/v1-plan-05-ui` | `WeeklyHubMenu` (planning hub), `JunimoShrineMenu` (upgrade shop), `MenuLauncher`, `SeasonGoalsBoard` (CC interactable), `UpgradeCatalog` + `UpgradePurchaseService`. |
| **Festival fixes** | `feat/v1-plan-05-ui` | Time flows during festivals, exit at real in-game time, auto-eject at festival end, HUD redraw during festivals, "Are you sure" suppression, day-8 hub unblock, day-3 forced rain removed, RNG re-seed on reset, Joja root-cause fix. |
| **Plan 06A — Persistence effects + per-stat keep upgrades** | `feat/v1-plan-06a-persistence-effects` | Wires `OwnedUpgrades` into reset effects (backpack, gold, kept coops/barns, kitchen, vault bus, horse, starting animals). Adds 80 chained keep entries (16 tool tiers + 2 rods + 50 skill levels + 12 mine elevator floors). Cap-not-grant via `PlayerSnapshot` (in-run peak captured pre-wipe) + `RunState.PeakMineFloor`. Profession picker re-fires for kept L5/L10 skills. Shrine UI hides locked entries. Generalised `MeetsMetaRequirement` (upgrade/quest/mail/season). |
| **Plan 06B — Cookbook + Craftbook** | `feat/v1-plan-06b-cookbook-craftbook` | 6 Carryover catalog entries (Cookbook/Craftbook I/II/III @ 150/350/700 JP, 5/10/20 slots). `CookbookMenu` + `CraftbookMenu` slot-grid IClickableMenus with sub-mode recipe picker (currently-known only) and confirm-remove dialog. `FarmHouse.checkAction` Harmony patches open menus on configurable tile coords (`tly_setcookbook`/`tly_setcraftbook`). `IndicatorRegistry` for reusable ?/! bubbles over world tiles. Quest intros via vanilla `Quest` on first reset after purchase. Recipe re-grant on `FarmerReset.Apply`. `MetaState` extended with `CookbookRecipes`/`CraftbookRecipes` (List<string>) + `DismissedIndicators` (HashSet<string>). |
| **Plan 06 — Theme effects layer** | `feat/v1-plan-06-theme-effects` | `ThemeModifiers` ids corrected to match signed-off spec (mines_closed / fish_bite_down / forage_off). `ActiveEffectsProvider` + `BonusDropResolver` Core types wired through `RunController` (Set/Clear on theme select + reset). 6 Harmony patch files implementing all 10 bonus/liability effects: forage_yield_up / forage_off / crop_growth_up / crop_growth_down / fish_bite_up / fish_bite_down / mine_drops_up / mines_closed / all_drops_up / all_sell_prices_down. `MixedSeedsPatch` injects Red Cabbage / Starfruit per cultivation upgrades (bool overload pinned). `fortune_rare_fish` gives +25% bite rate. `WeatherForecast` + `CartStockPreview` Core types deliver real foresight data to `WeeklyHubMenu` per owned Weather Sage / Cart Whisperer tiers. `tly_activeeffects` debug command. |
| **Plan 07 — Junimo Stash** | `feat/v1-plan-07-junimo-stash` | Pure Core: `StashItemRecord` POCO + `MetaState.StashItems` + `MetaState.StashSlotCount` (0/4/8 from `stash_1`/`stash_2`) + `GameplayConfig.StashTileX/Y`. Mod-side: `JunimoStashService` manages the tagged Chest lifecycle (place + populate + bank + register indicator + find), `JunimoStashCapPatch` enforces the slot cap via `Chest.addItem` postfix (HUD message on rejection), `JunimoStashShowMenuPatch` dismisses the `tly.stash` indicator on first open. Wired into `WorldResetService.PerformReset` step 13b, `MetaStore.Save` (anti-save-scum invariant preserved), and `ModEntry.OnSaveLoaded` (mid-run save-load safety). Quest intro `tly.-9003` fires on first run after stash_1 + tile configured. Debug commands: `tly_setstash`, `tly_openstash`, `tly_stashclear`. `tly_meta` extended with stash summary. |

## v1 implementation complete

All §14 v1-scope items shipped. Ready for first meaningful playtest.

**Pre-playtest setup checklist (debug-only, no in-game onboarding for v1):**

1. Build + deploy the mod (build is clean as of branch `feat/v1-plan-07-junimo-stash`).
2. Load a save.
3. Anchor the interactable world tiles via debug commands — each requires standing on/facing the target tile:
   - `tly_setboard` (Season Goals board, inside CC)
   - `tly_setcookbook` (kitchen counter)
   - `tly_setcraftbook` (farmhouse table)
   - `tly_setstash` (any farm tile)
4. Purchase upgrades via `tly_addjp 5000` + `tly_buyupgrade <id>` for the features to verify.
5. `tly_reset` to land on Spring 1 with the configured surfaces active.

## Deferred beyond v1

- **Cookbook/Craftbook Phase C (LY3)** — friendship per-NPC + wallet-flag per-item retention.
- **Cutscenes / full narrative** — placeholder text only in v1.
- **Endless victory-lap mode** — single-win run for v1.
- **Android port** — PC first.
- **Deep balancing pass** — calibrate numbers after v1 has been played.
- **Advanced contract modifiers** — per-run "blessings" etc.
- **SVE compatibility pass** — most pieces are SVE-safe already (see future-expansions notes).
- **LY2 / LY3** — Year 2/3 ultimate-perfection content, separate JP economies, possibly separate mods.

## Known playtest carryovers

From 06B:
- **Indicator `?` source rect** `(397, 489, 10, 10)` in `IndicatorRegistry` is approximate; visually verify the right sprite renders. One-line constant fix if wrong.
- **Indicator tile coords** start at `(0, 0)` (= disabled). After buying `cookbook_1` / `craftbook_1`, the player needs to run `tly_setcookbook` / `tly_setcraftbook` once each to anchor the interactable + bubble.

From 06:
- **`forage_off` over-suppression (JC-4)** — Mining liability also blocks weeds/stones via `spawnObjects`. Flag for playtest to assess if too punishing.
- **`fortune_rare_fish` is a 0.75× bite-rate multiplier (JC-2)** — v1 approximation for rare-fish boost (true rarity intercept requires deeper Stardew internals investigation).

## Small follow-ups (not blocking v1, can land any time)

- **Festival exit to host map.** Currently `Event.endBehaviors` warps to the farm entry; should land on the festival's host map (Town for Egg/Fair/Spirit's Eve; Beach for Luau/Jellies; Forest for Flower Dance). ~20 lines (`endBehaviors` postfix or transpiler).
- **Seed-driven weather scheduler** with per-season minimums. Spec'd in `TODO.md`.
- **Wipe-meta debug command** (`tly_wipemeta`). Trivial — replace `_meta.State` with `new MetaState()` + `_meta.Save()`.
- **Weekly Theme Journal entry.** Player-facing reminder + bonus-item completion tracking → liability suppression on completion. Spec'd in `TODO.md`.

## Workflow rules in effect

- Local commits only. Never push without explicit "yes, push".
- Co-Authored-By footer on every commit.
- Build/test/deploy: I do, user plays, I pull logs.
- Reserve playtests for MEANINGFUL feedback opportunities. Don't request a playtest just to confirm wiring fires — verify that solo.
- Run with `-p:EnableModDeploy=false` while Stardew is open (file-lock on the deployed DLL).
