# Handoff — 2026-05-27 night 4 (live-playtest bug reports)

The prior agent (me, post-Plan-07) ran out of context mid-investigation. This
hands off to a fresh agent with the current state plus the live playtest
findings the user reported across 4 messages near the end of the session.

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash` (last commit `c4af752`)
- Tests: 330 passing, 0 failing
- Build: clean (0 warnings, 0 errors)
- Deployed DLL: in `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\` — last build was post-`c4af752`
- Stardew was running (the user is mid-playtest) — confirm + close before re-deploy
- Config in user's Stardew install: `Mods/TheLongestYear/config.json` — was auto-rewritten on the last launch with the full new field set including `Enabled: true`, `StashTileX: 72`, `StashTileY: 12`, `CookbookTileX/Y: 4`, `CraftbookTileX: 10`, `CraftbookTileY: 4`. `SeasonGoalsBoardTileX/Y = 32, 8` (user customisation preserved from previous session).
- GMCM installed in the user's Mods folder. The "TLY → Enabled" toggle works.

## v1 plan series (all merged on the branch chain — not pushed)

```
feat/v1-plan-05-ui (base, pre-v1)
  └─ feat/v1-plan-06a-persistence-effects   (14 commits)
       └─ feat/v1-plan-06b-cookbook-craftbook  (10 commits)
            └─ feat/v1-plan-06-theme-effects   (15 commits)
                 └─ feat/v1-plan-07-junimo-stash (12 commits inc. user-direction follow-ups + standard-farm gate)
```

Tip of `feat/v1-plan-07-junimo-stash` = current shipped behaviour.

## User-direction changes already landed (post-Plan-07 follow-ups)

| Commit | Change |
|---|---|
| `a4f9876` | Stash from day 1 (4 base slots), stash_1/2/3 each +4, GMCM Enabled toggle, sensible default tile coords, one-shot config (0,0) migration |
| `181a009` | Always WriteConfig on Entry so new fields surface in config.json (preserves customisations) |
| `c4af752` | Standard-farm gate: `OnSaveLoaded` skips all setup if `Game1.whichFarm != 0` |

## OUTSTANDING PLAYTEST BUGS (4 new + 1 in-flight)

The user is on a fresh Standard-farm save with TLY enabled. After waking up
on Spring 1 and engaging the weekly hub, they reported:

### Bug A — Stash chest not visible at (72, 12)

> "I don't see a stash chest? Where is it?"

Likely causes (investigate in this order):
1. **Tile (72, 12) is not a valid open Farm tile on the Standard farm.** The
   farmhouse origin is around (61-72, 14-19). (72, 12) is 2 tiles ABOVE the
   farmhouse — might be inside a grass patch, a tree, or off the actual
   placeable area. Verify by reading `Maps/Farm.tbin` or by computing the
   farm bounds.
2. **`JunimoStashService.PlaceChest` silently fails.** The code does
   `farm.objects[tile] = chest;` without checking placeability. If the tile
   is unwalkable/blocked, the chest might be placed but visually hidden, or
   the assignment might throw silently. Add logging to confirm placement.
3. **`farm.objects` key requires the chest to be at a clear ground tile.**
   Vanilla Stardew allows objects on Diggable+Buildable tiles. (72, 12) may
   be a path or grass debris tile that's not in that set.

**Recommended fix paths** (pick one):
- **A-fix-1:** Pre-compute a known-safe Standard-farm tile and update the
  GameplayConfig default + migration. (60, 16) is just south of the
  farmhouse door area, in the open. (72, 17) is east of the farmhouse on
  open grass. Verify with the Standard Farm map data before picking.
- **A-fix-2:** Add a `PlaceChest` fallback — try the configured tile; if
  blocked, search adjacent tiles in a spiral; log the final tile.
- **A-fix-3:** Surface the tile in the SMAPI log on every `PlaceChest` call
  so we can tell whether the issue is "placed but invisible" vs "placement
  rejected" vs "tile is occluded."

The user also asked **"where is it?"** so make the SMAPI log say exactly
where it tried to place. Currently `JunimoStashService.PlaceChest` logs
"placed stash chest at ({X}, {Y})" but doesn't say what (if anything) was
there before, or whether the placement is visible.

### Bug B — Forage-off liability doesn't work

> "I think I picked the no forage option, but I still found a leek."
> "found 2 horseradish too. so that drawback doesn't work"

Leek `(O)20` and Horseradish `(O)16` are Spring forage items. The user is in
Spring 1+ of a fresh save. They picked the "no forage" option (the Mining
theme — `forage_off` liability — HARD spawn suppression).

`ForageOffPatch` (in `src/TheLongestYear/Loop/ForageOffPatch.cs`) is a prefix
on `GameLocation.spawnObjects` that returns `false` (skip vanilla) when:
- `ActiveLiability("forage_off")` is true
- Location is NOT a `MineShaft`
- Location `IsOutdoors`

Possible causes:
1. **`ActiveEffectsProvider` not Set when the player picked the theme.** Check
   the SMAPI log for the "Selected Mining (bonus mine_drops_up, liability
   forage_off)" line. If it's absent, the picker didn't fire `SelectByName`,
   so the active effects stay null.
2. **`ForageOffPatch` Harmony patch silently failed to wire.** Check the
   SMAPI log at startup for "Patching StardewValley.GameLocation.spawnObjects".
   If absent, the patch didn't attach.
3. **The forage was spawned BEFORE the theme was picked.** The Standard farm
   has wild forage items pre-placed on day 1 (vanilla behaviour — the farm
   spawns forage at game start). `ForageOffPatch` only suppresses NEW
   spawnObjects calls; pre-existing forage on day 1 is untouched. If the
   user picked Mining theme AFTER finding the leek, the forage was already
   there. Verify by checking what time the theme was selected vs when the
   forage was found.
4. **`spawnObjects` isn't the right method.** Vanilla 1.6 may have moved
   forage spawn to `GameLocation.dayUpdate` or a different path. Verify
   against the decompile at `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\decompiled\StardewValley\StardewValley\GameLocation.cs`.
5. **`__instance.IsOutdoors` check is wrong** — the Farm IS outdoors so this
   should be true. But verify.

**Recommended investigation:**
- Pull SMAPI log via `close-smapi.ps1` (after user closes game) or read
  `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`.
- Search for "Selected", "active effects", "spawnObjects", and the user's
  found items (leek/horseradish).
- If the patch is wiring but not firing, log inside the prefix:
  `_monitor.Log($"ForageOffPatch.Prefix: loc={loc.Name}, liability={ActiveLiability("forage_off")}");`

### Bug C — Weekly Theme Journal quest

> "you were supposed to build a quest that would show your weekly bonus
> items and benefit/drawback, when does that get built?"

**Status:** Not built yet. It's in `TODO.md` under "Open → Weekly Theme
Journal entry" (spec'd 2026-05-26). It is NOT in any of the v1 plans
(05/06A/06B/06/07). The user remembered the design discussion and expected
it to ship with the journal-relevant gameplay layer.

The TODO.md entry has the full design sketch. Implementing it is a
~10-task plan: hook into vanilla `Quest` system, list the 4 bonus items,
mark them donated on `DonationObserver` callback, complete the journal
when all 4 donated, suppress that week's liability for the remaining days
of the week (the bonus stays active).

**Decision needed from user:** Is this v1.0 scope or v1.1? It's not in §14
of the design spec, but the user clearly expected it.

### Bug D — Controller can't pick between themes (was investigating)

> "controller can't pick between themes"

I was reading `WeeklyHubMenu.cs` to investigate when context ran out.
Findings so far:
- `receiveGamePadButton` handles A button → confirms left/right card based
  on `currentlySnappedComponent` reference compare.
- `_leftCard.rightNeighborID = CardIdRight` and `_rightCard.leftNeighborID =
  CardIdLeft` ARE set in `RecomputeBoundsAndLayout` — snap nav between them
  should work.
- `snapToDefaultClickableComponent` sets `currentlySnappedComponent = _leftCard`
  on open.
- `DrawCard` tints the snapped card slightly brighter (`Color.White` vs
  `Color.White * 0.9f`) — visual difference is subtle (10% brightness diff).

Hypotheses (not yet verified in-game):
1. **Visual feedback too subtle.** The 10% tint difference may not register
   as "focus highlight" to the player. They don't know which card is
   currently selected. Increase contrast or add a border highlight to the
   snapped card.
2. **Snap nav not working at all.** Could be a `setUpForGamePadMode()` call
   missing, or `allClickableComponents` populated AFTER snap is set. Test
   in-game with logging.
3. **A button intercepted by something else.** Maybe the menu opens during
   a frame when game still has input focus, A press fires as a vanilla
   tool/action instead of menu-A.
4. **`Game1.options.gamepadControls` is false on the user's setup.** Then
   `snapToDefaultClickableComponent` is never called and
   `currentlySnappedComponent` stays null — A press in `receiveGamePadButton`
   gates on `currentlySnappedComponent != null` and falls through to base.

**Pull the SMAPI log** to check the user's controller config + whether the
theme was actually selected. The user said they picked Foraging/Mining (it's
ambiguous which one — Bug B suggests they picked the one with `forage_off`
liability, which is Mining). If `SelectByName` fired, they DID manage to
pick somehow — maybe via mouse/touch — but the controller didn't navigate.

### Bug E — Mines on day 1

> "what causes the mines to be opened in the first place? maybe we need to
> have it open from the first day too, thoughts?"

In vanilla Stardew, the mines open after the "Earthquake" event (Demetrius
visits the farm) on the morning of **Spring 5**. The trigger is a
combination of the `earthquakeEvent` mail flag + day-of-month check. Before
that, the entrance is blocked by rubble.

**User intent:** they want the mines open on day 1 of each TLY run.

**Recommended approach** (analogous to keep_bus_unlocked):
- Add a `WorldResetService` step that marks the rubble cleared on every
  reset — set the appropriate mail flag (`landslideDone` or similar — check
  decompile) AND remove the in-world rubble tiles if necessary.
- OR: Patch the `Mine.checkAction` / entry-block check to always allow
  entry if TLY is enabled.

Investigate the vanilla rubble-clear mechanism first:
- `Game1.player.mailReceived.Add("...")` — find the right flag in the
  decompile
- Or `Mountain.cs` (mine entrance is in Mountain location) — check what
  drives the rubble visibility

This is a small feature change (~30 lines if the right mail flag is found).
Could ship as part of fixing Bug A/B or as a separate commit.

## In-flight uncommitted work

None. Everything is committed at `c4af752`.

The investigation of Bug D was reading-only — no edits in progress.

## What was about to happen

I was going to:
1. Build + deploy the standard-farm gate (✓ done, commit `c4af752`)
2. Investigate Bug D by reading `WeeklyHubMenu.cs` controller handling
3. Likely add a clearer focus highlight (border around snapped card)
4. Then circle back to Bugs A, B, C, E in some priority order

The user was already mid-playtest with the game running. They'll need to:
1. Close the game
2. Pull SMAPI log so we can see what happened
3. Then any new build deploys cleanly

## Files most likely relevant to the bug fixes

| Bug | Files to read first |
|---|---|
| A — stash invisible | `src/TheLongestYear/Loop/JunimoStashService.cs` (PlaceChest), Maps/Farm.tbin for valid tiles |
| B — forage_off broken | `src/TheLongestYear/Loop/ForageOffPatch.cs`, `src/TheLongestYear/Loop/RunController.cs` (SelectByName), decompile `GameLocation.spawnObjects` |
| C — Theme journal | `TODO.md` "Weekly Theme Journal entry" section, decompile `StardewValley.Quests.Quest` |
| D — controller picker | `src/TheLongestYear/UI/WeeklyHubMenu.cs`, decompile `IClickableMenu.snapMovementKey` |
| E — mines on day 1 | decompile `Mountain.cs`, `MineShaft.cs`, vanilla mail flag lookup |

## Other context

- **Workflow rules:** I deploy + pull logs, user tests. Local commits only.
- **PC playtest tooling:** `close-smapi.ps1` kills SMAPI + copies log to project.
  No `launch-smapi.ps1` for TLY — launch directly via
  `Start-Process "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.exe"`.
- **Build:** `dotnet build src/TheLongestYear/TheLongestYear.csproj` (no
  `-p:EnableModDeploy=false` once game is closed — deploys to mods folder).
- **Test:** `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
- **Co-Authored-By footer required** on every commit.

## Auto memory worth checking

- `feedback_meaningful_playtests_only.md` — reserve user playtest for
  meaningful feedback. This IS the meaningful session, so 4 bugs in one
  go is expected.
- `feedback_deploy_and_logs_are_mine.md` — I deploy, user tests, I pull.
- `feedback_dont_ask_just_execute.md` — when user reports a bug, diagnose
  and execute, don't ask for permission.
