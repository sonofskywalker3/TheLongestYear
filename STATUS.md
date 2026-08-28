# The Longest Year - Status

**Last updated:** 2026-08-27 late (keep wallet items + Stardrops built, 0.16.19 to 0.16.25; live smoke pending)
**Branch:** `master`; 0.16.25 committed locally, NOT pushed, NOT released
**Tests:** 1171 passing, 0 failing
**Build:** clean
**Last public release:** 0.16.17 (SVE smoke finding recorded in TODO.md "SVE board audit")

**Decision 2026-08-27 late (TODO walk with Jeff):** next build is **keep wallet items + Stardrops via
per-item JP keeps at the shrine** (same shape as the book keeps). Brainstorm, then spec, then plan; see
`docs/superpowers/HANDOFF-2026-08-27-wallet-stardrops.md`. TODO headings were caught up with the
0.14.0 / 0.14.2 / 0.16.0 / 0.16.17 releases (1123181, rose1729, difficulty, books, deja-vu all shipped).
The settings screenshots (Features, Difficulty) are confirmed live on Nexus (gallery + description) and
GitHub. The Egg Hunt per-loop guard is still open: 0.14.1 only guards once per DAY.

## Keep wallet items + Stardrops (0.16.19 to 0.16.25): built, unit-tested, LIVE SMOKE PENDING

Spec `docs/superpowers/specs/2026-08-27-keep-wallet-stardrops-design.md`, plan
`docs/superpowers/plans/2026-08-27-keep-wallet-stardrops.md`. Eighteen Carryover rows
(`keep_wallet_*` x11, `keep_stardrop_*` x7, 6,950 JP) reach-gated on `mail:<flag>`,
`event:<id>` or `stardrop_mines`; `RunBaseline.KeptMailFlags` / `KeptEventIds` /
`KeptStardropCount`; `FarmerReset` re-adds the flags after the mail wipe, re-marks kept power
events after the re-seed, and sets max stamina to 270 + 34 per kept Stardrop. Bear's Knowledge
(2120303) and Spring Onion Mastery (3910979) joined `EventGatingTables.Default.ReplayableEventIds`,
so they no longer survive a rewind unless bought (they used to, for free). Debug: `tly_wallet`.
CHANGELOG `## Unreleased` written; README and Nexus description got the Shrine feature line (What's
New waits for the release). Deployed to PC Mods by the Release build; nobody has seen it run yet.
Smoke recipe: plan Task 8.

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
