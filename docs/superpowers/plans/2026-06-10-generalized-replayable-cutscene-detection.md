# Generalized Replayable-Cutscene Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** At save load, scan the live world's `Data/Events/*` for any cutscene that grants a run-wipe-able unlock (recipe / mail flag / quest) and auto-flag those event ids as "replayable" so they re-fire each loop — merged with the hardcoded vanilla furnace/cave ids — so a mod's teach/unlock cutscene regains eligibility each loop without per-mod hardcoding.

**Architecture:** Pure detection logic (`MatchedGrantToken` / `ScriptGrantsUnlock` / `CollectReplayableIds`) lives in `TheLongestYear.Core` (unit-tested, no game deps). An impure static provider `ReplayableEventScan` loads `Data/Events/*` from the live save at `SaveLoaded` and feeds the pure collector. `FarmerReset`'s reseed loop OR's the dynamic set with `EventGatingTables.Default`. Safety: an explicit exclusion set (`EventSuppressionPatch.SuppressedEventIds` ∪ `RelationshipEventIndex.Ids`), a config kill-switch (default on), and a `tly_dumpreplayable` debug dump.

**Tech Stack:** C# / .NET 6, SMAPI, Harmony, xUnit. Core project has `Nullable enable`; the main `TheLongestYear` project has `Nullable disable`. Tests run with `dotnet test --filter`.

**Spec:** `docs/superpowers/specs/2026-06-10-generalized-replayable-cutscene-detection-design.md`

**Versioning:** Currently on `master`. Each task commits its own change (one change per commit, bisectable). The single `manifest.json` PATCH bump for the whole feature lands once at the end (Task 9), since these tasks ship together as one feature. Commit locally only; no push/release without explicit approval.

---

### Task 1: Core — grant-command detection predicate

**Files:**
- Modify: `src/TheLongestYear.Core/EventGating.cs` (add static members to `EventGatingTables`)
- Test: `tests/TheLongestYear.Tests/ReplayableDetectionTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/ReplayableDetectionTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ReplayableDetectionTests
{
    [Theory]
    [InlineData("addCraftingRecipe Furnace/end", "addCraftingRecipe")]
    [InlineData("addCookingRecipe Survival Burger/end", "addCookingRecipe")]
    [InlineData("speak Marlon \"...\"/addMailReceived guildMember/end", "addMailReceived")]
    [InlineData("mailReceived guildMember/end", "mailReceived")]
    [InlineData("addQuest 16/end", "addQuest")]
    public void MatchedGrantToken_finds_the_grant_command(string script, string expected)
    {
        Assert.Equal(expected, EventGatingTables.MatchedGrantToken(script));
    }

    [Theory]
    [InlineData("speak Lewis \"Welcome\"/pause 500/warp Town 10 10/end")]
    [InlineData("playSound doorClose/move farmer 0 1 2/end")]
    [InlineData("")]
    [InlineData(null)]
    public void MatchedGrantToken_returns_null_for_pure_narrative(string script)
    {
        Assert.Null(EventGatingTables.MatchedGrantToken(script));
    }

    [Fact]
    public void MatchedGrantToken_ignores_a_token_inside_dialogue_text()
    {
        // "mailReceived" appears only inside a speak argument, not at a command-segment start.
        string script = "speak Robin \"Did you get my mailReceived note?\"/end";
        Assert.Null(EventGatingTables.MatchedGrantToken(script));
    }

    [Fact]
    public void ScriptGrantsUnlock_is_true_only_when_a_grant_command_runs()
    {
        Assert.True(EventGatingTables.ScriptGrantsUnlock("addMailReceived guildMember/end"));
        Assert.False(EventGatingTables.ScriptGrantsUnlock("speak Lewis \"hi\"/end"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ReplayableDetectionTests" --nologo`
Expected: FAIL to compile — `EventGatingTables` does not contain `MatchedGrantToken` / `ScriptGrantsUnlock`.

- [ ] **Step 3: Implement the predicate in Core**

In `src/TheLongestYear.Core/EventGating.cs`, inside the `EventGatingTables` class (e.g. just after the `IsFurnaceTeach` method on line 47), add:

```csharp
    /// <summary>Event-script commands that GRANT a run-wipe-able unlock. An event whose script runs
    /// any of these re-teaches/re-unlocks something <c>FarmerReset</c> clears, so it must re-fire each
    /// loop. "mailReceived" is the vanilla alias of "addMailReceived" (Event.cs DefaultCommands).</summary>
    private static readonly string[] GrantCommandTokens =
        { "addCraftingRecipe", "addCookingRecipe", "addMailReceived", "mailReceived", "addQuest" };

    /// <summary>The grant command this script runs (for diagnostics), or null if none. Event scripts
    /// are "/"-delimited command segments; a grant is detected when a segment STARTS WITH a token
    /// (followed by a space or end-of-segment), so a token appearing inside <c>speak</c> dialogue text
    /// is ignored.</summary>
    public static string? MatchedGrantToken(string? script)
    {
        if (string.IsNullOrEmpty(script))
            return null;
        foreach (string segment in script.Split('/'))
        {
            string s = segment.TrimStart();
            foreach (string token in GrantCommandTokens)
            {
                if (s.Length < token.Length)
                    continue;
                if (!s.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (s.Length == token.Length || s[token.Length] == ' ')
                    return token;
            }
        }
        return null;
    }

    /// <summary>True if the event script grants a run-wipe-able unlock (see <see cref="MatchedGrantToken"/>).</summary>
    public static bool ScriptGrantsUnlock(string? script) => MatchedGrantToken(script) != null;
```

(`System` and `System.Collections.Generic` are already imported at the top of the file.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ReplayableDetectionTests" --nologo`
Expected: PASS — all `MatchedGrantToken` / `ScriptGrantsUnlock` tests green.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/EventGating.cs tests/TheLongestYear.Tests/ReplayableDetectionTests.cs
git commit -m "feat(core): detect unlock-granting event-script commands"
```

---

### Task 2: Core — collect replayable ids (pure)

**Files:**
- Modify: `src/TheLongestYear.Core/EventGating.cs`
- Test: `tests/TheLongestYear.Tests/ReplayableDetectionTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `ReplayableDetectionTests.cs` (inside the class):

```csharp
    [Fact]
    public void CollectReplayableIds_flags_grants_excludes_narrative_and_unions_base()
    {
        var events = new (string id, string script)[]
        {
            ("100", "speak Lewis \"hi\"/end"),              // narrative → not flagged
            ("200", "addMailReceived guildMember/end"),     // grant → flagged
            ("300", "addCraftingRecipe Furnace/end"),       // grant → flagged
            ("191393", "addMailReceived ccDone/end"),       // grant BUT excluded → dropped
        };
        var baseIds = new[] { "992553", "65" };             // vanilla furnace/cave, always replayable
        var exclude = new System.Collections.Generic.HashSet<string> { "191393" };

        var result = EventGatingTables.CollectReplayableIds(events, baseIds, exclude);

        Assert.Contains("200", result);
        Assert.Contains("300", result);
        Assert.Contains("992553", result);
        Assert.Contains("65", result);
        Assert.DoesNotContain("100", result);     // narrative not flagged
        Assert.DoesNotContain("191393", result);  // excluded even though it grants
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~CollectReplayableIds_flags_grants" --nologo`
Expected: FAIL to compile — `EventGatingTables` does not contain `CollectReplayableIds`.

- [ ] **Step 3: Implement the collector in Core**

In `src/TheLongestYear.Core/EventGating.cs`, directly below `ScriptGrantsUnlock` (added in Task 1), add:

```csharp
    /// <summary>The event ids that should be REPLAYABLE each loop: every scanned event whose script
    /// grants an unlock, MINUS the explicit <paramref name="exclude"/> set (narrative-suppressed +
    /// relationship events), UNION the always-replayable <paramref name="baseReplayableIds"/> (the
    /// vanilla furnace/cave ids — never excluded). Pure; the runtime scanner feeds it loaded content.</summary>
    public static HashSet<string> CollectReplayableIds(
        IEnumerable<(string id, string script)> events,
        IEnumerable<string> baseReplayableIds,
        ISet<string>? exclude)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, string script) in events)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            if (exclude != null && exclude.Contains(id))
                continue;
            if (ScriptGrantsUnlock(script))
                result.Add(id);
        }
        if (baseReplayableIds != null)
            foreach (string id in baseReplayableIds)
                result.Add(id);
        return result;
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ReplayableDetectionTests" --nologo`
Expected: PASS — all tests in the file green.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/EventGating.cs tests/TheLongestYear.Tests/ReplayableDetectionTests.cs
git commit -m "feat(core): collect replayable event ids with exclusion + base union"
```

---

### Task 3: Config flag + GMCM toggle

**Files:**
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs`
- Modify: `src/TheLongestYear/ModEntry.cs:968-972` (GMCM options block in `OnGameLaunched`)

- [ ] **Step 1: Add the config property**

In `src/TheLongestYear.Core/GameplayConfig.cs`, after the `ShowJpHud` property (ends line 187), add:

```csharp
    /// <summary>When true (default), TLY scans the live save's Data/Events at load and auto-flags any
    /// cutscene that grants a run-wipe-able unlock (recipe / mail flag / quest) as "replayable" so it
    /// re-fires each loop — covering mod unlock cutscenes, not just the vanilla furnace/cave scenes.
    /// Set false to fall back to only the hardcoded vanilla ids (today's behavior). Takes effect on the
    /// next save load.</summary>
    public bool AutoDetectReplayableUnlockCutscenes { get; set; } = true;
```

- [ ] **Step 2: Add the GMCM toggle**

In `src/TheLongestYear/ModEntry.cs`, in `OnGameLaunched`, immediately after the `ShowJpHud` bool option (the block ending at line 972 with `tooltip: () => "Always-on corner counter ..."`), add:

```csharp
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.AutoDetectReplayableUnlockCutscenes,
                setValue: v => _config.AutoDetectReplayableUnlockCutscenes = v,
                name: () => "Auto-detect mod unlock cutscenes",
                tooltip: () => "Re-fire any mod cutscene that grants a recipe / mail flag / quest each " +
                               "loop, so wiped mod unlocks (e.g. SVE's guild) can be regained. Off = only " +
                               "vanilla furnace/cave scenes replay. Takes effect on next save load.");
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj --nologo`
Expected: Build succeeded (warnings OK).

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear.Core/GameplayConfig.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(config): add AutoDetectReplayableUnlockCutscenes toggle (default on)"
```

---

### Task 4: Expose the suppressed-event ids for exclusion reuse

**Files:**
- Modify: `src/TheLongestYear/Loop/EventSuppressionPatch.cs:35`

- [ ] **Step 1: Widen the field's accessibility**

In `src/TheLongestYear/Loop/EventSuppressionPatch.cs`, change line 35 from:

```csharp
        private static readonly System.Collections.Generic.HashSet<string> SuppressedEventIds
```

to:

```csharp
        // internal (not private): the replayable-cutscene scanner reuses this as its exclusion seed so
        // an event we explicitly suppress is never auto-flagged as replayable (single source of truth).
        internal static readonly System.Collections.Generic.HashSet<string> SuppressedEventIds
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/EventSuppressionPatch.cs
git commit -m "refactor: expose SuppressedEventIds as internal for exclusion reuse"
```

---

### Task 5: `ReplayableEventScan` static provider (impure shell)

**Files:**
- Create: `src/TheLongestYear/Loop/ReplayableEventScan.cs`

- [ ] **Step 1: Create the provider**

Create `src/TheLongestYear/Loop/ReplayableEventScan.cs`:

```csharp
using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Runtime-built set of event ids that GRANT a run-wipe-able unlock (recipe / mail flag / quest)
    /// and therefore must re-fire each loop — the general analogue of the hardcoded vanilla furnace/cave
    /// ids in <see cref="EventGatingTables.Default"/>. Scanned from the live save's Data/Events at
    /// SaveLoaded (so a MOD's teach/unlock cutscene replays too), then OR'd with Default by
    /// <see cref="FarmerReset"/> when re-seeding eventsSeen. Cleared on deactivate. The detection
    /// predicate + collection are pure in Core (unit-tested); this is the content-loading shell.
    /// </summary>
    internal static class ReplayableEventScan
    {
        private static HashSet<string> _ids = new(StringComparer.Ordinal);

        /// <summary>True if the scan flagged this event id as a wipe-able unlock grant.</summary>
        public static bool IsReplayable(string eventId) => _ids.Contains(eventId);

        /// <summary>Drop the scan (deactivate / non-TLY save / return to title).</summary>
        public static void Clear() => _ids = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Rebuild the flagged-id set from the live save's events. <paramref name="enabled"/> = false
        /// (config kill-switch) leaves the set empty so only Default's vanilla ids apply.
        /// </summary>
        public static void Populate(
            IGameContentHelper content,
            IEnumerable<GameLocation> liveLocations,
            IEnumerable<string> baseReplayableIds,
            ISet<string> exclude,
            bool enabled,
            IMonitor monitor)
        {
            if (!enabled)
            {
                Clear();
                monitor.Log(
                    "Replayable-cutscene auto-detection disabled (config) — only vanilla furnace/cave ids replay.",
                    LogLevel.Trace);
                return;
            }

            // Primary source: every loaded location's name. Using the live world (not a hardcoded list)
            // covers mod-added locations such as SVE's Custom_AdventurerSummit. Deduped.
            var locationNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameLocation loc in liveLocations)
                if (!string.IsNullOrEmpty(loc?.Name))
                    locationNames.Add(loc.Name);

            var events = new List<(string id, string script)>();
            foreach (string loc in locationNames)
            {
                Dictionary<string, string> data;
                try
                {
                    data = content.Load<Dictionary<string, string>>($"Data/Events/{loc}");
                }
                catch (Exception)
                {
                    continue; // no event data file for this location
                }
                if (data == null) continue;

                foreach (KeyValuePair<string, string> kv in data)
                {
                    int slash = kv.Key.IndexOf('/');
                    string id = slash < 0 ? kv.Key : kv.Key.Substring(0, slash);
                    events.Add((id, kv.Value ?? ""));
                }
            }

            _ids = EventGatingTables.CollectReplayableIds(events, baseReplayableIds, exclude);
            monitor.Log(
                $"Replayable-cutscene scan: flagged {_ids.Count} unlock-granting event id(s) across " +
                $"{locationNames.Count} location(s).",
                LogLevel.Trace);
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/ReplayableEventScan.cs
git commit -m "feat: ReplayableEventScan provider — scan live Data/Events for unlock grants"
```

---

### Task 6: Wire the scan into ModEntry (populate at load, clear on deactivate)

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` — add a private exclusion-set helper, call `Populate` in `OnSaveLoaded`'s proceed path, call `Clear` in `DeactivateTly`.

- [ ] **Step 1: Add the exclusion-set helper**

In `src/TheLongestYear/ModEntry.cs`, add this private method next to `RecordSeenEvents` (after the method ending at line 474). The exclusion seed is the explicitly-suppressed narrative ids plus relationship events — both must NOT be auto-flagged replayable:

```csharp
        /// <summary>The exclusion seed for the replayable-cutscene scan: events we explicitly suppress
        /// (<see cref="TheLongestYear.Loop.EventSuppressionPatch.SuppressedEventIds"/>, e.g. the Lewis
        /// CC intro) plus relationship/heart events (which re-fire via their own reseed skip). An event
        /// in this set is never auto-flagged as a wipe-able unlock grant.</summary>
        private static System.Collections.Generic.HashSet<string> BuildReplayableExclude()
        {
            var exclude = new System.Collections.Generic.HashSet<string>(
                TheLongestYear.Loop.EventSuppressionPatch.SuppressedEventIds,
                System.StringComparer.Ordinal);
            exclude.UnionWith(TheLongestYear.Loop.RelationshipEventIndex.Ids);
            return exclude;
        }
```

- [ ] **Step 2: Call `Populate` in the proceed path**

In `OnSaveLoaded`, immediately after `UpgradeChecker.HasUpgrade = id => _meta.State.HasUpgrade(id);` (line 279), add:

```csharp
            // Generalize the replayable-cutscene set: scan the live save's Data/Events for any
            // unlock-granting cutscene (recipe/mail/quest) so a mod's teach/unlock scene re-fires each
            // loop, merged with the vanilla furnace/cave ids. FarmerReset consults it at reset time.
            TheLongestYear.Loop.ReplayableEventScan.Populate(
                this.Helper.GameContent,
                Game1.locations,
                EventGatingTables.Default.ReplayableEventIds,
                BuildReplayableExclude(),
                _config.AutoDetectReplayableUnlockCutscenes,
                this.Monitor);
```

(`EventGatingTables` is in `TheLongestYear.Core`, already `using`-imported in ModEntry. `Game1.locations` is an `IEnumerable<GameLocation>`.)

- [ ] **Step 3: Clear the scan on deactivate**

In `DeactivateTly` (starts line 370), after `DonationService.Active = null;` (line 375), add:

```csharp
            TheLongestYear.Loop.ReplayableEventScan.Clear();
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj --nologo`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs
git commit -m "feat: populate replayable-cutscene scan at load, clear on deactivate"
```

---

### Task 7: Wire the dynamic set into the FarmerReset reseed

**Files:**
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs:108`

- [ ] **Step 1: OR the dynamic set into the replayable guard**

In `src/TheLongestYear/Loop/FarmerReset.cs`, change line 108 from:

```csharp
                if (EventGatingTables.Default.IsReplayable(id)) continue;
```

to:

```csharp
                // Replayable = the hardcoded vanilla ids (furnace/cave) OR any unlock-granting cutscene
                // the load-time scan flagged (mod teach/unlock scenes). Either way, don't re-mark it
                // seen, so it stays eligible to re-fire this loop.
                if (EventGatingTables.Default.IsReplayable(id)
                    || ReplayableEventScan.IsReplayable(id)) continue;
```

(`ReplayableEventScan` is in the same `TheLongestYear.Loop` namespace as `FarmerReset` — no `using` change needed.)

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Run the full test suite (no regressions)**

Run: `dotnet test --nologo`
Expected: PASS — all tests green (the prior suite plus the new `ReplayableDetectionTests`).

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/FarmerReset.cs
git commit -m "feat: reseed honors the scanned replayable-cutscene set"
```

---

### Task 8: `tly_dumpreplayable` debug command

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` — register the command (near line 183, beside `tly_dumpevents`) and add the handler (beside `CmdDumpEvents`, after line 454).

- [ ] **Step 1: Register the command**

In `src/TheLongestYear/ModEntry.cs`, after the `tly_dumpevents` registration (line 183), add:

```csharp
            helper.ConsoleCommands.Add("tly_dumpreplayable", "Audit which Data/Events cutscenes the loop treats as REPLAYABLE (re-fire each loop): logs each unlock-granting event id, the matched grant command, whether it's excluded, and the active exclusion set (debug — diagnoses 'an event keeps replaying').", this.CmdDumpReplayable);
```

- [ ] **Step 2: Add the handler**

In `src/TheLongestYear/ModEntry.cs`, after `CmdDumpEvents` (ends line 454), add:

```csharp
        /// <summary>Audit the replayable-cutscene detection: scan the live save's events, log every
        /// unlock-granting cutscene with the matched grant command + whether the exclusion set drops it,
        /// then the resulting flagged-id set. Requires a loaded save (reads Game1.locations).</summary>
        private void CmdDumpReplayable(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            System.Collections.Generic.HashSet<string> exclude = BuildReplayableExclude();
            int total = 0, grants = 0, excluded = 0;

            foreach (GameLocation loc in Game1.locations)
            {
                if (string.IsNullOrEmpty(loc?.Name)) continue;

                System.Collections.Generic.Dictionary<string, string> data;
                try
                {
                    data = this.Helper.GameContent.Load<System.Collections.Generic.Dictionary<string, string>>($"Data/Events/{loc.Name}");
                }
                catch (System.Exception)
                {
                    continue;
                }
                if (data == null) continue;

                foreach (System.Collections.Generic.KeyValuePair<string, string> kv in data)
                {
                    total++;
                    string script = kv.Value ?? "";
                    string token = EventGatingTables.MatchedGrantToken(script);
                    if (token == null) continue;

                    grants++;
                    int slash = kv.Key.IndexOf('/');
                    string id = slash < 0 ? kv.Key : kv.Key.Substring(0, slash);
                    bool isExcluded = exclude.Contains(id);
                    if (isExcluded) excluded++;
                    string snippet = script.Length > 120 ? script.Substring(0, 120) : script;
                    this.Monitor.Log(
                        $"[dumpreplayable] {loc.Name} id={id} grant='{token}' excluded={isExcluded} :: {snippet}",
                        LogLevel.Info);
                }
            }

            this.Monitor.Log(
                $"[dumpreplayable] scanned {total} events; {grants} grant-cutscene(s), {excluded} excluded, " +
                $"{grants - excluded} flagged replayable (config enabled={_config.AutoDetectReplayableUnlockCutscenes}). " +
                $"Exclusion set has {exclude.Count} id(s). Vanilla base always-replayable: " +
                $"[{string.Join(",", EventGatingTables.Default.ReplayableEventIds)}].",
                LogLevel.Info);
        }
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj --nologo`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs
git commit -m "feat: tly_dumpreplayable debug command for replayable-cutscene audit"
```

---

### Task 9: Final verification + manifest bump

**Files:**
- Modify: `manifest.json` (PATCH bump per `.claude/CLAUDE.md`)

- [ ] **Step 1: Full build + test**

Run: `dotnet build --nologo && dotnet test --nologo`
Expected: Build succeeded; all tests PASS.

- [ ] **Step 2: Bump the manifest version**

In `manifest.json`, bump the `Version` field one PATCH (e.g. `0.10.0` → `0.10.1`). Match the assembly `<Version>` in `src/TheLongestYear/TheLongestYear.csproj` only if the existing convention keeps them in step (check current values first; do not introduce a new mismatch).

- [ ] **Step 3: Commit**

```bash
git add manifest.json src/TheLongestYear/TheLongestYear.csproj
git commit -m "chore: bump to v0.10.1 — generalized replayable-cutscene detection"
```

- [ ] **Step 4: In-game smoke test (deploy → user tests → pull logs)**

This is the engineering-correctness verification; it does not need a meaningful playtest (per workflow rules, deploy + log-read is the author's job):
1. Deploy the build to the test device.
2. On a TLY save, run `tly_dumpreplayable` and confirm: the vanilla furnace `992553` + cave `65` appear as flagged; `191393` (Lewis CC intro) appears `excluded=true`; the totals line prints `config enabled=True`.
3. Confirm the SaveLoaded log contains `Replayable-cutscene scan: flagged N ... id(s)`.
4. Toggle the GMCM option off, reload, and confirm the scan logs `disabled (config)` and `tly_dumpreplayable` reports `enabled=False`.
5. Pull the log and verify no errors around the scan.

---

## Notes for the implementer

- **Do NOT push or create a release.** Local commits only; releases require an explicit "yes, push."
- **One change per commit** — keep history bisectable (workspace rule).
- The scan runs once per save load and is consumed only at reset time by `FarmerReset`; there is no per-reset rescan and none is needed (content does not change mid-session).
- The dynamic set only controls reseed *eligibility*. An event still must pass `EventSuppressionPatch`, `EventGatingPolicy`, and vanilla preconditions to actually fire — that layering is the primary safety net behind the max-recall detection.
