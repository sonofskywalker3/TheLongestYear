# The Longest Year — Status

**Last updated:** 2026-08-25 (season pity merged to master as v0.12.19, UNRELEASED, live smoke PASSED)
**Branch:** `master`
**Tests:** 794 passing, 0 failing
**Build:** clean; 0.12.19 deployed to PC Mods for the smoke, game closed
**Last public release:** 0.12.18 (2026-08-25 00:30, fully closed)

## Current state (2026-08-25): v0.12.19 on master, season pity merged, not released

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
