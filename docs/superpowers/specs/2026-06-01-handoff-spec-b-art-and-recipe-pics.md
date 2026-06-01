# Handoff — 2026-06-01 — Spec B deployed; art + recipe-picture fixes; testing continues

Context handoff. The prior agent ran long and is context-heavy; this picks up
mid-playtest of Spec B (Placeable Interactables). **SMAPI IS RUNNING** (relaunched
after deploying Spec B) — do NOT relaunch on startup; the user is mid-test.

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `060a54a`
- 369 tests passing, 0 warnings. Working tree clean.
- **Deployed DLL = the tip** (Steam `Mods/TheLongestYear/`). Local commits only —
  **never push** (workspace rule; user approval required for anything leaving the tree).

## What's deployed (this session's work, all committed)

1. **New-game intro rework** — a single engine-played event (Lewis porch →
   `changeLocation CommunityCenter` → Junimo) opens before control, then the theme
   picker. Dialogue was iterated heavily and is user-approved. Skip-intro forced +
   hidden; farm forced Standard. **Still NOT confirmed to play through end-to-end at
   runtime** (the user kept refining dialogue; the full Lewis→Junimo→picker chain on a
   real new game was never confirmed). Files: `Integration/IntroEventInjector.cs`
   (combined event script), `Integration/IntroSequenceDriver.cs`, `Core/Intro/*`.
2. **fix(reset): 5 basic tools re-granted each loop** — `FarmerReset` wiped the
   inventory and never recreated Axe/Hoe/Can/Pickaxe/Scythe; now `EnsureBasicTools`
   re-grants them. Verify on a reset.
3. **Spec B — Placeable Interactables** (just deployed, the focus of this handoff):
   - 3 books (Cookbook/Craftbook/Bundle-log) = place-and-click **custom furniture**
     (`Integration/BookFurniture.cs`), swept + re-granted exactly-one-each per loop
     (`ReconcileInventory`, called from `WorldResetService.PerformReset` + `OnSaveLoaded`).
   - Old tile interactables (`CookbookInteractable`/`CraftbookInteractable`/
     `SeasonGoalsBoard`) + their config tiles + `tly_set{cookbook,craftbook,board}`
     commands **deleted**.
   - Stash tinted **purple** (`JunimoStashService.PlaceChest`, `playerChoiceColor`).
   - **Planning shrine** = custom furniture (`UI/PlanningShrineService.cs`),
     auto-placed ~5 tiles left of the stash, opens read-only `UI/ShrinePreviewMenu.cs`
     (owned + next-buyable; no spending). Quest `tly.-9005` points to it.
   - Quests: book/board quests removed, stash quest kept, shrine quest added.
   - Spec: `docs/superpowers/specs/2026-06-01-placeable-interactables-design.md`;
     Plan: `docs/superpowers/plans/2026-06-01-placeable-interactables.md`.
4. **Docs / beta**: `README.md`, `CHANGELOG.md`, `docs/nexus-description.bbcode`,
   `docs/beta-release-notes.md`; manifest bumped to `1.0.0-beta.1`.
5. **`tly_addmoney <n>` debug command** added (mirrors `tly_addjp`).

## FIX THESE FIRST (user feedback this session)

1. **Book sprites look like cursor icons.** `src/TheLongestYear/assets/books.png`
   (48x16 tilesheet, three 16x16 covers at sprite indices 0/1/2) is crude placeholder
   art that reads as cursors. Redraw them to actually look like closed books (spine +
   cover). Generator: `tools/gen-sprites.py` (Python+Pillow, both confirmed installed).
   Rebuild deploys the PNGs (csproj `<Content Include="assets\**\*.png">`).
2. **The "shrine" is two cursors stacked.** `src/TheLongestYear/assets/shrine.png`
   (16x32) — redraw as a small shrine/altar. Same generator.
3. **Recipe/crafting "add item" list needs item pictures.** The recipe-picker sub-mode
   in `UI/CookbookMenu.cs` and `UI/CraftbookMenu.cs` (the list you pick a recipe to
   bank from) is text-only — add the dish/item sprite next to each name. Resolve the
   crafted/cooked output item via `ItemRegistry.Create(...)` (cooking recipes →
   `CraftingRecipe`/`Object` output; crafting recipes likewise) and draw its icon.

Note: the books DO function (place + click opens the right menu) and the shrine
works — only the sprites are bad. The custom-furniture format is therefore confirmed
working at runtime (registration + interaction), which de-risks the rest of Spec B.

### ShrinePreviewMenu issues found in playtest (2026-06-01)

4. **Title renders ABOVE the box.** In `UI/ShrinePreviewMenu.cs`, the "Junimo Shrine
   - Planning" title draws at `yPositionOnScreen + 48`, which sits above the dialogue
   box's visible top. Fix the layout (push the title inside the box, or grow/raise the
   box). Pure layout fix, independent of Spec A.
5. **Shows upgrades the player hasn't unlocked in-run** (Loadout section especially),
   and **6. does NOT show keep options for levels/tiers already gained.** Root cause:
   the preview reuses only the existing prereq + `MetaRequirement` gating — it has no
   notion of "what the player actually reached this run." Level/tool keeps are also
   chain-locked (`keep_X_level_2` needs `keep_X_level_1`), so only tier-1 shows and the
   reached tier doesn't surface. **These are exactly what Spec A's in-run gating fixes**
   — build that gating, then have `ShrinePreviewMenu` (and `JunimoShrineMenu`) filter
   "buyable" through it: only show a keep whose underlying thing the player has reached
   this run, and surface the highest reached tier rather than chain-hiding it. Treat the
   preview as a Spec A consumer; do Spec A's gating first, then re-derive the preview.

## STILL TO VERIFY (Spec B runtime — user is mid-test)

- Books: 3 in inventory; place + click each opens the right menu; fireplace no longer
  opens the tracker.
- Stash purple + at home tile.
- Shrine left of the stash; read-only preview shows owned + next-buyable; shrine quest
  clears on first open.
- **Reset sweep**: drop a book in another location, `tly_reset` → still exactly one of
  each book in inventory, stash purple + home, shrine present, no book/board quests.
- Also outstanding from earlier: the **day-1 intro chain** end-to-end, and the
  **5-basic-tools-on-reset** fix.

When the user reports a playtest done: kill SMAPI
(`Get-Process StardewModdingAPI | Stop-Process -Force`), copy
`%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` → `TheLongestYear/SMAPI-latest.txt`,
read it; if continuing, build with deploy on + relaunch + verify from the fresh log.

## Spec A — Keep System v2 (QUEUED, not started, no spec doc yet)

Brainstormed but only captured in conversation — **needs its own spec → plan → build.**
Requirements:
- **In-run purchase gating**: the shrine only offers a keep for a tier/level/item the
  player has actually reached **this run** (measured at shrine-open, which is pre-reset
  / end-of-run state). Filters `JunimoShrineMenu.VisibleCatalogForActiveCategory`
  (`src/TheLongestYear/UI/JunimoShrineMenu.cs`). Player still manually buys; this just
  hides un-earned keeps. New requirement type (live tool-tier/skill-level/mastery/
  golden-scythe check), distinct from the existing `MetaRequirement`.
- **Keep Golden Scythe**: buyable only once the Golden Scythe (`(W)53`, mail
  `gotGoldenScythe`) is obtained; once bought, each loop start grants the Golden Scythe
  **instead of** the basic scythe (suppress the basic-scythe grant in
  `FarmerReset.EnsureBasicTools`).
- **Keep Mastery 1–5**: appears once that mastery level is reached
  (`MasteryTrackerMenu.getCurrentMasteryLevel()`); persists mastery across loops, like
  the skill-level keeps. Catalog generators: `Core/UpgradeCatalogGenerators.cs`.

## Workflow / rules

- Local commits only; never push. Co-Authored-By footer on every commit.
- SMAPI running → build with `-p:EnableModDeploy=false -p:EnableModZip=false` (deploy
  fails on the locked DLL); else a normal build deploys. Tests: `dotnet test TheLongestYear.sln -c Release`.
- Standard farm only; PC only. Decompiled Android source at
  `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android` (use for runtime
  APIs; PC differs from the decompile for some members — reflect/verify).
- Test project references ONLY `TheLongestYear.Core` → only pure Core is unit-testable;
  Game1/Harmony glue is runtime-verified via the SMAPI log.
- User is not a coder: deploy + pull logs yourself; reserve playtests for meaningful
  feedback, not yes/no checks.

## Rodger save prep (debug, applied this session)

Via the `tly_commands.txt` bridge on load: `tly_addmoney 5000000`, `tly_addjp 2000`,
`tly_buyupgrade cookbook_1`, `tly_buyupgrade craftbook_1`. If the user closed Rodger
without sleeping, re-queue those (write the lines to
`Mods/TheLongestYear/tly_commands.txt`; the mod runs them once on the next save load).
