# Handoff — 2026-06-03 — foresight + reset fixes + event-gating + Cart Whisperer done; day-28 cutscene next

Big iterative playtest session. All work below is **committed locally on `feat/v1-plan-07-junimo-stash`
(tip `427c029`), never pushed**, deployed to the Steam `Mods/TheLongestYear/`, and loads clean
(**42 Harmony patch classes, 0 failed; 421 tests pass, 0 warnings**). **SMAPI is RUNNING** — do NOT
relaunch on startup; check first (`Get-Process StardewModdingAPI`).

## Shipped + deployed this session
1. **Foresight / shrine** — Weather Sage reworked to a **rolling, day-of-month** forecast (tier N =
   next N days from *tomorrow*, capped at **6**; dropped weather_sage_7). The Junimo planning shrine
   got a **calendar panel** (weather number-row + HUD weather icons in faint cells), a **cart row**,
   the window grown ~50%, and **green "Owned" rows** for top-of-chain upgrades (no cost).
2. **Recipe reset** — `FarmerReset` now wipes `cooking/craftingRecipes` to the vanilla baseline via
   `Farmer.LearnDefaultRecipes()` then re-grants banked → cookbook/craftbook finally matter.
3. **FarmHouse furniture on reset** — `WorldResetService.RestoreFarmHouseFurniture` clears + re-runs
   vanilla `AddStarterFurniture` so the **full** cabin set (bed, fireplace, rug, table+bowl, chairs)
   comes back correctly placed (fixed the missing fireplace).
4. **Event/cutscene gating** — `MetaState.SeenEventsEver` + `FarmerReset` re-seed (stop wiping
   `eventsSeen`) so seen scenes don't replay. Pure `EventGatingPolicy`/`EventGatingTables` (Core,
   unit-tested). Audited real ids via `tly_dumpevents`: **furnace teach = 992553** (suppress when
   recipe known this run), **Demetrius cave = 65** (replay every loop, held to Spring 5).
   **Relationship/heart events reset** each loop (`RelationshipEventIndex` scans friendship-gated
   ids, excluded from the re-seed). Forced scenes use vanilla `unskippable`.
5. **Keep Horse** — `early_horse` (display **"Keep Horse"**, id unchanged) is now **pure carry-over**
   (`HorseCarryoverService`, mirrors keep_pet): snapshot the player's stable tile + horse name/hat
   before reset, restore there after. **No fixed auto-build** (the old (48,7)-next-to-pet-bowl bug is
   gone). A player must build the stable once; thereafter it persists where they put it.
6. **Cart Whisperer → bundle sense** — single 350 JP upgrade (display "Cart Whisperer", no tier
   number; tiers 2/3 retired). On a cart day the shrine flags which of the cart's **real** stock can
   feed **any** CC bundle (direct items + seed→crop + ingredient→product + category refs), via pure
   `BundleRelevance` (Core, tested) + glue `BundleRelevanceIndex`. **Verified in-game** (Spring 5: it
   correctly flagged the cart's Daffodil = Spring Foraging bundle). The broken predictive preview is
   removed (hub + shrine).

## Cross-agent: Cart Catalog (a DIFFERENT agent owns this)
Cart Catalog is being built as a **separate standalone mod** in the workspace `../CartCatalog/`
folder by another agent — **do not build it here.** TLY only integrates: `CartCatalogIntegration`
checks `ModRegistry.IsLoaded("sonofskywalker3.CartCatalog")` and, when present, switches Cart
Whisperer's bundle-sense to **every day** (book ownership intentionally not required). The contract +
required UniqueID are in `../CartCatalog/README.md`.

## What's next for TLY (priority order)
1. **Day-28 Junimo bedtime cutscene** — THE remaining connected-subsystem piece (specced in
   `docs/superpowers/specs/2026-06-01-batch-b-cutscene-and-event-system-notes.md` §1). In-bed
   black-screen Junimo scene on sleeping the 28th: **gate closed → "we'll rewind the year, here's a
   head-start" → JP shop → save → Spring 1**; **gate open → "great job" → next season**. Must be
   non-skippable. It's also the **natural save point that closes the save-folder churn**. Needs a
   proper brainstorm → spec → build. **Reserve the user to watch it** (meaningful playtest).
2. **Verify the reset-dependent fixes on an actual reset** — recipe wipe, furniture restore,
   event-gating (seen scenes don't replay; Demetrius re-offers ~Spring 5; furnace re-fires only when
   unbanked), Keep Horse carry-over. All built + deployed but **not yet confirmed on a real reset
   this session**. Use `tly_reset` / `tly_failreset` + pull the log (`FarmerReset` logs
   `eventsReseeded`/recipe totals; `RestoreFarmHouseFurniture` + `HorseCarryover` log too).
3. **Save-folder churn / duplicate saves** — still open from the 2026-06-01 handoff. The reset
   re-rolls `uniqueIDForThisGame` → folder drift; the player has several `None*` saves
   (currently on `None2_440032185`). The day-28 full-save (#1) is the real fix; until then resets
   spawn dupes. Consolidate only at the title with no save loaded.

## Player save state (for testing)
- Testing on **`None2_440032185`** (farm "None", Spring, Run 5). The player **owns maxed Weather Sage
  (VI) + Cart Whisperer**, persisted to disk (they slept). Granted via console for testing — see the
  JP/upgrade history in the log if you need to true it up.

## Operational notes (carry-over)
- **Build:** SMAPI running → compile-only (`-p:EnableModDeploy=false -p:EnableModZip=false`); to
  deploy, kill SMAPI, build without those flags, relaunch `StardewModdingAPI.exe` from the Steam
  Stardew dir. Pull logs by copying `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` (no kill).
- **Type into the SMAPI console** programmatically via P/Invoke (AttachConsole + WriteConsoleInput);
  full snippet pattern saved in user memory `smapi-console-input-injection`. **Cast the CONIN$ access
  flag as `[uint32]3221225472`** or the handle is null.
- Only `TheLongestYear.Core` is unit-testable; glue is log-verified. Decompile at
  `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android` (PC differs — verify).
- **Never push / publish** without explicit approval. Local commits only.
