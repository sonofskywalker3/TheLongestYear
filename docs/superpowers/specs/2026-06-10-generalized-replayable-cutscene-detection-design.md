# Design: Generalized "replayable unlock cutscene" detection

**Date:** 2026-06-10
**Status:** Approved (brainstorm complete) — pending implementation plan
**Target:** v0.10.1 event-hygiene pass. Versioning follows the branch implementation lands on per
`.claude/CLAUDE.md`: small PATCH bumps per change on `master` (current branch); **no** `manifest.json`
Version bump if moved to a parallel feature branch.
**Origin:** SVE-clash investigation (Nexus bug 1089299, resolved NOT-A-BUG 2026-06-10). See `TODO.md`
"🔁 QUEUED — generalize 'replayable' cutscene detection".

## Problem

TLY's loop reset wipes the player's run state (`FarmerReset.Apply`) and re-seeds `eventsSeen` from the
cross-loop `SeenEventsEver` memory so already-watched cutscenes stay suppressed (déjà-vu skip — correct by
design). Cutscenes that **grant something the reset wipes** must be excluded from that re-seed so they can
re-fire each loop and the player can regain the unlock. Today that "replayable" set is **hardcoded to two
vanilla ids** in `EventGatingTables.Default`:

- `992553` — Clint teaches the Furnace crafting recipe.
- `65` — Demetrius' cave (mushrooms/bats) choice.

A **mod's** unlock cutscene whose grant the loop wipes is not in that list, so it is marked seen and never
replays — the player cannot regain that unlock on loop 2+. Motivating case: SVE's guild initiation event
`1000034` sets the `guildMember` mail flag via `addMailReceived`; `FarmerReset` clears `mailReceived` every
loop, so guild access is lost permanently after loop 1.

## Scope decisions (user-confirmed 2026-06-10)

1. **Build the general scan** (vs. closing the item). Regaining mod-mechanic access each loop matters for
   completeness across all mods.
2. **Detection scope = maximum recall:** flag recipe **and** mail **and** quest grants
   (`addCookingRecipe`, `addCraftingRecipe`, `addMailReceived` / alias `mailReceived`, `addQuest`). This
   catches the guild class, at the cost of flagging narrative events that incidentally set mail — which makes
   the safety scaffolding (below) mandatory, not optional.
3. **Seed the exclusion set now** with the known narrative ids, treating it as the shared list the other
   v0.10.1 hygiene items (suppress Lewis CC intro, etc.) also feed.

## Grant-command tokens (verified against the Android decompile)

`Event.cs` `DefaultCommands` registers event commands by method name (`SetupEventCommandsIfNeeded`,
reflection over public statics; `[OtherNames]` adds aliases). The grant commands:

| Token | Source | Grants (wiped by reset) |
|-------|--------|--------------------------|
| `addCookingRecipe`  | `DefaultCommands.AddCookingRecipe`  | cooking recipe (wiped unless cookbook-banked) |
| `addCraftingRecipe` | `DefaultCommands.AddCraftingRecipe` | crafting recipe (wiped unless craftbook-banked) |
| `addMailReceived`   | `DefaultCommands.AddMailReceived` (alias `mailReceived`) | mail flag (wiped by `mailReceived.Clear()`) |
| `addQuest`          | `DefaultCommands.AddQuest`          | quest that may gate a mechanic |

`mailReceived` is a substring of `addMailReceived`, but we match on **segment start** (below), so both the
canonical command and the alias are caught without naive substring over-matching.

## Architecture

Three pieces, following the existing house pattern (`RelationshipEventIndex` static lazy index;
`ActiveEffectsProvider` / `UpgradeChecker` / `DonationService` static providers nulled in
`ModEntry.DeactivateTly`).

### A. Core (pure, unit-testable) — `EventGatingTables` additions

- Module-level constant grant-token set (`StringComparer.OrdinalIgnoreCase`):
  `{ addCraftingRecipe, addCookingRecipe, addMailReceived, mailReceived, addQuest }`.
- `static bool ScriptGrantsUnlock(string script)` — **boundary-aware**: split the script on `/` (the event
  command delimiter), and return true if any command segment, after `TrimStart`, **starts with** a grant
  token (followed by a space or end-of-segment). Segment-start matching avoids false-matching a token that
  appears inside a `speak`/dialogue argument.
- `static HashSet<string> CollectReplayableIds(IEnumerable<(string id, string script)> events, ISet<string> exclude)`
  — runs `ScriptGrantsUnlock` per event, collects matching ids, subtracts `exclude`, and unions the
  hardcoded vanilla ids (`992553`, `65`). Pure → the whole detection is testable without game content.

### B. Runtime scanner (impure shell) — new `ReplayableEventScan` static provider

- Populated at `SaveLoaded` (after `Game1.locations` exists and content is loadable).
- Enumerates location names = **`Game1.locations` (every loaded location's `.Name`)**. Using the live world
  is what makes detection general — it covers mod-added locations (e.g. SVE's `Custom_AdventurerSummit`),
  not just the hardcoded 34 from `CmdDumpEvents`. **Live-only, intentionally** (resolved during code review,
  2026-06-10): every event-bearing vanilla location (Farm, Town, all interiors, CommunityCenter, Mine, …) is
  always a persistent `GameLocation` in `Game1.locations` at `SaveLoaded`, so the old vanilla static list is
  a strict subset that adds zero recall — and a 4th hardcoded copy of that list would violate DRY. Live
  enumeration is therefore at-least-as-correct for vanilla and strictly more general for mods.
- For each location, `Helper.GameContent.Load<Dictionary<string,string>>($"Data/Events/{loc}")` (try/catch
  the missing-file case, as `CmdDumpEvents` already does), strip the precondition suffix from each key to
  get the bare id, and feed `(id, script)` pairs into `CollectReplayableIds`.
- Caches the resulting `HashSet<string>` for the session; exposes `bool IsReplayable(string id)`.
- Cleared (set null/empty) in `DeactivateTly`, alongside the other static providers.
- Skipped entirely when the config kill-switch is off (provider stays empty → only vanilla ids apply).

### C. Wire-in — one line in `FarmerReset.Apply` reseed loop

Change the replayable guard from:

```csharp
if (EventGatingTables.Default.IsReplayable(id)) continue;
```

to:

```csharp
if (EventGatingTables.Default.IsReplayable(id) || ReplayableEventScan.IsReplayable(id)) continue;
```

`ReplayableEventScan` already unions the vanilla ids via `CollectReplayableIds`, so the `Default` check is
redundant when the scan ran — but it is the correct fallback when the scan is disabled or empty.

## Safety layers (load-bearing under max-recall)

1. **Replayable set controls re-seed *eligibility*, not firing.** An id removed from `eventsSeen` still must
   pass `EventSuppressionPatch` (already returns `-1` for the Lewis CC intro `191393`), `EventGatingPolicy`,
   and vanilla preconditions before it fires. Post-reset friendship is 0, so friendship-gated narrative
   events do not re-fire regardless. Most false positives are inert.
2. **Explicit exclusion set** — `CollectReplayableIds`'s `exclude` param, seeded now with the known
   narrative-suppress ids (Lewis CC intro and any sibling hygiene-item ids) plus the `RelationshipEventIndex`
   ids (which the reseed loop already skips separately). An event you have decided is narrative is never
   auto-flagged, even if its script sets mail. This is the shared list the other v0.10.1 items feed.
3. **Config kill-switch** — `ModConfig.AutoDetectReplayableUnlockCutscenes` (default `true`) + GMCM toggle.
   Off → only the hardcoded `{992553, 65}` apply (exactly today's behavior). One-flip escape hatch.
4. **Debug dump** — `tly_dumpreplayable` command: runs the scan and logs every auto-flagged id, the token
   that matched, a script snippet, and the active exclusion set. Lets us diagnose any "wrong event replaying"
   report from logs alone (matches the deploy → pull-logs workflow).

## Testing

Pure Core functions → unit tests with no game content (mirrors `EventGatingPolicyTests`):

- **`ScriptGrantsUnlock`**:
  - `addCraftingRecipe Furnace`, `addCookingRecipe …`, `addMailReceived guildMember`, `mailReceived …`,
    `addQuest 16` → **true**.
  - Pure narrative (`speak Lewis "…"/pause 500/warp …/end`) → **false**.
  - Boundary guard: a `speak` line whose dialogue text contains the literal word "mailReceived" → **false**
    (segment-start matching, not substring).
- **`CollectReplayableIds`**: feed a fake event dict; assert flagged ids, assert the exclusion set is
  subtracted, assert the vanilla `{992553, 65}` still merge in.
- The impure `ReplayableEventScan` shell stays thin (load content → call pure collector) and is exercised
  live via `tly_dumpreplayable`; no unit test.

## Deliverables

- `EventGatingTables`: grant-token constant + `ScriptGrantsUnlock` + `CollectReplayableIds` (pure, Core).
- `ReplayableEventScan` static provider (impure shell; scans at `SaveLoaded`; nulled in `DeactivateTly`).
- `FarmerReset` reseed: one-line OR against `ReplayableEventScan`.
- Exclusion set seeded with the known narrative ids (Lewis CC intro) + `RelationshipEventIndex`.
- `ModConfig.AutoDetectReplayableUnlockCutscenes` (default true) + GMCM toggle.
- `tly_dumpreplayable` debug command.
- Unit tests for `ScriptGrantsUnlock` and `CollectReplayableIds`.

## Out of scope

- A "don't re-fire if the unlock is already held this run" refinement (like the furnace's
  `furnaceKnownThisRun` gate) generalized to all unlocks. The existing furnace special-case stays; everything
  else relies on vanilla preconditions + the in-run `eventsSeen` (only cleared at reset) to avoid mid-run
  repeats. Revisit only if a "redundant re-teach" report appears.
- The other v0.10.1 event-hygiene items (suppress Lewis CC, Demetrius cave per-loop prompt, furnace
  recipe-known gate, `tly_dumpevents` audit) — separate work; this design only *consumes* their suppressed
  ids via the exclusion set.
- The reset-path consolidation tech debt — explicitly deferred until after any bugfix work (no refactor
  mid-fix).
- **Known matcher limitation (review note, 2026-06-10):** `MatchedGrantToken` splits scripts on `/` only, so
  a grant command nested inside a `quickQuestion` dialogue-choice branch (branches are `\`-delimited, not
  `/`) is not detected — that event won't be auto-flagged replayable. No vanilla unlock teach uses this shape;
  revisit only if a "my mod's choice-gated teach won't replay" report appears. A code comment near
  `MatchedGrantToken` records this so it isn't a future surprise.
