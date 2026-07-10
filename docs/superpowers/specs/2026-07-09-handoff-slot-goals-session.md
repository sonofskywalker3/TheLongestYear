# Handoff — 2026-07-09 session (5th sweep + slot-based weekly goals)

**Repo state:** master at `v0.11.23`, all work committed locally (NOT pushed, nothing released).
Deployed to PC Mods = 0.11.23. Tests: 498 passing; solution rebuild 0 warnings.

## What shipped this session (all on master)

| Version | Change | Status |
|---|---|---|
| 0.11.11 | Derived default quota for unclassified pick-X-of-Y bundles — remixed bundles were silently dropped from season checkpoints/win gate/weekly pools (caused khauser13's premature win, xsansara's 6 log WARNs) | Needs a remixed-save log check: `Bundle requirements built: N classified (… 0 unclassified skipped)` + `using derived ramp` INFO lines |
| 0.11.12–19 | **Slot-based weekly theme goals** (spec + plan in `docs/superpowers/{specs,plans}/2026-07-09-*`). Goals = exact open CC bundle slots; tick reads live CC state; 1.5× JP slot-strict; open-slots-only sampling; shrink/empty-pool lift; save migration | **PLAYTEST CONFIRMED 2026-07-09** (wrong-slot 1× parsnip = no tick; 5 gold → tick + `(bonus x1.5)`). Empty-pool lift never observed live (unit-covered) |
| 0.11.20 | Quest tip moved BELOW the goal checklist (was pushing goals below the fold) | User-reported, fixed same session |
| 0.11.21 | Horse re-named-every-morning fix: blank-name snapshot loop + unset stable owner; `EnsureHorseNamed` day-start heal | Verify: first in-game morning with a stable → no NamingMenu, or a `HorseCarryover: repaired` log line. Horse-returns-to-stable = vanilla, NOT a bug |
| 0.11.22 | `tly_select <theme>` now forces any theme (skipOfferCheck) — playtest tool | — |
| 0.11.23 | Ghost-picker fix: a theme pick consumes the week's offer (sets OfferPresentedWeek + clears same-week deferred offer); stale cross-week deferred offers dropped | Found live when a stale deferred picker overwrote the user's Farming pick |

Also: 5th forum sweep triaged into `TODO.md` (top section); memory index compacted (detail moved
to `ac-key-lessons.md` / `device-deployment-details.md` topic files).

## Local-only conveniences to remember

- **`EnableThemeReroll: true` is set in the DEPLOYED config.json** (`.../Stardew Valley/Mods/TheLongestYear/config.json`) for playtesting — code default stays false. Consider flipping the user's back off before any release build sanity pass.
- The user's active test save (`None_44…`, changes name each reset) has debug-granted items/JP from testing.
- Console injection works via `tools/send-smapi-command.ps1` (see memory `smapi-console-input-injection`).
- Deploy while the game runs fails (locked DLL) — build with `-p:EnableModDeploy=false`, then re-deploy after the game closes (watcher pattern: `until ! tasklist | grep -qi stardew; do sleep 5; done; dotnet build src/TheLongestYear`).

## Open items (priority order, details in TODO.md)

1. **Reset-leak audit** (Dusklight7): museum donations/rewards, mine milestone chests, monster-slayer progress, worn clothes/rings persist across loops. Decide keep/reset per surface, fix leaks.
2. **Green rain never triggers in summer** (khauser13) — likely `WeatherScheduler` drops vanilla's green-rain day. (Summer 13/26 fixed storms = WAI.)
3. **`keep_silo` upgrade** (khauser13 + Dusklight7) — small catalog addition next to the other keep-buildings.
4. **📬 USER decision: Fluxwb Chinese translation** (Nexus 47926, modified DLL, asks permission) — reply is the user's; consider i18n support.
5. Balance reports (too easy ×3 players, spring-reset JP cheese) → 0.12.0/0.13.0 roadmap fodder (`tly-012-013-014-roadmap`).
6. Awaiting: VeggieGirl43 Better Chests retest (0.11.3); xsansara money bug unreproducible (closed unless re-reported).
7. Release: next public release bundles 0.11.4→0.11.2x; changelog + README/Nexus description sync ride it (release = explicit user "yes").
