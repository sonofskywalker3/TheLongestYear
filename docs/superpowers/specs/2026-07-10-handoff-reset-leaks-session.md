# Handoff — 2026-07-10 session (reset-leak audit + green rain + keep_silo + review pass)

**Repo state:** master at `v0.11.30`, all work committed locally (NOT pushed, nothing released).
Tests: 531 passing; mod builds 0 warnings. Deploy to PC Mods pending — the game was running all
session (user's bedtime playtest, then left on); a background watcher builds+deploys HEAD the
moment the game exits. Deployed Mods folder is still 0.11.23 until then.

## What shipped this session (all on master)

| Version | Change | Verify |
|---|---|---|
| 0.11.24 | Reset-leak audit, player side: worn equipment slots (hat/shirt/pants/boots/rings/trinkets) unequipped via vanilla hooks; `specificMonstersKilled` + `chestConsumedMineLevels` cleared; run-scoped `Stats.Values` keys removed via new pure `StatResetRules` (Book_*, mastery_*, MasteryExp, masteryLevelsSpent, ticketPrizesClaimed, specialOrderPrizeTickets) | One reset pass covers all: reset wearing rings+hat with museum donations, slayer kills, consumed floor-10 chest, read book → all clean after |
| 0.11.25 | Reset-leak audit, world side: `netWorldState.MuseumPieces` cleared on reset (same survival class as CC bundles; wiped reward mail + persisting donations re-armed the reward ladder) | Museum empty after reset |
| 0.11.26 | Green rain fires in summer again: patch resolves vanilla's pick (`GreenRainDay.VanillaSummerDay`), scheduler reserves the day like a festival BEFORE storm/rain placement; Weather Sage previews show the 1.6 icon + "Green Rain" hover | Summer forecast shows a green-rain day on one of {5,6,7,14,15,16,18,23}; that day actually runs green rain |
| 0.11.27 | `keep_silo` upgrade (150 JP, Buildings, reach `building:Silo`); reach evaluator exact-match fallback; rebuilt at (60,9) | Shrine shows the row after building a silo this run |
| 0.11.28 | Review fix: `trinketSlots` added to StatResetRules — InventoryPage gates the trinket slot on that stat, NOT on mastery_4, so it leaked for anyone who ever claimed combat mastery | Covered by the reset pass |
| 0.11.29 | Review cleanup: shared `UI/WeatherIcons` (icon map + label were duplicated across both menus); `WeatherScheduler.SummerSeasonIndex` + public `GreenRain` consts; `HasBuildingAtLeast` single loop | Behavior unchanged |
| 0.11.30 | `tly_loadsave <folderName>` debug command (title-screen-only `SaveGame.Load`) for unattended verification passes | — |

**All keep-vs-reset decisions were user-approved 2026-07-09** (full reset on museum, mine chests,
slayer counts, ALL equipment slots incl. cosmetics). Dusklight7's "day-1 parsnips" was already
fixed 2026-05-30. Ancient seed stays WAI.

**Keep-Mastery semantics (documented in FarmerReset):** owners re-claim mastery perks at the
pedestal each loop — intentional; perk recipes/items are wiped every reset, so re-claiming is how
the keep functions (same pattern as kept skill levels re-picking professions). Claims per loop are
bounded by the kept level.

## Code-review findings NOT acted on (user rulings wanted)

An 8-angle adversarial review ran over 0.11.24-27; confirmed items were fixed (0.11.28/29). Left open:

1. **`LostBooksFound` (and other netWorldState siblings)** — same survival class as MuseumPieces:
   after a reset the museum is empty but the library still shows lost-book pages found in prior
   runs. Unreported, flavor-only → left alone. Rule wanted: reset it for consistency, or keep as
   harmless flavor?
2. **StatResetRules is an allow-list** (wipe only known keys). A future vanilla/mod stat key
   outside `Book_*`/`mastery_*` would silently leak until reported. The keep-list direction
   (wipe-by-default) fails safer for the roguelite but would irreversibly wipe lifetime/cosmetic
   counters (steps taken, crops shipped) — which the user never approved resetting. Kept the
   allow-list deliberately; revisit if 1.6.x adds new progression stats.
3. **Coop/barn chain encoded twice** (RunReachEvaluator string arrays vs WorldResetService
   ChainInfo tuples) — pre-existing, cross-layer consolidation deferred (no in-game verification
   available tonight).
4. **Single-tier keep-buildings need 3 edit sites** (catalog + baseline + placement tile). Fine at
   one silo; generalize if keep_shed/keep_mill/etc. get added.

## Overnight automation state

- **Deploy watcher** (background): waits for StardewModdingAPI.exe to exit, then `dotnet build
  src/TheLongestYear` (deploy enabled) → Mods gets 0.11.30.
- **After deploy** the plan was: launch SMAPI, boot smoke (patch classes, 0 errors), then
  `tly_loadsave <folder>` on the user's test save to read the 0.11.11 remixed-bundle
  classification lines (`0 unclassified skipped` + `using derived ramp`). Read-only: NO
  tly_reset/tly_failreset/tly_leaktest on the user's save (they mutate it), no sleeping.
- `EnableThemeReroll: true` still set in the DEPLOYED config.json (playtest convenience) — leave
  until a release pass.

## ✅ Executed 2026-07-10 morning (unattended) — master now v0.11.34

The watcher fired (game exited ~08:47), deployed HEAD, and the verification pass ran with three
findings/fixes along the way:

- **v0.11.31 — tly_loadsave was broken on its first live run.** `SaveGame.Load` alone leaves the
  TitleMenu active: the loader finishes, `gameMode` flips to playing, but the title keeps drawing
  and SaveLoaded never fires (log freezes at "Game loader done"). Vanilla pairs every
  `SaveGame.Load` with `Game1.exitActiveMenu()` (LoadGameMenu.cs:85-86) — now we do too. Verified
  live: `Context: loaded save` + full TLY diagnostics within seconds.
- **The user's test save None_443325260 uses STANDARD bundles**, so its load log can never show
  the remixed derived-ramp lines. **v0.11.32 adds `tly_classify`** (diagnostics-only rebuild of
  catalog + requirements over live BundleData; active run untouched). Paired with vanilla
  `debug ShuffleBundles` (in-memory remix, nothing saved): **0.11.11 fix LOG-VERIFIED** —
  `26 classified (0 category-only skipped, 0 unclassified skipped)`, derived ramps logged for
  Brewer's [1,2,3,4], Wild Medicine [0,1,2,3], Treasure Hunter's [1,2,3,5].
- **v0.11.33 + v0.11.34 — the tly_commands.txt bridge couldn't drive tly_loadsave**: the command
  wasn't routed in ExecuteDebugLine (0.11.33), and the bridge poll itself bailed on
  `!Context.IsWorldReady` so it never ran at the title screen (0.11.34 restructures the gate:
  in-world work still requires an active TLY save; with no world loaded the poll falls through —
  every world-touching command self-guards). Verified end-to-end: queued command consumed at
  title on boot, save loaded, 0 errors.

Boot smoke on every launch: 46 Harmony patch classes applied, 0 failed; 531 tests pass; the only
log ERROR all morning was a console-injection typo (stray leading char — retry succeeded).
Nothing was saved in-game at any point; the user's save file is untouched. Better Chests
verification remains deferred (needs Nexus download + in-game interaction).

## ✅ Better Chests verification executed later that morning — master now v0.11.36

User-directed session (~10:00–10:30): BC is HIDDEN on Nexus (author marked it obsolete Dec
2024); the author's own uploads are still live on **CurseForge** — downloaded BC 2.18.6 +
FauxCore 1.2.2 from there. Result: **0.11.3 did NOT fix the report** (70-slot grid reproduced;
BC sizes the menu via transpiler from its ResizeChest OPTION, not GetActualCapacity).
**v0.11.35** = real fix: BC per-chest modData opt-out stamped on the stash at placement —
screenshot-verified 4-slot grid with BC active. **v0.11.36** = SpecialChestType→None pin
(inert defense). Cross-check with Unlimited Storage 1.2.0 (BC's live successor) found ITS
transpiler still inflates the stash grid (36/70, cosmetic only, deposits stay capped) with no
per-instance opt-out surface — documented as a known limitation in TODO.md, robust menu-rebuild
fix deliberately NOT built (unreported, regression-risky). All third-party mods removed after
testing; Mods folder restored.

**⚠ Vortex incident:** at 10:14 the ContentPatcher folder was emptied (only the
`__folder_managed_by_vortex` marker + config.json remain) — almost certainly a Vortex purge
reacting to the externally-added test mods. FTM now skips (missing CP dependency). **User must
redeploy in Vortex before the lunch playtest.**

## Open items (priority order)

1. **📬 USER: Fluxwb Chinese translation reply** (Nexus 47926) + i18n-support decision (would be a
   large string-extraction pass — not started without a go-ahead).
2. **Playtests** (one reset pass covers the leak audit; plus green rain, keep_silo, horse-fix
   first morning with a stable — see table above).
3. **Better Chests × stash verification (0.11.3)** — BC is not installed locally and not in
   Downloads; needs a Nexus download. Deferred: installing mods into the user's game overnight +
   no debug command to open the stash menu = too intrusive/fragile unattended.
4. Balance reports → 0.12.0/0.13.0 roadmap (needs user brainstorm).
5. Release: next public release bundles 0.11.4→0.11.3x; changelog + README/Nexus description sync
   ride it (release = explicit user "yes"). Also still owed: Advanced Options screenshot for the
   mod page (remixed-bundles recommendation).
