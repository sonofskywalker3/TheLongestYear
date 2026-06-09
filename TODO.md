# The Longest Year — TODO

Ongoing scratchpad for design / feature ideas captured during playtesting.
Items here are NOT yet planned; they need spec'ing before execution.
Once an item is planned, it moves into `docs/superpowers/plans/`.

## Open

### 🐞 INVESTIGATE — beta bug/UX reports (re-scrape 2026-06-08)
*Third scrape (Reddit 53 / Nexus 19). Concrete things to investigate, highest-value first.
New 2026-06-08 reports are tagged **[3rd scrape]**:*

**Fixed 2026-06-09 (4th scrape — khauser13 08 Jun 4:50PM + emmainthealps 09 Jun), bound for the v0.10.0 release:**
- **✅ FIXED v0.9.38 — Mine elevator did not lock on loop reset.** *khauser13*: "could still get down to
  floor sixty and I didn't buy the elevator unlocks." `WorldResetService` cleared only
  `LowestMineLevelForOrder`, but `MineShaft.lowestLevelReached` falls back to `LowestMineLevel` (never
  reset) + `deepestMineLevel` was only bumped up. Now all three pin to the kept floor (cap-not-grant).
- **✅ FIXED v0.9.43 — Weekly goal "Large Egg" didn't say which color it wanted.** *khauser13*: "needed a
  large brown egg, the white egg didn't count." Investigation (decompile + live save) confirmed the two
  large-egg slots in the Animal bundle are **vanilla** (6 animal products / need 5; 174 white + 182 brown
  are distinct CC items) — NOT a TLY bug, and not interchangeable. Tried an equivalence fix (v0.9.39-40,
  reverted); the right fix is to **name the color** in the quest log: `ResolveDisplayName` appends
  "(Brown)"/"(White)" for 174/182/176/180. Matching stays exact-id (vanilla-faithful).
- **✅ FIXED v0.9.41 — Stale vanilla "Rat Problem" quest appeared in a run.** *khauser13 / niki_m_m3*.
  `RatProblemQuestPatch` prefixes `Farmer.addQuest` to skip id 26 mid-run + strips it from existing
  saves on load; gated on `RunActivation.IsActive`.

### 🔧 Tech debt — consolidate the three reset paths (found 2026-06-09)
There are **three** near-identical "reset world → BeginNewRun → persist → present week-1 offer"
sequences, each maintained by hand:
- `RunController.ContinueAfterResetSpend` — the real loop reset (fail-day-28 / win→new-loop / `tly_failreset`).
- `RunController.ApplyKeepPlaying` — the win→keep-playing branch.
- `ModEntry.FullResetAndPresentOffer` — the debug `tly_reset` / `tly_resetif` path.

Every cross-cutting fix has to be applied to all three and one always gets missed: the JP-refund guard
and the double-pick `OfferPresentedWeek` re-save (v0.9.25) both landed in `ContinueAfterResetSpend` but
NOT in `FullResetAndPresentOffer` (double-pick on `tly_reset` caught 2026-06-09, patched v0.9.44). They
also diverge behaviorally — the debug path calls `PresentOffer()` directly while the real path calls
`DoDayStartSeasonAndHub()` (season setup + more), so **`tly_reset` is not a faithful stand-in for a real
reset** (this muddied the v0.9.38 mine-elevator test). **Fix:** extract ONE shared "finalize reset"
routine (reset → drain picker → BeginNewRun → clear effects → save → ForceFullSave → day-start hub →
re-save offer marker) and route all three callers through it, so fixes land once and `tly_reset`
exercises the real path. **Refactor — do it AFTER the v0.10.0 release** (no refactor mid-bugfix).

- **✅ FIXED v0.9.28-29 — Bus-repair (Vault) goal renders inconsistently in the Season Goals menu.**
  User (2026-06-08): "the bus repair in the season goals is completely different from all the other
  goals — make it consistent." Restyled the vault/bus-repair entry in `SeasonGoalsMenu.cs` into a
  real list row matching the bundle goals (commits `dd41590` + `e161727`).

- **✅ FIXED v0.9.26 (confirmed) — Vault bundle indices were wrong on REMIXED saves (gate could never satisfy).**
  Now derived from live `BundleData` (room == "Vault"), remix-aware — verified via the v0.9.27 load
  diagnostic (`Vault bundles: 23=2,500g … 26=25,000g`). `VaultBundleMap` is the single source of truth;
  `VaultPaymentSync`/`DonationObserver`/`DonationService` all read it. Original investigation below:

  **🔴 INVESTIGATE — Vault bundle indices may be wrong on REMIXED saves (gate could never satisfy).**
  Found 2026-06-08 inspecting a live remixed save: the Vault money bundles are at indices **23–26**
  (`Vault/23`=2500g … `Vault/26`=25000g), but `VaultRules` hardcodes **34–37** (those are actually
  Bulletin's Dye/Fodder + Joja's "The Missing" in this save). Consequences if confirmed: `IsVaultIndex`
  rejects 23–26 → `DonationObserver` misclassifies a real vault payment as a normal bundle completion
  (`OnBundleCompleted`, never `OnVaultBundlePaid`) → `VaultBundlesPaid` stays empty →
  `IsVaultGateSatisfied` is false → the season gate can NEVER pass (unless `keep_bus_unlocked`). And
  `VaultPaymentSync.Reconcile` checks `isBundleComplete(34..37)` = the wrong bundles. **Verify:** is
  34–37 only correct for NON-remixed bundles (vanilla renumbers the vault under Remixed)? TLY
  *recommends* remixed, so this would hit the recommended config. If real, derive the vault indices
  from the live `BundleData` (room == "Vault") instead of hardcoding. Worked around in the 2026-06-08
  playtest with `tly_payvault spring` (count-based, index-agnostic). Confirm against a non-remixed save.

- **✅ FIXED v0.9.25 (confirmed) — Loop reset presented the weekly theme offer TWICE; first pick discarded.**
  Added a second `_store.Save()` right after `DoDayStartSeasonAndHub()` in `ContinueAfterResetSpend`
  (`RunController.cs`) so the deferred reload reads `OfferPresentedWeek` as set and the day-start guard
  skips the re-present. Playtest `tly_failreset` (Run 17): exactly one offer + one Selected line, pick
  stuck. Original diagnosis below:

  **🔴🔴 TODO (FIX NEXT) — Loop reset presents the weekly theme offer TWICE; first pick is discarded.**
  **CONFIRMED in playtest 2026-06-08 via `tly_failreset`** (picked Farming → forced Fishing/Mixed →
  Fishing overwrote it). **Safe fix confirmed:** the reset-time hub SURVIVES the reload (player picks
  from it), so persist `OfferPresentedWeek` BEFORE the deferred reload — add `_store.Save()` right
  after `DoDayStartSeasonAndHub()` (`RunController.cs:~368`) so `MetaStore.Load()` reads the marker as
  set and the day-start guard skips the re-present. No "no-picker" risk (hub survives). See
  HANDOFF-2026-06-08. Details below:
  Found 2026-06-08 during playtest (Summer 28 fail → Spring 1). The reset opens the Week-1 hub, but
  `OfferPresentedWeek` is set in `DoDayStartSeasonAndHub()` (`RunController.cs:368`) AFTER the
  post-reset save (`_store.Save()` / `ForceFullSave()` at 365-366), so the deferred
  `SaveLoaded → MetaStore.Load()` reloads `Run` with `OfferPresentedWeek = -1` and the day-start
  guard re-presents the offer. Because the first pick is now in `SelectedThemesThisMonth`,
  `SelectionService.OfferForWeek` re-rolls a different pair, so the **second pick overwrites the
  first** (log: picked Farming, then forced to pick again → Fishing). **NOT caused by the v0.9.19-21
  fixes** (those are all day-ending code); pre-existing, fires on every reset.
  **Fix carefully:** the naive "re-save after presenting" risks the OPPOSITE failure (no picker at
  all) if the reset-time hub is closed by the reload — first VERIFY whether that hub survives the
  reload, then either persist `OfferPresentedWeek` before the deferred reload OR present the offer
  only on the post-load path (once). This offer flow has a history of the "no picker" regression
  (see the win→keep-playing comments at `RunController.cs:714-719`). Confirm with a real reset.

- **🔴 Day-28 loop gate is unreliable — wrong reset/advance + JP-spend menu flashes away. [3rd scrape]**
  Two reports, opposite symptoms; root-caused 2026-06-08 to TWO defects (not one):
  - ✅ **FIXED v0.9.20** — *khauser13 (Nexus)*: **completed every** donation goal but it **reset to
    Spring instead of advancing**. Cause: the item-donation ledger was observer-only (live
    JunimoNoteMenu watcher) and could miss a deposit, so the gate read "failed." Added
    `ItemDonationSync` (item analogue of `VaultPaymentSync`) to reconcile the ledger from vanilla CC
    slot state at day-end before the gate eval.
  - ✅ **FIXED v0.9.37 (confirmed faithful repro 2026-06-09)** — *emmainthealps (Nexus)*: **failed** the
    28th yet the JP-spend menu flashed away and the game **advanced to Summer with progress intact**.
    The earlier "whole-CC-completion / `eventUp`" theory was WRONG. Real cause: finishing **just the
    Vault** on day 28 queues `ccVault` in `mailForTomorrow`, so that night vanilla plays the bus-repair
    **`WorldChangeEvent(7)`** — a **`Game1.farmEvent`, which is a SEPARATE flag from `eventUp`** and
    runs with `newDay==false` AND `eventUp==false` but `farmEvent!=null` (`Game1.cs:9340` clears newDay
    before `:9361` assigns farmEvent). TLY's day-28 driver + `MenuLauncher` guarded only on `eventUp`,
    so the shrine opened DURING the bus scene and the event's end-of-play warp (`Game1.cs:4977-4989`)
    tore it down without firing `exitFunction` → reset dropped. (That's also why the prior
    "defer until `!eventUp`" never fired — wrong flag.) **Fix, 3 parts:** (1) FAIL loop strips the
    rewind-doomed CC mail at day-end (`SuppressResetDoomedRoomScenes`) so the bus scene never plays;
    (2) day-28 driver + launcher now also wait on `Game1.farmEvent` for the PASS path; (3)
    `PerformReset` purges CC mail from `mailForTomorrow` (fixes the "bus fixed, 0 bundles done"
    carryover). Log proof: `Fail loop: suppressed 1 reset-doomed CC restoration scene ([ccVault%&NL&%])`
    with `farmEvent=none` through the reset. **PASS path also confirmed** (2026-06-09): with the gate
    passed on Spring 28 + Vault done, the log shows `Day-28 cutscene: deferring the Continue scene until
    the overnight FarmEvent (WorldChangeEvent) finishes` → opens 14s later with `farmEvent=none` → clean
    advance to Summer. Both branches verified.
- **✅ FIXED v0.9.19 — Kept smoked/preserved fish loses its inner-fish identity through the carry chest.
  [3rd scrape]** *emmainthealps (Nexus)*: a **Smoked Legend** carried back as a blank 57g smoked fish.
  Cause: the Junimo Stash serialized items to a lossy (ItemId, Quantity, Quality) record. Now captures
  + re-applies `preservedParentSheetIndex` / `preserve` / `price.Value` (covers all flavored goods —
  wine, jelly, aged roe, honey, bait, …). *Known remaining gaps: weapon enchantments/forged gems and
  colored-item tint not yet round-tripped — log if reported. Verify via a value-preservation playtest.*
- **✅ CONFIRMED NOT A BUG (2026-06-08) — "Keep tool upgrades" missing from the JP purchase screen.**
  The earlier diagnostic caught a *grant artifact* (a granted duplicate pickaxe in the bag, which
  `ToolLevel` resolved to the L0 copy). A real Clint upgrade replaces the tool in place, so the keep
  rows show in BOTH the planner and the spend menu and persist into the next run — faithfully retested
  2026-06-08. khauser13's report does not reproduce on a genuine upgrade. Original notes below:

  **⏳ "Keep tool upgrades" missing from the JP purchase screen. [3rd scrape]** *khauser13 (Nexus)*: shows
  in the planner but not the spend menu. **Root-caused 2026-06-08 — NOT catalog drift** (both menus call
  identical code). The tool-keep rows carry a `tool:<kind>:<tier>` reach requirement read LIVE from
  `Game1.player.Items` at menu-build time; the spend menu opens at the fragile day-28 morning where the
  tool may be absent/perturbed → reach reads 0 → rows filtered out. **Fix (pending, same boundary-race
  area as the emmainthealps half above):** gate the boundary reach on a pre-wipe peak snapshot
  (`WorldResetService.CapturePeaks`) instead of live inventory. Confirm with the same day-28 log.
- **~~Weekly task can request an already-donated item → penalty locked for the whole week.~~ BY DESIGN
  (user decision 2026-06-08). [3rd scrape]** *emmainthealps (Nexus)*: weekly tasks may ask for items
  already donated, locking the penalty for the week. **Intended** — part of the challenge; not a bug.
  No change.

- **✅ ADDRESSED — JP-spend confusion (Dusklight7 / TheFirstBanana / emmainthealps).** The planning
  view (`ShrinePreviewMenu`) already prints, near the top, "Planning view — you spend JP when a loop
  resets or you win, not here." That's the wording the author told testers was "in place." Ships in
  this release. (Further clarity will come from the Junimo intro in the story update.)
- ~~**Weather/luck desync (*u/Tutorem*, day 6).**~~ **INVESTIGATED 2026-06-07 — not an internal bug.**
  Traced the 1.6 flow: the in-game TV (TV.cs:326/589) AND the actual applied weather both resolve
  through the patched `getWeatherModificationsForDate` (Game1.cs:9594 → Default LocationWeather →
  `UpdateDailyWeather` sets real IsRaining), so both land on the same deterministic
  `WeatherScheduler` value per date — internally consistent. Vanilla's nightly "tomorrow" re-roll is
  overwritten by the override next cycle. The only thing that disagrees is the **external predictor
  tool** (recomputes from vanilla RNG, unaware of the mod) — user doesn't care about it. Day-3 rain
  intentionally removed by the scheduler; luck untouched (FarmerReset zeroes only the defunct Luck
  *skill*); 0 rain by day 6 is fine (≥2/season guarantee, not early). No fix. *Optional: confirm
  empirically from one playtest log (TV forecast at night vs next-day actual).*
- **✅ FIXED v0.9.21 (NPC routing) — CC reads as "restored" from day 1 — NPCs route into it (3 reports).**
  *u/Tutorem*: "needed Clint on day 5… he went to the CC instead." **[3rd scrape]** corroborated by
  *dm_me_your_kindness (Reddit)* (Granny/Gus/Clint) and *khauser13 (Nexus)* ("townspeople entering the
  community center… confused me"). Root cause: `CcLocationAccessiblePatch` forces
  `isLocationAccessible("CommunityCenter")` true for the player's door, but vanilla NPC scheduling reads
  the SAME flag — so it un-cancelled every villager's CC schedule. Fix: `NpcCcScheduleStayOutPatch`
  postfixes `changeScheduleForLocationAccessibility` to always cancel the CC entry during a TLY run
  (villagers use their default schedule), door stays open for the player. **Confirm villagers stay out
  via playtest.** *Note: the "looks visually restored" reports are NOT a TLY-flag bug — restoration
  keys on `areasComplete[]`, which TLY never sets; a player who finishes bundles sees genuine vanilla
  restoration. The NPC-dialogue idea (feedback-triage) is moot now that they stay out.*
- **Double-forage buff feels weak + double-XP question (*u/Tutorem*).** Buff "probably worthless past
  week 1-2 unless it affects truffles"; also asks whether double-forage grants double XP. Balance +
  a behavior question to answer.
- **✅ VERIFIED CLOSED — Greenthumb without purchase.** *khauser13 (Nexus, 05 Jun)*. `GreenThumbPatch`
  is correctly gated (`UpgradeChecker.GetTier("green_thumb",5)==0 → return`), so the mod's passive never
  fires unpurchased; the report was the day-1 junimo-notes-unlock gate (fixed alongside the bulletin
  board, v0.9.5). khauser played extensively on later versions (07-08 Jun) without re-reporting it.

### 📣 Community feedback triage (beta, 2026-06-06) — ideas/inspiration (replies are the user's)
*Mined from the r/StardewValley beta thread (1txuhfb) + Nexus mod 47192 posts.
**Replies are the user's to write** — idea/inspiration capture with attribution only.
Already-captured elsewhere: u/dcempire's "give the CC purpose after completion" → `mod-ideas.md` #3;
u/Khajiit-ify→Emmalution and u/petraliten→Poxial → `marketing/youtuber-outreach.md`; u/Gribbleby's
déjà-vu → the [1.0.0] entry below. Remaining items:*

- **Balance — early difficulty may be too low.** *u/Tutorem*: CC is "very doable in Y1" (often done by
  early Fall with seed-picking/resets); worried the challenge is soft at the start. Watch during the
  difficulty-tuning pass.
- **Balance — Traveling Cart RNG.** *u/jneedham2*: a lucky Cart buy (red cabbage / truffle / sandfish)
  can trivialize a run. TLY currently does nothing with the Cart; author is open to revisiting if it
  becomes the dominant win path. Decide whether to constrain/handle the Cart.
- **Compatibility — big-CC-content mods.** *ErraticPixel (Nexus)*: how does the 1-year gate interact
  with CC-overhaul mods whose bundles need >1 year to finish? Also asked about mid-save install
  (the per-save dormant gate covers that now). Worth a documented compat stance for large-CC mods.
- **Cutscene presentation.** *Dusklight7 (Nexus)*: the opening cutscene should show ALL the talking
  Junimos, not just the one recolored sprite. Fold into the cutscene overhaul above.
- **NPC-in-CC dialogue (turn the bug into flavor). [3rd scrape]** *khauser13 (Nexus)*: if townsfolk are
  going to be in the (abandoned) CC, give them dialogue explaining what they're doing there. Secondary
  to actually fixing the schedule routing (see the 🔴/CC-restored bug above) — capture as flavor only.
- **Design inspiration (reference, not a request).** *u/jneedham2*: vanilla "Prank Grandpa's Ghost —
  Glorious Victory" challenge (complete the remixed CC in five seasons) as a kindred framing.
- **Community art offer.** *triangulummortis (Nexus)*: offered a drawn banner / fan art; connected via
  Discord (Sonofskywalker3). No action needed beyond the user's own follow-up.

### ☆ TODO: brainstorm + write the "one-continuous-save trilogy architecture" spec
*Captured 2026-06-06. User decision: TLY1/2/3 all run **continuously on one save** (one evolving
campaign, not three independent runs/mods). This is a SEPARATE design from the story/cutscene pass —
needs its own brainstorm → spec. **User explicitly asked to be reminded to do this — surface it; don't
let it slip.*** Scope to cover:
- Save continuity spanning three "years"/stages; a year/stage state machine and how you advance TLY1→2→3.
- **Escalating win bar:** TLY1 = restore CC; TLY2 = CC + (if too easy) basic Perfection; TLY3 = ultimate Perfection.
- A **new layer of Junimo upgrades each year** to keep pace with the higher seasonal goals.
- How TLY2 (Ginger Island / Joja resort) and TLY3 (valley annexation + Morris redemption at Perfection) hang off it.
- Companion to the story brainstorm notes at
  `docs/superpowers/notes/2026-06-06-story-cutscene-brainstorm-notes.md`.

### ★ NEXT NON-BUG-FIX UPGRADE: animated loop cutscene + real ending cutscene
*Captured 2026-06-05. User-flagged as the priority once bug fixes are clear —
the next feature upgrade, not a polish afterthought.*

Two distinct cutscene pieces:

1. **Animated loop (reset) cutscene.** What we have now is *OK but static* — the
   user wants it **animated, not a still frame**. This is the transition the
   player sees when a loop resets (Winter 28 → next Spring 1). Make it feel like
   the year actually rewinding rather than a placeholder card.

2. **Real ending / victory cutscene.** The current 0.9 `VictoryMenu` is a
   placeholder (see the deferral note below + the `VictoryMenu` class comment).
   The real 1.0 ending should be a proper cutscene that shows:
   - **Joja giving up and closing the store** — the narrative payoff for
     restoring the CC and beating the loop.
   - **A Junimo party / celebration** (or similar) — the joyful button on the
     whole run.

Ties together with the already-deferred items below: the "Win screen → JP shrine
transition is jarring" entry explicitly defers transition polish into *this* real
ending work, so fold them together when this gets spec'd. Not yet spec'd —
needs an event-script design pass (custom `Data/Events`, Junimo sprite reuse from
`Characters/Junimo`, Joja-store staging at JojaMart).

### [1.0.0] Déjà-vu villager dialogue — meta tracks (but doesn't preserve) relationships
**Source / credit: u/Gribbleby** on the r/StardewValley beta announcement thread
(https://www.reddit.com/r/StardewValley/comments/1txuhfb/ — 98 upvotes, 20k+ views). Their seed:
*"I assume relationships will also be reset? If somehow the villagers retained some memory it could
make for some fun Groundhog Day dynamics!"* — **credit u/Gribbleby if this ships.** (The specific
example lines below were the author's elaboration of that idea.)
*Captured 2026-06-05. Not yet spec'd. Corroborating interest 2026-06-06: **wolfseas** (Nexus) also
asked how heart events behave across loops — a second data point that relationship-across-loops is a
wanted direction.*

The loop wipes friendship every reset (villagers don't remember you) — but the **meta layer should
silently track cumulative interaction** per villager across loops, *without* preserving the actual
heart/relationship level. Once cumulative interaction with a villager is **significant**, occasionally
intercept their conversations to inject a faint subconscious-familiarity line — the loop bleeding
through. Examples the commenter gave:
- *"I swear we've met before — do you have a twin?"*
- *"I don't know why, but I feel very comfortable with you."*

Why it's great: it's the perfect thematic payoff for a time loop — the villagers can't *remember*, yet
something *lingers*. Rewards long-term players narratively without giving a mechanical head-start
(hearts still reset, so no day-1 gifting/marriage exploit).

Design seeds (needs a real spec):
- **New MetaState field:** per-villager cumulative-interaction counter (talks + gifts + heart events
  summed across *all* loops). This is the only thing that persists — the live `friendship` value keeps
  resetting via the existing reset path. Explicitly do NOT preserve hearts (same boundary as the
  barn-animal upgrades: track the meta, reset the mechanical level).
- **"Significant" threshold** gates eligibility for the déjà-vu lines (tune so it kicks in after a
  villager you've genuinely invested in across several loops, not someone you said hi to once).
- **Injection:** low random chance to prepend/substitute a déjà-vu line when an eligible villager
  starts a conversation — via a `Dialogue`/`NPC.CurrentDialogue` intercept or a `Characters/Dialogue/<name>`
  asset edit. Keep it **rare** so it stays uncanny, not spammy.
- **Line pool** (start with the two above, add more); could escalate tone with the cumulative counter
  (mild "have we met?" → warmer "I trust you for some reason").
- Keep it mysterious — never explain the loop in these lines; that's the intro/Junimo's job.

### Win screen → JP shrine transition is jarring (defer to the real 1.0 ending)
Playtest 2026-06-05: dismissing the 0.9 `VictoryMenu` cuts straight into the
JP shrine store with no easing — visually abrupt. **Deliberately deferred** —
the 0.9 win screen is a placeholder; the elaborate payoff cutscene is a 1.0
item (see `VictoryMenu` class comment). Fold the transition polish into that
real-ending work rather than patching the placeholder. No fix needed for the
0.9.x beta.

### Small playtest carryovers (from STATUS.md)
Picked up during the 2026-05-29 audit; STATUS.md was stale (last update
2026-05-27) so these were drifting:

- ~~**Festival exit to host map**~~ — closed 2026-05-29. The TODO
  entry described a behaviour that conflicted with what was already
  shipped. `FestivalTimeFlow.cs` (Plan 06A) handles festival exits
  end-to-end via `SkipExitFestivalPromptPatch` (skips the "are you
  ready?" prompt and runs `forceEndFestival` directly) +
  `EndBehaviorsPatch` (preserves `timeOfDayAfterFade` to the actual
  in-game time at exit, undoing vanilla's hard-coded 2200 jump).
  Result: walk into a map-edge warp → festival ends silently, player
  lands at the Farm porch, time stays whatever it was when you walked
  out — which is the behaviour the user identified as the desired one:
  "I'd walk out it would work me to the farm it would be the same time
  it was when I walked out and I could walk all the way back to town
  and be at the festival again." Briefly tried an alternative (host-map
  exit / direct warp through edge / two-patch combo) in `b49bf5b`; the
  user pushed back to restore the FestivalTimeFlow behaviour, so
  reverted. No new patch needed.
- ~~**Indicator `?` source rect**~~ — closed 2026-05-29. User feedback:
  "you never got the indicator right, so just remove it and close it."
  `IndicatorRegistry` deleted; `Dismiss` calls in CookbookMenu /
  CraftbookMenu / JunimoStashShowMenuPatch now write directly to
  `MetaState.DismissedIndicators` to preserve the one-time intro-quest
  gating. `WorldResetService.RegisterIndicators` + the SMAPI
  RenderedWorld hook + `JunimoStashService.RegisterIndicator` all gone.
- ~~**`forage_off` over-suppression (JC-4)**~~ — closed 2026-05-29.
  User: "it's not an issue." Current behaviour (Mining liability also
  blocks weeds/stones overnight via spawnObjects) stays.
- ~~**`fortune_rare_fish` is a 0.75× bite-rate multiplier (JC-2)**~~ —
  stale note, closed 2026-05-29. The 0.75× bite-rate wiring was already
  replaced by the Curiosity Lure piggyback in `FishRareLurePatch` per
  the 2026-05-28 audit. The "true rarity intercept" follow-up is the
  canonical implementation by design — Stardew has no abstract "rare
  fish" concept, rarity lives inside per-spawn `SpawnFishData.GetChance`
  thresholds (GameLocation.cs:13797). Curiosity Lure IS vanilla's
  rare-fish boost pathway; any further rewire would require
  reimplementing the spawn table. See expanded comment block on
  `FishRareLurePatch` for the full design rationale.

### (closed — moved here from "Open" 2026-05-29 audit)
### ~~Continue-after-victory mode~~ — SHIPPED 2026-05-29 as `5959de0`
Source: 2026-05-29 playtest spec. After the win condition fires (CC restored,
year complete, all bundles), the player should have the option to keep
playing the same run instead of being forced into a reset. Currently the
reset-trigger fires automatically at year-end on a completed CC.

Implementation notes:
- New flag in `MetaState` or `RunState` — `VictoryAcknowledged` — set when the
  player picks "continue" on the post-win screen.
- `WorldResetService` checks the flag before scheduling a reset; if set, the
  current run keeps going indefinitely (next month, next season, no roll-over).
- Acknowledgement UI: the existing JunimoShrineMenu or a one-off "you won"
  modal with "New loop" / "Keep playing" options.
- The player can still trigger a manual reset later via the shrine — the
  acknowledgement isn't permanent, just defers the auto-reset.
- JP banking can keep accruing during the continued run; donations after win
  still award JP at the usual season-multiplier, no special bonus.
- **JP-spend dialog at the end of the win scene.** Whether the player picks
  "New loop" or "Keep playing", surface the same Junimo Shrine purchase menu
  one more time so they can dump their banked JP on whatever upgrades they
  want active for the infinite run (or for the next loop) before the choice
  finalises. Reuses the existing `JunimoShrineMenu` — no new UI to design.
  Important: the menu has to fire AFTER the victory cutscene is fully closed,
  not stack on top of it, or controller focus + drawing layer get fighting.
- **JP-spend dialog ALSO pops on every natural loop reset** (Winter 28 → next
  Spring 1). User clarification 2026-05-29: "it's going to pop when you reset
  the loop or when you complete it, that's it." Same menu, two trigger paths.
  Important: must fire BEFORE `WorldResetService.PerformReset` commits, since
  the reset zeroes run-state (but MetaState.JunimoPoints survives the reset,
  so the spending CAN happen here — the constraint is purely UX, not data).
- **~~Remove the in-world JP shrine tile interactable.~~** Audited
  2026-05-29: no tile interactable was ever shipped. Plan 05 docs reference
  it as a design intent, but `JunimoShrineMenu` is only opened by
  `MenuLauncher.OpenShrineShop()`, which is in turn only called by the new
  reset/win popup paths and by the `tly_openshop` debug command. No tile
  removal needed.

Status: spec'd, not planned. Tagged as v1.x polish (the auto-reset isn't a
blocker — the player can manually save before the auto-reset hits if they
want to keep their post-win state preserved on a backup save).

### ~~Co-opted day-1 intro cutscene (replaces vanilla 191393)~~ — SHIPPED 2026-05-30 as `85b029b`, PENDING PLAYTEST
Shipped this session: `tly_intro_porch` (Lewis on the Farm porch, Joja
threat + Winter 28 deadline + landmark protection + hands over key) and
`tly_intro_cc` (Junimo loop-explainer inside the CC), injected via
`IntroEventInjector` asset edits and prepended to win first-match. Per-run
mail-flag chaining + cross-run `MetaState.HasSeenIntro` (set in OnSaving,
promoted to `tly_intro_done` on every load) gate the events. Retest via
`tly_replayintro` + `tly_reset`. Dialogue is one-pass, unreviewed — polish
deferred unless the user comments. Original spec preserved below for history.

(original spec)
Source: 2026-05-29 playtest. User saw vanilla event 191393 (Demetrius +
Lewis CC intro) fire on Spring 5 of a TLY loop. Suppressed for now via
`EventSuppressionPatch` (returns `-1` from `checkEventPrecondition` for
the 191393 key). The eventual replacement is a TLY-specific intro that
RE-USES the 191393 staging (Lewis at Town near the CC) with new
dialogue:

1. Plays the FIRST time on a new save, on day 1 — **before** the
   weekly-theme picker opens. (Currently the picker opens immediately
   on `SaveLoaded` if it's a new run.)
2. Lewis explains the Joja takeover threat in TLY terms (the year-loop
   stakes — Junimos rewinding the year if the CC isn't restored).
3. Lewis walks off; the player walks into the CC; a Junimo pops up to
   explain the loop mechanic (themes, donations, Junimo Points).
4. Must fire on a new save **even if the player skips the intro on the
   first try** — track a `MetaState.HasSeenIntro` flag, set only after
   the intro completes OR after the picker is shown post-intro.
5. Skippable on first run (vanilla `Esc` / B). Auto-skipped on every
   subsequent loop (the meta-state flag is preserved across resets).

Implementation surface:
- New cutscene script in a custom `Data/Events/TLYIntro` or appended to
  Town events.
- Hook `OnSaveLoaded` (existing TLY entry point) to play the intro
  before the picker the first time only.
- `WeeklyThemeQuestService` should know to wait for the intro to finish.
- Junimo NPC sprite already in `Characters/Junimo` (used by hub menu);
  reuse for the loop-explainer beat.

Status: spec'd, not planned. Will be one of the v1.1 narrative tasks.

### ~~JP upgrade: `keep_pet`~~ — SHIPPED 2026-05-29
See Resolved section below. Cost landed at 75 JP, sentimental tier.

### (closed) JP upgrade: `keep_pet` — pet persists with hearts
Source: 2026-05-29 playtest. New JP upgrade in the Animals / Buildings
category that preserves the player's pet (cat / dog / turtle) AND its
friendship hearts across loops, so a long-tenured pet stays maxed out
between runs.

Implementation notes:
- Pet is a `Pet` instance hanging off `Game1.player.activePet` (or the
  per-Farm `Farm.characters`). On reset (`loadForNewGame`) the pet is
  typically wiped along with the rest of the world.
- Need to snapshot in MetaState: pet kind (which species), name, water
  bowl state, and `friendshipTowardFarmer.Value`.
- On reset, re-instantiate the pet of the saved kind, set hearts, place
  in the farmhouse / on the porch the way vanilla day-1 adoption does.

**Critical contrast — barn/coop animals (the existing `keep_*_animal`
upgrades) must continue to start each loop at 0 hearts.** User spec:
"the 'keep 1 cow' should still start over with 0 hearts so they can't
be getting large milk day 1. same for all barn/coop animals." The
existing `WorldResetService.ApplyStartingAnimals` builds fresh
`FarmAnimal` instances each reset (friendshipTowardFarmer defaults
to 0), which already matches this requirement — but call this out in
the `keep_pet` design so future cleanup doesn't accidentally unify the
two paths and start propagating animal hearts too.

JP cost ballpark: 50–100 JP. User: "they can't do much for a run, it's
mostly for feelings." Pet doesn't gate a measurable progression vector
(no Large Milk, no shipping value) so the cost should reflect that
sentimental-only payoff rather than a typical run-saver price.

Status: spec'd, not planned.

### ~~JP upgrades: keep kitchen / keep basement / keep shortcuts~~ — SHIPPED
Audited 2026-05-29: all three are wired end-to-end. Catalog entries:
`keep_kitchen` (800 JP), `keep_basement` (1800 JP, requires keep_kitchen),
`keep_shortcuts` (900 JP). Effects:
- `RunBaselineBuilder` reads them into `KitchenOnDay1` / `BasementOnDay1`
  / `ShortcutsUnlocked`.
- `FarmerReset` forces `HouseUpgradeLevel = 1` or `3` accordingly.
- `WorldResetService` step 7b adds the `communityUpgradeShortcuts` mail
  flag (vanilla reads it in Forest/Mountain/Town/Beach for the five
  shortcut tile overrides). Step 7c creates the `Cellar` location for
  L3-house resets so the FarmHouse warp doesn't dead-end.

(Original spec preserved below for design history.)

### (original spec, kept for design history) JP upgrades: keep kitchen / keep basement / keep shortcuts
Source: 2026-05-28 playtest. User correction after a first-pass sketch
that bundled all Robin-related kept-state into one upgrade: "NO don't
bundle robin's upgrades, I want one for keeping the kitchen, one for
keeping the basement, and one for keeping the shortcuts, that's it."

Three separate JP upgrades. All three are independent of CC completion
(Robin sells them for gold in vanilla without any CC dependency).

**1. `kept_kitchen`** — preserve farmhouse upgrade level 1 across runs
   - Vanilla: 10,000g + 450 wood, 3-day build. Adds the kitchen room
     (cooking + fridge) and bumps `Game1.player.HouseUpgradeLevel` 0→1.
   - Reset behaviour: `FarmerReset` currently wipes HouseUpgradeLevel
     back to 0 every run. When this upgrade is owned, skip that wipe
     (or restore L1 after `loadForNewGame` rebuilds the FarmHouse so
     `resetForPlayerEntry` lays out the kitchen-tier interior).
   - The current cookbook unlock (`cookbook_1`) needs review for
     interaction — cookbook is meta-state, kitchen is run-state, but
     the player would expect both to feel "I have a kitchen this run."

**2. `kept_basement`** — preserve farmhouse upgrade level 3
   - Vanilla: 100,000g, requires L2 first. L3 adds the cellar (basement
     with 33 cask slots — the aging infrastructure for wine/cheese).
   - This upgrade should imply L2 (kids' room) as a side effect since
     L3 can't exist without L2 in vanilla data — the menu shouldn't
     even offer `kept_basement` until `kept_kitchen` is owned.
   - On reset, restore HouseUpgradeLevel = 3 (or use the highest owned:
     3 if `kept_basement`, else 1 if `kept_kitchen`, else 0).

**3. `kept_shortcuts`** — preserve Robin's 5 map shortcuts (one upgrade
   for all five, NOT five separate upgrades — user spec)
   - Vanilla: each shortcut purchased separately from Robin
     post-`Mountain_Shortcuts_Spoke_Robin` mail flag.
   - The five shortcuts (1.6):
     - Forest south fence → south Town path
     - Bus stop tunnel north
     - Forest tree stump bridge → Backwoods
     - Mountain → quarry path
     - Mountain → Town side route
   - Each unlocks via a mail flag like `OpenedTreeStumpShortcut` plus
     a passable-tile property toggle on the Mountain/Forest map.
     Need to verify exact flag names against the 1.6 PC source —
     check `Mountain.cs`, `Forest.cs`, `Town.cs` for `mailReceived.Add`
     calls keyed to shortcut tile properties.
   - On reset: `WorldResetService.PerformReset` re-adds all five mail
     flags after `loadForNewGame` (similar pattern to `landslideDone`
     in MountainUnlock).

JP cost ballpark (relative to bus repair = 100 JP):
- `kept_kitchen`: 75 JP (adds cooking ability + fridge — meaningful)
- `kept_basement`: 200 JP (skips two L3 prerequisites + 100k gold)
- `kept_shortcuts`: 100 JP (saves Robin's 15k×5 = 75k gold per run)

Status: spec'd, not planned. Out of scope for the current playtest
batch; queue as its own commit chain.

## Resolved / closed

- **Vault/money gate invisible + unpayable** — fixed 2026-06-06 (v0.9.8–0.9.16, master).
  Spec `docs/superpowers/specs/2026-06-06-vault-payment-gate-design.md`, plan
  `docs/superpowers/plans/2026-06-06-vault-payment-gate.md`. Changes:
  - **Gate reworked to count-based, tier-agnostic:** a season is satisfied when
    `VaultBundlesPaid.Count >= season ordinal` (Spring 1 … Winter 4), any tiers, or
    `keep_bus_unlocked`. Pay all four in Spring → every season pre-satisfied. Replaces the old
    exact-tier match.
  - **Payable in normal play:** `DonationObserver` routes vault-bundle (34–37) completions to a new
    `DonationService.OnVaultBundlePaid`, and `VaultPaymentSync.Reconcile` (vanilla
    `CommunityCenter.isBundleComplete` = source of truth) additively backfills the ledger at
    day-end / journal-open / shrine-open — also covers the mid-run-upgrade migration (already-paid
    bundles have no false→true transition to observe).
  - **JP scales with gold paid** (`JpSettings.VaultGoldPerJp` default 1000 → 2500g=3, 5000g=5,
    10000g=10, 25000g=25), no completion bonus, not season-multiplied.
  - **`keep_bus_unlocked` now needs run-reach `bus:4`** (all four), and the `bus` metric returns
    the paid count — resolves the old `bus:1`-only-via-debug deadlock.
  - **Green journal** (`SeasonGoalsMenu`) shows a pinned per-season "Vault (bus repair): X of N
    paid — MET/NOT MET" line. 462 tests pass. **PENDING: in-game playtest** (Task 11 step 4 of the
    plan) — not yet deployed/tested.

- **Continue-after-victory mode** — shipped 2026-05-29 as commit `5959de0`.
  JP-spend popup pops on both reset AND win paths; post-win choice
  dialog ("Start a new loop" / "Keep playing this run") sets
  `MetaState.VictoryAcknowledged` on Keep, which suppresses the popup on
  subsequent Winter 28 wins. Manual `tly_reset` stays raw (debug path).
  Plan-05 in-world shrine tile was never actually shipped — no removal
  needed.

- **`keep_kitchen` / `keep_basement` / `keep_shortcuts`** — shipped
  earlier; audit 2026-05-29 confirmed all three are wired end-to-end
  (RunBaselineBuilder → FarmerReset HouseUpgradeLevel + WorldResetService
  cellar/mail-flag step). TODO entry above kept for design history.

- **`keep_pet` upgrade** — shipped 2026-05-29 as `PetCarryoverService`
  + `MetaState.PetState` + `PetSnapshot` record. 75 JP, Buildings
  category. Snapshots kind / breed / name / friendship before
  `loadForNewGame`, restores at the Farm porch after starting-animal
  placement, sets the `MarniePetAdoption` mail flag to suppress
  vanilla's day-1 adoption offer. Barn/coop animals still start fresh
  (0 hearts) per spec — only the pet carries hearts.

- **Seed-driven weather scheduler** — shipped 2026-05-28 as
  `WeatherScheduler` + `WeatherModificationsPatch`. Per-season minimums
  (≥2 rain Spring/Fall, ≥2 storm + ≥2 rain Summer, ≥2 snow Winter),
  deterministic from `(uniqueIDForThisGame, seasonIndex)`. Subsumes
  the prior day-3 forced-rain bypass + Summer 13/26 hardcoded storms.
  Commit 14322d4.

- **`tly_wipemeta` debug command** — shipped 2026-05-28 as
  `MetaStore.WipeMeta()` + `CmdWipeMeta`. Replaces State with a fresh
  MetaState() and persists immediately. Commit 61ab125.

- **UX6 — always-on JP HUD** — shipped 2026-05-28 as `DrawJpHud` on the
  existing `Display.RenderedHud` hook. Top-right corner, 2 lines (banked
  JP + active theme + 1.5×/lifted suffix). GMCM toggle. Commit 1a8e2b2.

- **Plan 06 effects layer (UX5)** — ALL ten modifier ids wired with real
  Harmony patches: `forage_yield_up` (ForageYieldPatch), `mines_closed` +
  `mine_drops_up` (MineDropsPatch), `crop_growth_up/down` (CropGrowthPatch),
  `fish_bite_up/down` (FishBiteRatePatch), `forage_off` (ForageOffPatch),
  `all_drops_up` + `all_sell_prices_down` (AllDropsPatch). Liability/bonus
  mapping table preserved in design-spec docs.

- **Weekly Theme Journal entry** — shipped 2026-05-28 as `WeeklyThemeQuestService`.
  Creates a vanilla Quest on theme select with a 4-item checklist; each CC donation
  ticks a box; on completion awards +N JP (season-scaled) and suppresses the week's
  liability via `ActiveEffectsProvider.SuppressLiability`. Bonus stays active.
  Persisted via `RunState.LiabilitySuppressedThisWeek`. Commits 5bdb8f6 + 13776ed.
