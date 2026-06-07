# The Longest Year — TODO

Ongoing scratchpad for design / feature ideas captured during playtesting.
Items here are NOT yet planned; they need spec'ing before execution.
Once an item is planned, it moves into `docs/superpowers/plans/`.

## Open

### 🐞 INVESTIGATE — beta bug/UX reports (re-scrape 2026-06-07)
*Second scrape (Reddit 50 / Nexus 17). Concrete things to investigate, highest-value first:*

- **JP-spend confusion (2 reports → fix the UX).** *Dusklight7* + *TheFirstBanana (Nexus)*: clicking
  shrine upgrades does nothing mid-run and players think it's broken ("been so confused why I couldn't
  spend my JP right away"). Working-as-designed (JP store opens only at loop boundaries — reset/win)
  but reads as a bug. **Fix:** make the shrine UI state explicitly that purchases happen on
  reset/win, not mid-run.
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
- **CC reads as "restored" from day 1 (*u/Tutorem*).** The CC looks visually completed at the start
  AND NPC schedules treat it as restored — "needed Clint on day 5… he went to the CC instead."
  Investigate: the day-1 CC-access patch is also flipping the visual/complete state + schedule
  routing. Bundles should be accessible without the CC looking done or rerouting NPCs.
- **Double-forage buff feels weak + double-XP question (*u/Tutorem*).** Buff "probably worthless past
  week 1-2 unless it affects truffles"; also asks whether double-forage grants double XP. Balance +
  a behavior question to answer.
- **POSSIBLE OPEN BUG — Greenthumb without purchase.** *khauser13 (Nexus)*: Greenthumb perk active on
  crops without buying it. Author tied it to the junimo-notes unlock gate (same patch as the
  bulletin-board fix) — **verify it's actually fixed in 0.9.6 or still leaking.**

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
