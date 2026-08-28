# Handoff: review the even-year availability build (0.16.73 to 0.16.84)

Written 2026-08-28 for a fresh agent. Jeff wants an independent check of everything below before
it ships, plus a second opinion from Codex (`/codex` skill). Nothing here is pushed; `master` is
30 commits ahead of `origin/master`. Deployed and running in the Mods folder: 0.16.82.

## What Jeff asked for, in his words

- The theme weeks must not be "impossible in Spring but free in Winter". Weeks and seasons should
  ask for similar amounts all the way through: challenging but mostly achievable.
- The availability floor's only job is to stop an item showing up before it is reasonably
  accessible. **Nothing may force an item to show up early** on the themes or the gates. (He
  rejected my idea of moving mid-mine floors to a Summer gate to "spread the mining domain".)
- Mines: 30 floors a week for the theme goals; Skull Cavern in the Fall gate and beyond.
- Every category needs some presence in Spring where the game allows it (the old model defaulted
  82 of 154 board items to Winter because no rule knew them).
- After every sim: list every item on the board with its first week and put every unknown or
  judgement item in front of him to rule on (memory `tly-sim-list-unknowns-each-run`).
- Parked for a later build: single-loop difficulty (Normal winnable but not easily in one loop,
  Hard not in one). emmalution won on one Summer fail without using the stash.

## What was built (commits 0.16.73 to 0.16.84, all on master)

Specs: `docs/superpowers/specs/2026-08-28-theme-week-budget-design.md` and
`docs/superpowers/specs/2026-08-28-even-year-availability-design.md`. Plan:
`docs/superpowers/plans/2026-08-28-even-year-availability.md`.

1. **Goal budget** (0.16.73/74): a theme asks for its open lines spread over the weeks left in
   the season, capped by the season ceiling, floored at 2 while the pool has 2 (the offer rule
   needs 2). `GoalBudget.cs`.
2. **Week-granular availability** (0.16.78): `ItemAvailability` carries `EarliestWeek` (1 to 16)
   and `GateSeason`. Every Phase 2 rule (mines, geodes, monster drops, artifacts, animals, artisan
   goods, dishes, ponds, crops, forage, saplings) now places an item in time, not just in effort.
   `AvailabilityWeeks.cs` holds every judgement number. Goals check the week
   (`GoalObtainability`), per-item deadlines clamp to the gate season (`BundleDeadlines`).
3. **Ramps follow the items** (0.16.78): a pick-X-of-Y ramp is an even quarter split of X capped by
   what its own ingredients can supply by each season's gate (`BundleClassifier.RampFromItems`).
   The curated quota table is empty; user `BundleQuotas` still overrides.
4. **Spring foothold** (0.16.78, narrowed 0.16.79): the engine swaps one Spring-gated item into a
   re-rolled season-less bundle when the pool has one (`BundleSlotFiller`, `SpringFoothold`).
   **Jeff has not decided whether to keep this**; it is the one mechanism that shapes bundle
   contents rather than gating them.
5. **Flat goal ceilings 5/5/5/6** (0.16.78); filler allowed in every season (0.16.82).
6. **Goals follow the gate, half a season ahead at most** (0.16.80, revised 0.16.82): per bundle
   the goals may ask for what the gate demands by this season plus half of next season's share
   (`SeasonNeed.cs`). Fixed sim H (goal player emptied the board by Fall) and sim L (weeks 3 and 4
   empty once the season's share was in).
7. **Rules fixed from the sims** (0.16.79 to 0.16.84): earliest week wins across rules (Wood was
   week 5 from the Recycling Machine); pins may move a rule week earlier (only fish, crab-pot and
   metal floors are facts); deluxe animal produce keeps its building's week; plural machine tags
   (`category_fruits`); trap fish from Data/Fish (week 2); Pierre's staples and the Saloon menu
   (week 1); Guild and Help Wanted rewards; pool artifacts and books; table weeks for Cave Carrot,
   Moss, Tea Leaves, Pickles, Oil of Garlic, Jack-O-Lantern, fruit-tree fruit, Oasis-seed crops,
   Winter dig forage; foothold skips season-named bundles.
8. **Tooling**: `tly_dumpavailability` (every board item: week, gate, placed-by, basis, plus
   Unknown items and Rejected overrides sections), `tly_skipscene`, `tly_playseason goalsonly`,
   `tools/sim-year.sh minimal|goals <label>`, `docs/HEADLESS_DRIVING.md` year-sims section.

## Sim results (headless, `tools/sim-year.sh`; gates = cumulative required slots by day 28)

| Sim | Build | Player | Gates Sp/Su/Fa/Wi | Winter weeks 1 and 2, askable Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki | Unknown |
|---|---|---|---|---|---|
| A (baseline) | 0.16.72 | goal-completing | 11/34/65/90 | Mixed 7, rest 0 to 2; weeks 2 to 4: 1 or 0 | n/a |
| G | 0.16.78 | gate-only | 19/40/67/98 | 3/2/3/3/6/1/1/3 then 4/3/4/3/6/1/1/3 | 20 |
| H | 0.16.78 | goal-completing | 19/50/82/103 | 2/1/2/1/4/0/1/0 (84 of 96 donated by Fall 28) | 17 |
| L | 0.16.81 | goal-completing | 23/45/73/102 | 3/4/3/1/6/0/2/2; Spring weeks 3 and 4 asked 0 | 6 |
| M | 0.16.81 | gate-only | 24/46/79/98 | 3/4/3/2/6/2/2/2 then 4/5/4/2/6/2/2/2 | 0 |
| N | 0.16.82 | goal-completing | 21/49/74/96 | 1/1/3/1/6/0/0/0 (22 Winter-only lines left) | 1 |
| O | 0.16.82 | gate-only | 22/43/71/100 | 3/3/3/2/6/1/2/2 then 4/3/4/2/6/1/2/2 | 1 |

Sims I, J, K were invalid (a task-stopped sim kept running; see HEADLESS_DRIVING). Every gate
passed in every valid sim; `tly_gatecheck` reported no impossible gate on any board. Raw
transcripts are in the session scratchpad (`sim*.txt`, `board-*.md`, `gaterows*.txt`); the last
board listing is `docs/board-availability.md` (gitignored, regenerate with `tly_dumpavailability`).

## Known wrong or doubtful placements (found after the last sim, NOT fixed)

- **Trash, Driftwood, Soggy Newspaper, Broken Glasses, Broken CD: week 5.** They are placed by
  the fish-pond rule (a Cockle pond yields them). No rule knows that fishing yields trash from day
  1; Jeff: "you can't get a crab pot until you've done enough fishing to have a collection of
  trash", so trash must be week 1 and before the crab-pot week (2).
- **Crystal Fruit week 2** via Dust Sprites at 2 percent. Technically reachable, practically a
  Winter forage. Jeff to rule.
- **Coffee Bean week 2** (Dust Sprite drop, then a 10-day crop). Plausible, not checked by Jeff.
- The whole judgement table below has not been reviewed by Jeff item by item.

## Judgement numbers (all in `src/TheLongestYear.Core/AvailabilityWeeks.cs`)

- Mines: floors 1 to 39 week 1, 41 to 79 week 2, 81 to 119 week 3 (Summer gate), Skull Cavern
  week 9 (Fall gate). Mine fish: Stonefish, Ghostfish 1; Ice Pip 2; Lava Eel, Cave Jelly 4
  (Summer gate). Sewer and Bug Land week 5; Witch's Swamp week 13.
- Machines by skill level: 0 to 2 week 2, 3 week 3, 4 to 5 week 4, 6 to 7 week 6, 8 to 9 week 7,
  quest or friendship unlock week 9; the good is never before its input. Kitchen dishes week 5
  regardless of keep_kitchen (board determinism). Fish pond product = fish week + 4. Crab pot
  catches week 2. Books week 2. Saplings and artifacts week 1.
- Animals: base coop or barn week 2, big week 5, deluxe week 9; deluxe produce keeps the
  building's week.
- Shops and rewards: Sugar, Wheat Flour, Oil, Rice, Vinegar, Salad, Bread, Spaghetti, Pizza,
  Coffee week 1. Guild: Hard Hat 2, Crabshell Ring 2, Insect Head 3, Skeleton Mask 3, Vampire
  Ring 4, Savage Ring 5 (Summer gate), Burglar's Ring 6, Slime Charmer 8, Napalm Ring 12. Prize
  Ticket 1, Mystery Box 2.
- Tables: Cave Carrot 1, Moss 1, Salmonberry 3, Blackberry 10, Tea Leaves 4, Pickles 4, Oil of
  Garlic 7, Jack-O-Lantern 11; fruit tree fruit Orange, Peach 5, Apple, Pomegranate 9, Apricot,
  Cherry 13 (second year), Banana, Mango 13; Oasis-seed crops Cactus Fruit, Beet 11, Rhubarb,
  Starfruit 13; Winter Root, Snow Yam 13. Strawberry 3 (Egg Festival), Sweet Gem Berry 13.
- Crops: first season's first week plus growth weeks, never past the season's last week.
  Forage: first spawn week plus location week.

## Open decisions for Jeff

1. Keep or remove the Spring foothold (mechanism 4).
2. Goal ceilings 5/5/5/6 or 4/4/4/5 (a goal-completing player's Winter is two themes wide).
3. Every judgement number above; trash first.

## How to verify

- Tests: `dotnet test tests/TheLongestYear.Tests` (1514 green at 0.16.84).
- Deploy: `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`, then
  `git checkout -- test-output/log-archive`. Load the Rodger throwaway save (`None_*`, read the
  folder name from `%APPDATA%\StardewValley\Saves`; never PuffPuff_* or Cheatside_*).
- Sims: `bash tools/sim-year.sh goals <label>` and `bash tools/sim-year.sh minimal <label>`,
  never overlapping, never task-stopped (kill the script process and redeploy instead). Each
  ends by writing `docs/board-availability.md`; read the Unknown items section and the rows
  whose basis says "for Jeff to confirm".
- Everything goes through the SMAPI bridge; no mouse, keyboard or foreground tools
  (`docs/HEADLESS_DRIVING.md`).
