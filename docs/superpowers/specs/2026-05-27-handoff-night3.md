# Handoff — 2026-05-27 (night 3)

Picking up TLY work after a long playtest session. Branch `feat/v1-plan-05-ui`,
223 tests passing, build clean, all changes committed (no working-tree changes).

## What shipped today

Ten commits — `49540a8` through `667f68e`. Highlights:

- **Joja root-cause fix.** Stopped adding event `191393` to `eventsSeen` in
  `CommunityCenterUnlock`; instead Harmony-patched `Game1.isLocationAccessible`
  for the CC. That flag had 9 vanilla branch points; two were lighting up the
  destroyed-Joja repaint + the rain-night `WorldChangeEvent(12)` that destroys
  Joja. Joja is now open in v1.
- **Day-8 hub.** Dropped the over-defensive `!CanMove` gate in
  `MenuLauncher.CanOpen`. Hub now opens on day 8+ wake-ups.
- **Day-3 forced rain.** Patched `Game1.getWeatherModificationsForDate` to
  suppress the vanilla "Spring day 3 = Rain" hardcode.
- **RNG re-seed on reset.** `WorldResetService.PerformReset` re-rolls
  `Game1.uniqueIDForThisGame` so forage placement varies per run. Renames the
  save folder on disk to match the new unique ID (manual computation — SMAPI's
  `Constants.CurrentSavePath` caches at SaveLoaded and doesn't track in-session
  ID changes).
- **Save backup moved out of Saves dir.** Now lands at
  `Mods/TheLongestYear/backups/` so it doesn't appear as a phantom save on
  the title screen.
- **Festival time-flow.** Time advances during festivals (patched
  `Game1.shouldTimePass`). Exit at the actual in-game time instead of
  vanilla's 2200 jump. Auto-eject at festival `endTime` with HUD message.
  Skipped the "Are you sure you want to leave?" confirmation. Re-drew the
  DayTimeMoneyBox during festivals via SMAPI `Display.RenderedHud` since
  vanilla's `drawHUD` short-circuits on `eventUp`.

## Open / pending

### Verified-but-not-tested

- **Festival auto-end + clock-during-festival.** Code shipped; user got as
  far as confirming the time-flow works but hadn't reached Spring 13
  (Egg Festival) before we paused. Nothing to do right now; will surface
  next playtest.

### Small follow-up patch (not yet implemented)

- **Festival exit to host map, not farm.** Vanilla `Event.endBehaviors`
  warps the player to the farm entry after a festival. User wants them to
  exit to the festival's actual host map (Town for Egg/Fair/Spirit's Eve;
  Beach for Luau/Moonlight Jellies; Forest for Flower Dance; etc.) so it
  feels like walking off any other map. ~20 lines, postfix/transpiler on
  `endBehaviors`. Defer until after the persistence plan if needed.

### Design just landed — PLAN THIS NEXT

- **`docs/superpowers/specs/2026-05-27-persistence-meta-progression-design.md`** —
  full persistence + meta-progression design for LY1. User approved at the
  design-review gate. Phase A (effects layer + per-stat keep upgrades +
  skill XP flooring + profession re-pick + generalised MetaRequirement +
  shrine UI filter) is the next implementation plan. Phase B (Cookbook +
  Craftbook) follows. Phase C deferred to LY3.

## Workflow rules (unchanged)

- **Local commits only.** Never push or upload anything without explicit
  "yes, push" from the user.
- **Co-Authored-By footer** on every commit:
  `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
- **I deploy, user tests, I pull logs.** Don't ask the user to run
  commands themselves when the `tly_commands.txt` bridge can do it.
- **Test before each commit:**
  `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
- **Build/deploy:**
  `dotnet build src/TheLongestYear/TheLongestYear.csproj`
  (EnableModDeploy auto-copies the DLL to the Stardew Mods folder).
- **PC vs Android decompile gotcha.** The decompile at
  `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android` is
  the only Stardew source available; lift method signatures from it
  carefully because PC-side fields/methods sometimes differ (this session
  caught `GameLocation.tapToMove` as Android-only — broke the PC build).
  Always verify with a quick `dotnet build` before claiming PC parity.

## Why this handoff exists

Previous-session agent (me, 2026-05-27) spent ~5 hours bouncing through
debugging, design clarification, and brainstorming. Context window
pollution risk by the time the persistence design landed. Fresh agent
will produce a better Phase A implementation plan than continuing in
the same session. Spec is intentionally self-contained for that reason.
