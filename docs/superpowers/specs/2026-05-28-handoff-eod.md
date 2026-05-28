# Handoff — 2026-05-28 end of day

Today's session shipped the entire post-v1 polish + feature backlog. The
TODO is empty. A fresh playtest is in progress as of this handoff (game
launched seconds ago — no log yet).

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `7f620ed` (TODO cleanup) — actual work tip is `14322d4` (weather scheduler)
- Not pushed (no remote configured for TLY)
- 358 tests passing, build clean (0 warnings, 0 errors)
- Deployed DLL is fresh in `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\`

## Today's commits (in order)

```
13e6b00 feat(tly): force Standard farm at character creation
e5ae8fe fix(farm-enforcer): hide non-Standard farm options via reflection
8821beb feat(stash+hub): plain-English modifiers, tighter cards, Junimo chest, fixes
e6cbc7c fix(stash+hub+sampler): playtest polish batch 2
5bdb8f6 feat(quest): weekly theme tracker in the player's quest log
13776ed feat(quest): JP bonus + liability suppression on quest completion
36bbc1d docs(todo): mark Weekly Theme Journal entry as shipped
e7ca1d8 docs(todo): mark Plan 06 effects layer as shipped
4f80eea fix(effects): audit corrections — fish bite math, rare-fish rewire, mines display, deterministic crops, fireplace hit area
b6d7d3b fix(themes): revert crop growth display strings to rounded 25%
1314b9f feat(upgrades): keep_basement + keep_shortcuts JP upgrades
45eac6b fix(stash): walk fallback ladder when first auto tile is blocked
61ab125 feat(debug): tly_wipemeta command for clean-slate testing
1a8e2b2 feat(hud): always-on JP corner counter
14322d4 feat(weather): seed-driven scheduler with per-season minimums
7f620ed docs(todo): mark weather scheduler, wipemeta, JP HUD as shipped
```

## What's live as of this DLL

### Theme & loop layer
- **Weekly Theme Journal quest** (`WeeklyThemeQuestService`) — quest in log with 4-item checklist, ticks as you donate, auto-completes when all 4 donated, awards +N JP (season-scaled: Spring 30 / Summer 45 / Fall 75 / Winter 120) AND **suppresses the week's liability** for the rest of the week. `RunState.LiabilitySuppressedThisWeek` persists; reset on theme select / month / run.
- **Plan 06 effects layer** — all 10 modifier IDs wired with real patches:
  - Foraging: `forage_yield_up` (ForageYieldPatch — 25% extra forage drop) / `mines_closed` (MineDropsPatch — elevator + descent ladder blocked)
  - Farming: `crop_growth_up` (CropGrowthPatch — deterministic 2-of-7 days extra tick) / `fish_bite_down` (FishBiteRatePatch ×1.30)
  - Fishing: `fish_bite_up` (×0.70 — 30% sooner) / `crop_growth_down` (deterministic 2-of-7 lost ticks)
  - Mining: `mine_drops_up` (30% extra mine drop, excl. stone) / `forage_off` (ForageOffPatch — spawnObjects skipped on outdoor non-mine + mine mushroom postfix removal)
  - Mixed: `all_drops_up` (10% extra any drop) / `all_sell_prices_down` (sellToStorePrice ÷ 2)
- **Bonus item sampler** — Spring quota-0 Percentage bundles excluded; week 1-2 filters ~30 EarlyGameAvoid items (cheese/wine/cloth/fruit-tree fruits/deep-mine essences/Calico Desert).
- **`fortune_rare_fish` upgrade** rewired correctly: postfixes `HasCuriosityLure` so the +0.4 baitPotency rare-fish boost fires when owned. (Was previously mis-wired to bite rate.)

### Run layer
- **`keep_kitchen` / `keep_basement` / `keep_shortcuts` JP upgrades**:
  - `keep_kitchen` (800 JP) — `HouseUpgradeLevel = 1`, day-1 kitchen
  - `keep_basement` (1800 JP, prereq `keep_kitchen`) — `HouseUpgradeLevel = 3`, cellar + 33 cask slots + Cask recipe; `Cellar` location created on demand in WorldResetService
  - `keep_shortcuts` (900 JP) — single `communityUpgradeShortcuts` mail flag covers all 5 of Robin's map shortcuts
- **MountainUnlock** — mine entrance landslide cleared on day 1 every loop
- **StandardFarmEnforcer** — strips non-Standard options from CharacterCustomization at title-screen new-game flow + force-resets `whichFarm = 0` on `SaveCreating`
- **JunimoStashService** — chest auto-picks 11 candidate offsets (south of farmhouse first, then sweep horizontally) when configured tile blocked; Junimo Chest sprite; capped slot UI; color picker stripped; immovable via pickaxe; indicator bubble 128px above
- **forage_off sweep** — removes already-spawned forage when liability activates (SelectByName, OnRunLoaded restore, day-28 pre-pick path)

### UI
- **WeeklyHubMenu** — 480×300 cards, plain-English modifier descriptions, parseText word-wrap, controller DPad nav (`receiveGamePadButton` routes to `applyMovementKey`, bypassing snappyMenus gate), `populateClickableComponentList` override (the reflection wiped `allClickableComponents`), no card-hover tooltip, no gold border (cursor signals focus)
- **SeasonGoalsBoard** — 3×3 hit area around the configured fireplace tile
- **JP HUD** — top-right, banked JP + active theme + (1.5×/lifted) suffix. `Display.RenderedHud` hook. GMCM toggle (`ShowJpHud`).

### Weather
- **WeatherScheduler** — per-season minimums (≥2 rain Spring/Fall, ≥2 storm+rain Summer, ≥2 snow Winter), deterministic from `(uniqueIDForThisGame, seasonIndex)`. Days 1-2 forced Sun. Festival days preserved. Subsumes Spring 3 Y1 rain + Summer 13/26 hardcoded storms.

### Debug
- **`tly_wipemeta`** — wipes MetaState in place (keeps the save), warns to reload because IndicatorRegistry / cap patches captured the old reference at OnSaveLoaded.

## Open playtest follow-ups

The user launched a fresh playtest right before this handoff. They have NOT
reported on the new layers yet. Things to watch for in the log when they
finish:

1. **Stash auto-pick** — should now find a clear tile on the first reasonable
   fallback rather than placing on the Farmhouse rect. Look for INFO line:
   `first auto candidate (66, 16) blocked by building 'Farmhouse'; using
   fallback tile (66, 17). Run tly_setstash to anchor a different tile.`
2. **Weekly quest** — should appear in the player's quest log on theme
   selection. Donating all 4 bonus items should award the JP bonus + suppress
   liability (HUD message: "Weekly theme complete! +N JP, drawback lifted.").
3. **JP HUD** — should be visible in top-right at all times outside cutscenes.
   Check that the "(1.5x)" suffix appears after a theme is selected and
   "(1.5x, lifted)" after quest completion.
4. **Weather** — first day of any season is Sun. By the end of Spring, at least
   2 of the 28 days should have been rain. Same for Summer storms, Winter snow.
5. **Crop growth liability** — if Fishing theme is picked (crop_growth_down),
   watered crops should lose growth on days 2 and 5 of each TLY week
   (`dayOfMonth % 7 == 2` or `5`).
6. **Controller** — DPad left/right should navigate between the two theme
   cards on a PC controller (vanilla's snappyMenus gate is bypassed).

If the user reports a bug, pull the log via:
```powershell
Get-Process -Name "Stardew Valley","StardewModdingAPI" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1
Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\SMAPI-latest.txt" -Force
```

## Workflow reminders

- **Local commits only** — TLY has no remote configured. Don't `git push`.
- **Game-running deploys fail** — if the user is mid-playtest and you need to
  build, use `-p:EnableModDeploy=false -p:EnableModZip=false`. Deploy + commit
  when they exit.
- **Standard farm only** — TLY refuses to set up on non-Standard saves
  (StandardFarmEnforcer scrubs the picker; OnSaveLoaded bails on existing
  non-Standard saves).
- **Use Bash tool with bash, PowerShell tool with PS syntax** — earlier in
  the day I sent `Start-Process` to Bash and it failed exit 127. Tool match
  matters.
- **No `/sdcard/` paths** — workspace-wide rule, even though TLY is PC-only.
- **Co-Authored-By footer** — every commit gets the `Claude Opus 4.7 (1M
  context)` footer.

## Files most likely relevant to follow-up bugs

| Area | Files |
|---|---|
| Weather | `src/TheLongestYear.Core/WeatherScheduler.cs`, `src/TheLongestYear/Loop/WeatherModificationsPatch.cs` |
| Quest | `src/TheLongestYear/Loop/WeeklyThemeQuestService.cs`, `src/TheLongestYear/Donations/DonationService.cs` (AfterDonation callback) |
| Stash | `src/TheLongestYear/Loop/JunimoStashService.cs`, `src/TheLongestYear/Loop/JunimoStashCapPatch.cs` |
| JP HUD | `src/TheLongestYear/ModEntry.cs` (DrawJpHud method), `src/TheLongestYear.Core/GameplayConfig.cs` (ShowJpHud) |
| Effects | `src/TheLongestYear/Loop/{AllDropsPatch,CropGrowthPatch,FishBiteRatePatch,ForageOffPatch,ForageYieldPatch,MineDropsPatch}.cs` |
| Upgrades | `src/TheLongestYear.Core/UpgradeCatalog.cs`, `src/TheLongestYear.Core/RunBaselineBuilder.cs`, `src/TheLongestYear/Loop/FarmerReset.cs`, `src/TheLongestYear/Loop/WorldResetService.cs` |
| Active effects | `src/TheLongestYear.Core/ActiveEffectsProvider.cs` (`SuppressLiability` for quest completion) |

## What's NOT done

After this session, TODO.md is empty under "Open." Any new work needs a fresh
ask from the user. Possible next directions if they bring more feedback:
- Live playtest revealed a regression → triage + fix
- New design ideas (UX nits, more upgrades, narrative beats) → spec + plan
- Approach v1 release: README polish, Nexus listing, unex upload
- Push branch to a remote (need to ask the user to set one up first —
  TLY currently has no `origin`)
