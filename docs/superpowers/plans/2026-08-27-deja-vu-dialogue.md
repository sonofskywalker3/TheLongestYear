# Deja-vu Villager Dialogue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Villagers the player has interacted with a lot across loops occasionally prepend a rare, in-character "have we met?" line, with no mechanical effect and no loop explanation.

**Architecture:** A nightly rollup (`FamiliarityRollup`, Core) turns the day's live friendship signals into `MetaState.VillagerFamiliarity`, the only persisted state. `DejaVuRules` (Core) decides eligibility, tier and the roll from meta + per-loop `RunState` caps. `DejaVuLines` (Core) resolves i18n keys per villager with a default fallback pool. One Harmony postfix on `NPC.checkForNewCurrentDialogue` (glue) pushes the chosen `Dialogue` on top of the stack so it plays before the villager's own line and never touches an `activeDialogueEvents` (Introduction) line.

**Tech Stack:** C# / .NET, SMAPI 4, HarmonyLib, xunit in `tests/TheLongestYear.Tests`.

**Spec:** `docs/superpowers/specs/2026-08-27-deja-vu-dialogue-design.md`; approved lines in `docs/superpowers/specs/2026-08-27-deja-vu-dialogue-lines.md`.

## Global Constraints

- Work on `master`; commit per task; never push or release. Patch-bump `src/TheLongestYear/manifest.json` per code commit (starts at 0.16.12).
- No em dashes anywhere. Only the approved lines go into i18n, verbatim.
- Core has no game refs. Tests: `dotnet test tests/TheLongestYear.Tests` (1138 passing at start). Build with `-p:EnableModDeploy=false` while the game runs.
- Ask Jeff before `tools/deploy.ps1` (it takes the desktop).
- Numbers: threshold 60, chance 6%, tier 2 at 3x threshold, one line per villager per loop, one per 7 days overall, never in loop 1.
- Commit footer `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: Familiarity rollup (Core)

**Files:**
- Create: `src/TheLongestYear.Core/FamiliarityRollup.cs`
- Modify: `src/TheLongestYear.Core/MetaState.cs` (new dictionary after `SeenEventsEver`)
- Test: `tests/TheLongestYear.Tests/FamiliarityRollupTests.cs`

**Interfaces:**
- Produces: `record VillagerDaySignals(string Npc, bool Talked, int Gifts, int HeartEvents)`; `FamiliarityRollup.Apply(MetaState meta, IEnumerable<VillagerDaySignals> signals) : int` (points added); constants `TalkPoints = 1`, `GiftPoints = 3`, `HeartEventPoints = 10`; `MetaState.VillagerFamiliarity : Dictionary<string,int>`.

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FamiliarityRollupTests
{
    [Fact]
    public void Talk_gift_and_heart_event_score_1_3_10()
    {
        var meta = new MetaState();
        int added = FamiliarityRollup.Apply(meta, new[]
        {
            new VillagerDaySignals("Pierre", Talked: true, Gifts: 2, HeartEvents: 1),
            new VillagerDaySignals("Haley", Talked: false, Gifts: 0, HeartEvents: 0),
        });
        Assert.Equal(17, added);
        Assert.Equal(17, meta.VillagerFamiliarity["Pierre"]);
        Assert.False(meta.VillagerFamiliarity.ContainsKey("Haley"));   // nothing happened, no entry
    }

    [Fact]
    public void Days_accumulate_on_the_same_villager()
    {
        var meta = new MetaState();
        for (int day = 0; day < 5; day++)
            FamiliarityRollup.Apply(meta, new[] { new VillagerDaySignals("Pierre", true, 0, 0) });
        Assert.Equal(5, meta.VillagerFamiliarity["Pierre"]);
    }
}
```

- [ ] **Step 2: Run, expect compile failure.**
- [ ] **Step 3: Implement.** In `MetaState.cs` after `SeenEventsEver`:

```csharp
    /// <summary>Deja-vu dialogue (spec 2026-08-27): cumulative interaction points per villager
    /// (internal name) across EVERY loop. Talk +1, gift +3, heart event +10, rolled up nightly by
    /// FamiliarityRollup. Hearts themselves still reset; this is the only thing that remembers.</summary>
    public Dictionary<string, int> VillagerFamiliarity { get; set; } = new();
```

`FamiliarityRollup.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One villager's interaction signals for a single day, read from the live friendship data.</summary>
public sealed record VillagerDaySignals(string Npc, bool Talked, int Gifts, int HeartEvents);

/// <summary>Nightly rollup of interaction into <see cref="MetaState.VillagerFamiliarity"/> (deja-vu
/// dialogue spec 2026-08-27). Pure; the glue gathers the signals from Game1.</summary>
public static class FamiliarityRollup
{
    public const int TalkPoints = 1;
    public const int GiftPoints = 3;
    public const int HeartEventPoints = 10;

    /// <summary>Adds each villager's points for the day. Returns the total added. A villager with
    /// zero points gets no entry, so the dictionary only lists people the player has dealt with.</summary>
    public static int Apply(MetaState meta, IEnumerable<VillagerDaySignals> signals)
    {
        int total = 0;
        foreach (VillagerDaySignals s in signals)
        {
            int points = (s.Talked ? TalkPoints : 0) + s.Gifts * GiftPoints + s.HeartEvents * HeartEventPoints;
            if (points <= 0) continue;
            meta.VillagerFamiliarity.TryGetValue(s.Npc, out int current);
            meta.VillagerFamiliarity[s.Npc] = current + points;
            total += points;
        }
        return total;
    }
}
```

- [ ] **Step 4: Run the suite, expect green.**
- [ ] **Step 5: Commit** (manifest 0.16.13): `git commit -m "v0.16.13: villager familiarity rollup into MetaState"`

---

### Task 2: Eligibility, tiers and the roll (Core)

**Files:**
- Create: `src/TheLongestYear.Core/DejaVuRules.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs` (three fields), `src/TheLongestYear.Core/GameplayConfig.cs` (three settings after `FestivalMainEventOncePerDay`)
- Test: `tests/TheLongestYear.Tests/DejaVuRulesTests.cs`

**Interfaces:**
- Produces: `RunState.DejaVuShownTo : List<string>`, `RunState.DejaVuLastDay : int = -1`, `RunState.EventsSeenAtDayStart : List<string>`; `GameplayConfig.EnableDejaVuDialogue = true`, `DejaVuThreshold = 60`, `DejaVuChancePercent = 6`; `DejaVuRules.WeeklyCapDays = 7`, `TierMultiplier = 3`; `DejaVuRules.Tier(int familiarity, int threshold) : int` (0 = below threshold, 1, 2); `DejaVuRules.IsEligible(MetaState meta, RunState run, string npc, int daysPlayed, int threshold) : bool`; `DejaVuRules.TryPick(MetaState meta, RunState run, string npc, int daysPlayed, GameplayConfig config, Func<int,int> rollPercent, bool force) : int` returning the tier to play or 0, and stamping `run.DejaVuShownTo` / `run.DejaVuLastDay` on a hit.

- [ ] **Step 1: Failing tests**

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class DejaVuRulesTests
{
    private static MetaState Meta(int resets, string npc, int fam)
    {
        var m = new MetaState { CompletedResets = resets };
        m.VillagerFamiliarity[npc] = fam;
        return m;
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(59, 0)]
    [InlineData(60, 1)]
    [InlineData(179, 1)]
    [InlineData(180, 2)]
    public void Tier_boundaries(int fam, int tier) => Assert.Equal(tier, DejaVuRules.Tier(fam, 60));

    [Fact]
    public void Never_in_loop_one_or_below_threshold()
    {
        Assert.False(DejaVuRules.IsEligible(Meta(0, "Pierre", 500), new RunState(), "Pierre", 10, 60));
        Assert.False(DejaVuRules.IsEligible(Meta(1, "Pierre", 59), new RunState(), "Pierre", 10, 60));
        Assert.True(DejaVuRules.IsEligible(Meta(1, "Pierre", 60), new RunState(), "Pierre", 10, 60));
    }

    [Fact]
    public void Per_villager_and_weekly_caps()
    {
        var run = new RunState();
        run.DejaVuShownTo.Add("Pierre");
        Assert.False(DejaVuRules.IsEligible(Meta(1, "Pierre", 100), run, "Pierre", 10, 60));
        var run2 = new RunState { DejaVuLastDay = 10 };
        Assert.False(DejaVuRules.IsEligible(Meta(1, "Haley", 100), run2, "Haley", 16, 60));   // 6 days later
        Assert.True(DejaVuRules.IsEligible(Meta(1, "Haley", 100), run2, "Haley", 17, 60));    // 7 days later
    }

    [Fact]
    public void TryPick_rolls_the_chance_and_stamps_the_caps()
    {
        var cfg = new GameplayConfig();
        var meta = Meta(1, "Pierre", 200);
        var run = new RunState();
        Assert.Equal(0, DejaVuRules.TryPick(meta, run, "Pierre", 30, cfg, _ => 50, force: false));   // roll 50 >= 6: miss
        Assert.Empty(run.DejaVuShownTo);
        Assert.Equal(2, DejaVuRules.TryPick(meta, run, "Pierre", 30, cfg, _ => 3, force: false));    // roll 3 < 6: hit, tier 2
        Assert.Contains("Pierre", run.DejaVuShownTo);
        Assert.Equal(30, run.DejaVuLastDay);
        Assert.Equal(0, DejaVuRules.TryPick(meta, run, "Pierre", 31, cfg, _ => 0, force: false));    // capped now
    }

    [Fact]
    public void Force_bypasses_chance_and_caps_but_not_the_loop_one_rule()
    {
        var cfg = new GameplayConfig();
        var run = new RunState { DejaVuLastDay = 30 };
        run.DejaVuShownTo.Add("Pierre");
        Assert.Equal(1, DejaVuRules.TryPick(Meta(1, "Pierre", 60), run, "Pierre", 31, cfg, _ => 99, force: true));
        Assert.Equal(1, DejaVuRules.TryPick(Meta(0, "Pierre", 10), run, "Pierre", 31, cfg, _ => 99, force: true));   // force even below threshold: tier floor 1
    }

    [Fact]
    public void Disabled_config_never_picks()
    {
        var cfg = new GameplayConfig { EnableDejaVuDialogue = false };
        Assert.Equal(0, DejaVuRules.TryPick(Meta(1, "Pierre", 200), new RunState(), "Pierre", 30, cfg, _ => 0, force: false));
    }
}
```

- [ ] **Step 2: Run, expect compile failure.**
- [ ] **Step 3: Implement.** `RunState.cs` (after `AwardedBundleCompletions`):

```csharp
    /// <summary>Deja-vu dialogue: villagers who already said their line this loop (one each).</summary>
    public List<string> DejaVuShownTo { get; set; } = new();

    /// <summary>Deja-vu dialogue: days-played stamp of the last line anywhere in town (-1 = none).</summary>
    public int DejaVuLastDay { get; set; } = -1;

    /// <summary>Deja-vu rollup: Farmer.eventsSeen as of the last rollup, so tonight's new heart
    /// events can be counted by difference.</summary>
    public List<string> EventsSeenAtDayStart { get; set; } = new();
```

`GameplayConfig.cs` after `FestivalMainEventOncePerDay`:

```csharp
    /// <summary>Deja-vu dialogue (spec 2026-08-27): villagers you have dealt with a lot across loops
    /// occasionally half-remember you. No mechanical effect. GMCM "Features".</summary>
    public bool EnableDejaVuDialogue { get; set; } = true;

    /// <summary>Familiarity points (talk 1, gift 3, heart event 10, summed over every loop) a
    /// villager needs before a deja-vu line can play. Tier 2 lines start at three times this.</summary>
    public int DejaVuThreshold { get; set; } = 60;

    /// <summary>Percent chance per eligible conversation. Capped to one line per villager per loop
    /// and one line per week across the whole town regardless of this value.</summary>
    public int DejaVuChancePercent { get; set; } = 6;
```

`DejaVuRules.cs`:

```csharp
using System;

namespace TheLongestYear.Core;

/// <summary>Eligibility, tier and roll for the deja-vu villager lines (spec 2026-08-27). Pure; the
/// Harmony postfix supplies the live day count and RNG. Everything here is deliberately stingy:
/// the lines are meant to be uncanny, not a feature the player farms.</summary>
public static class DejaVuRules
{
    public const int WeeklyCapDays = 7;
    public const int TierMultiplier = 3;

    /// <summary>0 below the threshold, 1 from the threshold, 2 from TierMultiplier x threshold.</summary>
    public static int Tier(int familiarity, int threshold)
    {
        if (threshold <= 0) threshold = 1;
        if (familiarity >= threshold * TierMultiplier) return 2;
        return familiarity >= threshold ? 1 : 0;
    }

    public static bool IsEligible(MetaState meta, RunState run, string npc, int daysPlayed, int threshold)
    {
        if (meta.CompletedResets < 1) return false;                         // loop 1: nothing to remember
        meta.VillagerFamiliarity.TryGetValue(npc, out int fam);
        if (Tier(fam, threshold) == 0) return false;
        if (run.DejaVuShownTo.Contains(npc)) return false;                  // one per villager per loop
        if (run.DejaVuLastDay >= 0 && daysPlayed - run.DejaVuLastDay < WeeklyCapDays) return false;
        return true;
    }

    /// <summary>The tier to play now (0 = nothing). <paramref name="rollPercent"/> returns a value in
    /// [0,100) given 100; a hit is roll &lt; chance. <paramref name="force"/> (debug) skips the
    /// chance and the caps, never the config switch, and plays at least tier 1.</summary>
    public static int TryPick(MetaState meta, RunState run, string npc, int daysPlayed,
        GameplayConfig config, Func<int, int> rollPercent, bool force)
    {
        if (!config.EnableDejaVuDialogue) return 0;
        meta.VillagerFamiliarity.TryGetValue(npc, out int fam);
        int tier;
        if (force)
            tier = Math.Max(1, Tier(fam, config.DejaVuThreshold));
        else
        {
            if (!IsEligible(meta, run, npc, daysPlayed, config.DejaVuThreshold)) return 0;
            if (rollPercent(100) >= config.DejaVuChancePercent) return 0;
            tier = Tier(fam, config.DejaVuThreshold);
        }
        if (!run.DejaVuShownTo.Contains(npc)) run.DejaVuShownTo.Add(npc);
        run.DejaVuLastDay = daysPlayed;
        return tier;
    }
}
```

- [ ] **Step 4: Run the suite, expect green.**
- [ ] **Step 5: Commit** (manifest 0.16.14): `git commit -m "v0.16.14: deja-vu eligibility, tiers, caps and roll"`

---

### Task 3: Lines in i18n and `DejaVuLines` (Core)

**Files:**
- Create: `src/TheLongestYear.Core/DejaVuLines.cs`
- Modify: `src/TheLongestYear/i18n/default.json` (new `dejavu.*` block at the end, every approved line verbatim)
- Modify: `tests/TheLongestYear.Tests/I18nGuardTests.cs` `ReferencedKeys()` (record the family by executing `DejaVuLines.AllKeys`)
- Test: `tests/TheLongestYear.Tests/DejaVuLinesTests.cs`

**Interfaces:**
- Produces: `DejaVuLines.KeysFor(string npc, int tier, IReadOnlyCollection<string> availableKeys) : IReadOnlyList<string>` (villager pool `dejavu.<npc lower>.<tier>.<n>` for n = 1.. contiguous, else the `dejavu.default.<tier>.<n>` pool); `DejaVuLines.Pick(string npc, int tier, IReadOnlyCollection<string> availableKeys, Func<int,int> rollIndex) : string?` (resolved text via `Strings.Get`, null when no pool); `DejaVuLines.AllKeys(IReadOnlyCollection<string> availableKeys) : IEnumerable<string>` (every key the family could ever ask for, for the guard).

- [ ] **Step 1: i18n block.** Append to `default.json` before the closing brace, keys `dejavu.default.1.1` to `.1.3`, `dejavu.default.2.1` to `.2.3`, then `dejavu.<name>.1.1` and `dejavu.<name>.2.1` for each of the 34 villagers, text copied verbatim from `2026-08-27-deja-vu-dialogue-lines.md` (names lower-case: abigail, alex, caroline, clint, demetrius, dwarf, elliott, emily, evelyn, george, gus, haley, harvey, jas, jodi, kent, krobus, leah, leo, lewis, linus, marnie, maru, pam, penny, pierre, robin, sam, sandy, sebastian, shane, vincent, willy, wizard).

- [ ] **Step 2: Failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class DejaVuLinesTests
{
    private readonly I18nFixture _fixture;
    public DejaVuLinesTests(I18nFixture fixture) => _fixture = fixture;

    [Fact]
    public void Villager_pool_is_used_when_present_else_default()
    {
        var keys = _fixture.Map.Keys.ToList();
        Assert.Equal(new[] { "dejavu.pierre.1.1" }, DejaVuLines.KeysFor("Pierre", 1, keys));
        Assert.Equal(new[] { "dejavu.default.2.1", "dejavu.default.2.2", "dejavu.default.2.3" },
            DejaVuLines.KeysFor("SomeModNpc", 2, keys));
    }

    [Fact]
    public void Pick_resolves_text_and_every_villager_has_both_tiers()
    {
        var keys = _fixture.Map.Keys.ToList();
        Assert.Equal("Have you shopped here before? I feel like I know your order.",
            DejaVuLines.Pick("Pierre", 1, keys, _ => 0));
        Assert.Equal("Being with you feels like the island. Safe.", DejaVuLines.Pick("Leo", 2, keys, _ => 0));
        string[] villagers = { "Abigail","Alex","Caroline","Clint","Demetrius","Dwarf","Elliott","Emily","Evelyn","George","Gus","Haley","Harvey","Jas","Jodi","Kent","Krobus","Leah","Leo","Lewis","Linus","Marnie","Maru","Pam","Penny","Pierre","Robin","Sam","Sandy","Sebastian","Shane","Vincent","Willy","Wizard" };
        foreach (string v in villagers)
        {
            Assert.Contains($"dejavu.{v.ToLowerInvariant()}.1.1", keys);
            Assert.Contains($"dejavu.{v.ToLowerInvariant()}.2.1", keys);
        }
    }

    [Fact]
    public void Lines_never_explain_the_loop_and_have_no_em_dashes()
    {
        foreach (string key in DejaVuLines.AllKeys(_fixture.Map.Keys.ToList()))
        {
            string text = _fixture.Map[key].ToLowerInvariant();
            Assert.DoesNotContain("—", text);
            Assert.DoesNotContain("loop", text);
            Assert.DoesNotContain("reset", text);
            Assert.DoesNotContain("junimo", text);
        }
    }
}
```

- [ ] **Step 3: Implement `DejaVuLines.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Resolves deja-vu line keys: dejavu.&lt;npc&gt;.&lt;tier&gt;.&lt;n&gt; (n from 1, contiguous)
/// with dejavu.default.&lt;tier&gt;.&lt;n&gt; as the pool for any villager without lines. Pools are
/// discovered from the translation key set, so adding a line is a JSON edit.</summary>
public static class DejaVuLines
{
    public const string Prefix = "dejavu.";
    public const string DefaultPool = "default";

    private static List<string> Pool(string slug, int tier, IReadOnlyCollection<string> available)
    {
        var keys = new List<string>();
        var set = available as ISet<string> ?? new HashSet<string>(available, StringComparer.Ordinal);
        for (int n = 1; ; n++)
        {
            string key = $"{Prefix}{slug}.{tier}.{n}";
            if (!set.Contains(key)) break;
            keys.Add(key);
        }
        return keys;
    }

    public static IReadOnlyList<string> KeysFor(string npc, int tier, IReadOnlyCollection<string> available)
    {
        var own = Pool(npc.ToLowerInvariant(), tier, available);
        return own.Count > 0 ? own : Pool(DefaultPool, tier, available);
    }

    /// <summary><paramref name="rollIndex"/> maps a pool size to an index in [0,size).</summary>
    public static string? Pick(string npc, int tier, IReadOnlyCollection<string> available, Func<int, int> rollIndex)
    {
        IReadOnlyList<string> keys = KeysFor(npc, tier, available);
        if (keys.Count == 0) return null;
        int i = Math.Clamp(rollIndex(keys.Count), 0, keys.Count - 1);
        return Strings.Get(keys[i]);
    }

    /// <summary>Every dejavu.* key the family could ask for (the i18n guard executes this).</summary>
    public static IEnumerable<string> AllKeys(IReadOnlyCollection<string> available)
    {
        foreach (string key in available)
            if (key.StartsWith(Prefix, StringComparison.Ordinal))
                yield return key;
    }
}
```

In `I18nGuardTests.ReferencedKeys()` after the `UpgradeCatalog.All` loop:

```csharp
            foreach (string key in DejaVuLines.AllKeys(map.Keys.ToList()))
                _ = Strings.Get(key);
```

- [ ] **Step 4: Run the suite, expect green** (guard sees the new family as reachable).
- [ ] **Step 5: Commit** (manifest 0.16.15): `git commit -m "v0.16.15: deja-vu lines (approved set) and DejaVuLines resolver"`

---

### Task 4: Glue: rollup at day end, heart-event attribution, Harmony postfix, GMCM, debug

**Files:**
- Modify: `src/TheLongestYear/Loop/RelationshipEventIndex.cs` (also record npc per event id: `NpcFor(string eventId) : string?`)
- Create: `src/TheLongestYear/Integration/FamiliarityGlue.cs` (gather signals from `Game1.player`, call the rollup, refresh the snapshot)
- Create: `src/TheLongestYear/Loop/DejaVuDialoguePatch.cs`
- Modify: `src/TheLongestYear/Loop/RunController.cs` `OnDayEnding` (call `FamiliarityGlue.Rollup(_store.State, Run, _monitor)` first thing, before the gate evaluation)
- Modify: `src/TheLongestYear/ModEntry.cs`: `DejaVuDialoguePatch.Connect(...)` where `XpMultiplierPatch.Connect` is wired; GMCM bool after `FestivalMainEventOncePerDay`; console command `tly_dejavu`; i18n keys `gmcm.dejavu.name` / `gmcm.dejavu.tooltip`.

**Interfaces:**
- Consumes: Tasks 1 to 3.
- Produces: `DejaVuDialoguePatch.Enabled`, `DejaVuDialoguePatch.ForceNext : string?` (npc name, consumed on use), `FamiliarityGlue.Rollup(MetaState, RunState, IMonitor)`.

- [ ] **Step 1: `RelationshipEventIndex`**: alongside `_ids`, keep `Dictionary<string,string> _npcByEvent`; where the "f <npc> <points>" segment is parsed, store `npc`. Add `public static string? NpcFor(string eventId) => Ids.Contains(eventId) && _npcByEvent!.TryGetValue(eventId, out string? n) ? n : null;`.

- [ ] **Step 2: `FamiliarityGlue`**

```csharp
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear.Integration
{
    /// <summary>Nightly deja-vu rollup: reads today's talk/gift flags off the live friendship data and
    /// new heart events off eventsSeen (by difference with the last snapshot), then hands the pure
    /// rollup the numbers. Runs before vanilla's own day-end resets those flags.</summary>
    internal static class FamiliarityGlue
    {
        public static void Rollup(MetaState meta, RunState run, IMonitor monitor)
        {
            Farmer p = Game1.player;
            if (p == null) return;
            var previous = new HashSet<string>(run.EventsSeenAtDayStart);
            var heartEventsToday = new Dictionary<string, int>();
            foreach (string id in p.eventsSeen)
            {
                if (previous.Contains(id)) continue;
                string npc = RelationshipEventIndex.NpcFor(id);
                if (npc == null) continue;
                heartEventsToday.TryGetValue(npc, out int n);
                heartEventsToday[npc] = n + 1;
            }
            var signals = new List<VillagerDaySignals>();
            foreach (string name in p.friendshipData.Keys)
            {
                Friendship f = p.friendshipData[name];
                heartEventsToday.TryGetValue(name, out int hearts);
                signals.Add(new VillagerDaySignals(name, f.TalkedToToday, f.GiftsToday, hearts));
            }
            int added = FamiliarityRollup.Apply(meta, signals);
            run.EventsSeenAtDayStart = p.eventsSeen.ToList();
            if (added > 0)
                monitor.Log($"Familiarity rollup: +{added} across {signals.Count(s => s.Talked || s.Gifts > 0 || s.HeartEvents > 0)} villagers.", LogLevel.Trace);
        }
    }
}
```

- [ ] **Step 3: `DejaVuDialoguePatch`**

```csharp
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Deja-vu villager dialogue (spec 2026-08-27). Postfix on the method NPC.checkAction
    /// calls to choose today's line (NPC.cs:2832 -> 4009). After vanilla has chosen, and only on the
    /// LAST call it makes for this talk (noPreface, or the first call already succeeded), push a rare
    /// deja-vu line on top of CurrentDialogue so it plays first and the villager's own line follows.
    /// Never when the top line came from Farmer.activeDialogueEvents (the Introduction line the
    /// 0.16.8 fix re-seeds), never on festival days, never for a spouse.</summary>
    [HarmonyPatch(typeof(NPC), nameof(NPC.checkForNewCurrentDialogue))]
    internal static class DejaVuDialoguePatch
    {
        public static bool Enabled = true;
        /// <summary>Debug: next talk with this villager injects regardless of chance/caps.</summary>
        public static string ForceNext;

        private static MetaState _meta;
        private static System.Func<RunState> _run;
        private static GameplayConfig _config;
        private static IMonitor _monitor;
        private static System.Func<System.Collections.Generic.IReadOnlyCollection<string>> _keys;

        public static void Connect(MetaState meta, System.Func<RunState> run, GameplayConfig config, IMonitor monitor,
            System.Func<System.Collections.Generic.IReadOnlyCollection<string>> translationKeys)
        { _meta = meta; _run = run; _config = config; _monitor = monitor; _keys = translationKeys; }

        private static void Postfix(NPC __instance, bool noPreface, bool __result)
        {
            try
            {
                if (!Enabled || _meta == null || !RunActivation.IsActive) return;
                if (!(noPreface || __result)) return;                 // wait for checkAction's last call
                if (__instance.CurrentDialogue.Count == 0) return;     // nothing is about to play
                if (Game1.isFestival()) return;
                if (__instance.getSpouse() == Game1.player) return;
                Dialogue top = __instance.CurrentDialogue.Peek();
                string tk = top?.TranslationKey ?? "";
                foreach (string key in Game1.player.activeDialogueEvents.Keys)
                    if (key.Length > 0 && tk.EndsWith(":" + key, System.StringComparison.Ordinal)) return;

                string npc = __instance.Name;
                bool force = ForceNext != null && ForceNext == npc;
                RunState run = _run();
                int tier = DejaVuRules.TryPick(_meta, run, npc, (int)Game1.stats.DaysPlayed, _config,
                    max => Game1.random.Next(max), force);
                if (tier == 0) return;
                if (force) ForceNext = null;
                string text = DejaVuLines.Pick(npc, tier, _keys(), size => Game1.random.Next(size));
                if (text == null) return;
                __instance.CurrentDialogue.Push(new Dialogue(__instance, "TLY.dejavu", text));
                _monitor.Log($"Deja-vu: {npc} tier {tier} on day {Game1.stats.DaysPlayed}{(force ? " (forced)" : "")}.", LogLevel.Trace);
            }
            catch (System.Exception ex)
            {
                _monitor?.Log($"DejaVuDialoguePatch failed for {__instance?.Name}: {ex}", LogLevel.Error);
            }
        }
    }
}
```

- [ ] **Step 4: ModEntry wiring.** Next to `XpMultiplierPatch.Connect(...)` (find it; it runs after `_meta.Load()` in OnSaveLoaded or wherever the meta is ready):

```csharp
            TheLongestYear.Loop.DejaVuDialoguePatch.Enabled = _config.EnableDejaVuDialogue;
            TheLongestYear.Loop.DejaVuDialoguePatch.Connect(_meta.State, () => _meta.Run, _config, this.Monitor,
                () => this.Helper.Translation.GetTranslations().Select(t => t.Key).ToList());
```

GMCM after the `FestivalMainEventOncePerDay` option:

```csharp
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.EnableDejaVuDialogue,
                setValue: v => { _config.EnableDejaVuDialogue = v; TheLongestYear.Loop.DejaVuDialoguePatch.Enabled = v; },
                name: () => Strings.Get("gmcm.dejavu.name"),
                tooltip: () => Strings.Get("gmcm.dejavu.tooltip"));
```

i18n: `"gmcm.dejavu.name": "Deja-vu dialogue"`, `"gmcm.dejavu.tooltip": "Villagers you have spent a lot of time with across loops occasionally half-remember you. Rare, no gameplay effect. Off = never."`.

Console command (register + `ExecuteDebugLine` case + handler):

```csharp
        private void CmdDejaVu(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State; RunState run = _meta.Run;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (mode)
            {
                case "set" when args.Length >= 3 && int.TryParse(args[2], out int n):
                    s.VillagerFamiliarity[args[1]] = n;
                    this.Monitor.Log($"tly_dejavu: {args[1]} familiarity = {n}.", LogLevel.Info); break;
                case "force" when args.Length >= 2:
                    TheLongestYear.Loop.DejaVuDialoguePatch.ForceNext = args[1];
                    this.Monitor.Log($"tly_dejavu: next talk with {args[1]} will inject a line.", LogLevel.Info); break;
                case "reset":
                    run.DejaVuShownTo.Clear(); run.DejaVuLastDay = -1;
                    this.Monitor.Log("tly_dejavu: loop caps cleared.", LogLevel.Info); break;
                default:
                    var sb = new System.Text.StringBuilder($"tly_dejavu status: resets={s.CompletedResets} threshold={_config.DejaVuThreshold} chance={_config.DejaVuChancePercent}% lastDay={run.DejaVuLastDay} shownThisLoop=[{string.Join(",", run.DejaVuShownTo)}]\n");
                    foreach (var kv in s.VillagerFamiliarity.OrderByDescending(k => k.Value))
                        sb.Append($"  {kv.Key}={kv.Value} tier={DejaVuRules.Tier(kv.Value, _config.DejaVuThreshold)} eligible={DejaVuRules.IsEligible(s, run, kv.Key, (int)Game1.stats.DaysPlayed, _config.DejaVuThreshold)}\n");
                    this.Monitor.Log(sb.ToString().TrimEnd(), LogLevel.Info); break;
            }
        }
```

- [ ] **Step 5: Build** (`-p:EnableModDeploy=false`), run the suite; expect clean and green.
- [ ] **Step 6: Commit** (manifest 0.16.16): `git commit -m "v0.16.16: deja-vu dialogue: nightly rollup, checkForNewCurrentDialogue postfix, GMCM toggle, tly_dejavu"`

---

### Task 5: Live smoke (ask Jeff first)

- [ ] `tools/deploy.ps1`, `tly_loadsave None_447536393` (already loop >= 1).
- [ ] `tly_dejavu set Pierre 200`, `tly_dejavu force Pierre`, `tly_dejavu status` -> Pierre tier 2 eligible.
- [ ] With Jeff's OK on driving, walk to Pierre's shop and talk (`tools/game.ps1`); expect the tier-2 line, then his normal line; log shows `Deja-vu: Pierre tier 2`. If desktop driving is declined, verify by log only after Jeff talks to him.
- [ ] Introduction guard: `tly_reset`, then `tly_dejavu force Pierre` and talk on Spring 1: Introduction line plays, no deja-vu line, `ForceNext` still set; log shows nothing injected.
- [ ] Sleep once: log shows `Familiarity rollup: +N`; `tly_dejavu status` shows Pierre's counter grew by 1 (talked).
- [ ] Record in STATUS.md and TODO.md.

### Task 6: Docs

- [ ] CHANGELOG Unreleased: "The town half-remembers. Villagers you have spent a lot of time with across loops occasionally say something uncanny (idea: u/Gribbleby). No gameplay effect; toggle in Features."
- [ ] README + Nexus: Features bullet with the same sentence; Credits line "u/Gribbleby for the deja-vu villager dialogue idea". Identical content.
- [ ] Commit docs (no bump).
