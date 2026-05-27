# The Longest Year — v1 Plan 06A: Persistence Effects + Per-Stat Keep Upgrades

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing `OwnedUpgrades` list actually *do something* on every `tly_reset`, and add ~80 chained keep-tier upgrades (tools, fishing rod, skill levels, mine elevator) that retain peak in-run progress across resets using a "cap-not-grant" model. After this plan the player who has spent JP on `keep_iron_hoe` wakes up on Spring 1 with an iron hoe (capped at what they reached this run); a player with `keep_farming_level_5` wakes up at Farming 5 with the profession picker firing again.

**Architecture:** Three-layer split.
1. **Pure Core** (`TheLongestYear.Core`) — extended `MetaState` (new tracking fields + generalised `MeetsMetaRequirement` + `HighestKeptTier` helper), extended `RunState` (`PeakMineFloor`), programmatic catalog generators (`UpgradeCatalog` keeps its hand-authored entries + adds generated ones), and a new pure `RunBaseline` value object + builder that computes the starting state from `MetaState` + `RunState` peaks. All TDD'd.
2. **Mod-side reset application** (`TheLongestYear/Loop`) — `FarmerReset` becomes an instance class that takes a `RunBaseline` and applies it (gold, max items, tool tiers, skill levels with XP flooring). `WorldResetService` applies the world-side baseline (kitchen, mine floor cap, vault pre-pay, horse + stable, pre-built coops/barns, starting animals). A new `ProfessionPickerScheduler` queues `LevelUpMenu` instances for kept L5/L10 skills on day 1 morning.
3. **Mod-side observation** (`TheLongestYear/Loop`) — a `PeakMineFloorTracker` updates `RunState.PeakMineFloor` on `Player.Warped` into a `MineShaft`. `WorldResetService.PerformReset` increments `MetaState.CompletedResets` so the new `season:N` meta-requirement has a producer.

UI changes are minimal: `JunimoShrineMenu` filters out chain-locked and meta-locked rows so the player only sees the next tier they can buy. No new menus.

**Tech Stack:** C# / .NET 6, SMAPI 4.x, Harmony 2.x, MonoGame, xUnit. Stardew 1.6 on PC, verified against the 1.6 decompile at `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android`. The decompile is Android-source; PC fields verified as present before lift (handoff note from 2026-05-27).

**Repo conventions:** Working dir `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`. Build:
`dotnet build src/TheLongestYear/TheLongestYear.csproj`
Test:
`dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Files < 400 lines, tuning in `GameplayConfig`, Core references no game assemblies. Local commits only — **never push without explicit approval**. End every commit body with:
`Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`

**Persistence rule (do NOT regress):** keeps mutate `MetaState` in memory only; the existing `Saving` event hook in `MetaStore.Save` writes to disk. No eager writes.

**Out of scope (deferred to Plan 06B / Plan 06C / LY3):** Cookbook + Craftbook (Phase B), theme bonuses/liabilities (separate plan), Junimo Stash item carryover (Plan 07), friendship per-NPC retention, wallet-flag per-item retention.

---

## File layout after Plan 06A

```
src/TheLongestYear.Core/                              (pure, unit-tested)
  MetaState.cs                  MODIFY: new fields + generalised dispatch + helper
  RunState.cs                   MODIFY: + PeakMineFloor
  UpgradeCatalog.cs             MODIFY: drop carry_xp_*, add generated keep chains
  UpgradeCatalogGenerators.cs   NEW:    static generators for tool/skill/mine chains
  RunBaseline.cs                NEW:    pure value object describing the starting state
  PlayerSnapshot.cs             NEW:    pure value object capturing in-run peaks (tools, skills)
  RunBaselineBuilder.cs         NEW:    pure builder reading MetaState + RunState + snapshot

src/TheLongestYear/                                    (SMAPI mod; game-integration)
  Loop/
    FarmerReset.cs              MODIFY: instance class; applies RunBaseline
    WorldResetService.cs        MODIFY: builds baseline; applies world-side effects;
                                        increments CompletedResets
    ProfessionPickerScheduler.cs NEW:   queues LevelUpMenu for kept L5/L10 skills
    PeakMineFloorTracker.cs     NEW:    SMAPI Warped hook → RunState.PeakMineFloor
  UI/
    JunimoShrineMenu.cs         MODIFY: filter chain-locked + meta-locked rows
  ModEntry.cs                   MODIFY: wire PeakMineFloorTracker; pass ProfessionPickerScheduler

tests/TheLongestYear.Tests/
  RunStateTests.cs              MODIFY: + PeakMineFloor round-trip + BeginNewRun resets it
  MetaStateTests.cs             MODIFY: + new namespace tests + HighestKeptTier tests +
                                          new fields round-trip
  UpgradeCatalogTests.cs        MODIFY: + generated entries present + chain integrity
                                          + carry_xp_* removed
  RunBaselineTests.cs           NEW
  RunBaselineBuilderTests.cs    NEW
```

---

## Task 0: Branch + baseline verification

**Files:** none (git only)

- [ ] **Step 1: Create and switch to the feature branch from current `feat/v1-plan-05-ui`**

Run from `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`:

```bash
git checkout -b feat/v1-plan-06a-persistence-effects
git branch --show-current
```

Expected: `feat/v1-plan-06a-persistence-effects`.

- [ ] **Step 2: Confirm baseline green before touching anything**

Run:

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS (223 existing tests from Plans 01–05 + handoff doc commits).

- [ ] **Step 3: Confirm the mod builds clean**

Run:

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

Expected: build SUCCESS, no errors, no warnings.

---

## Task 1: `RunState.PeakMineFloor` (Core)

**Files:**
- Modify: `src/TheLongestYear.Core/RunState.cs`
- Modify: `tests/TheLongestYear.Tests/RunStateTests.cs`

The per-run peak mine floor reached. Restored on reset to `min(owned_keep_floor, run_peak_floor)`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/RunStateTests.cs`:

```csharp
[Fact]
public void PeakMineFloor_defaults_to_zero_and_round_trips_through_json()
{
    var fresh = new RunState();
    Assert.Equal(0, fresh.PeakMineFloor);

    var original = new RunState { PeakMineFloor = 65 };
    string json = System.Text.Json.JsonSerializer.Serialize(original);
    RunState restored = System.Text.Json.JsonSerializer.Deserialize<RunState>(json)!;
    Assert.Equal(65, restored.PeakMineFloor);
}

[Fact]
public void BeginNewRun_resets_PeakMineFloor()
{
    var run = new RunState { PeakMineFloor = 90 };
    run.BeginNewRun(seed: 42);
    Assert.Equal(0, run.PeakMineFloor);
}

[Fact]
public void RecordMineFloor_takes_the_max_and_ignores_shallower_floors()
{
    var run = new RunState();
    run.RecordMineFloor(20);
    Assert.Equal(20, run.PeakMineFloor);
    run.RecordMineFloor(10);
    Assert.Equal(20, run.PeakMineFloor);
    run.RecordMineFloor(45);
    Assert.Equal(45, run.PeakMineFloor);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~RunStateTests.PeakMineFloor|FullyQualifiedName~RunStateTests.RecordMineFloor|FullyQualifiedName~RunStateTests.BeginNewRun_resets_Peak"
```

Expected: FAIL (compiler errors: `PeakMineFloor`, `RecordMineFloor` don't exist).

- [ ] **Step 3: Add the field, the recorder, and the BeginNewRun reset**

Edit `src/TheLongestYear.Core/RunState.cs`. Add this property next to the other per-run fields (after `VaultBundlesPaid`):

```csharp
/// <summary>Deepest mine floor reached this run. Used by RunBaseline to cap the
/// restored mine elevator floor on reset (cap-not-grant). Updated by
/// PeakMineFloorTracker (mod-side) on Player.Warped into a MineShaft.</summary>
public int PeakMineFloor { get; set; }

/// <summary>Record having reached the given floor this run. Idempotent for shallower
/// floors — only deeper reaches update the peak.</summary>
public void RecordMineFloor(int floor)
{
    if (floor > PeakMineFloor)
        PeakMineFloor = floor;
}
```

In `BeginNewRun`, add the reset line alongside the other per-run resets:

```csharp
PeakMineFloor = 0;
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~RunStateTests"
```

Expected: PASS (all RunStateTests, including the three new ones).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/RunState.cs tests/TheLongestYear.Tests/RunStateTests.cs
git commit -m "$(cat <<'EOF'
feat(core): track per-run peak mine floor on RunState

Adds RunState.PeakMineFloor + RecordMineFloor recorder + BeginNewRun reset.
Phase A persistence design §B: mine elevator is capped at min(owned_keep_floor,
in-run_peak_floor) — this is the in-run cap side. PeakMineFloorTracker (mod
side, later task) populates it on Player.Warped into a MineShaft.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: `MetaState` — new tracking fields

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Modify: `tests/TheLongestYear.Tests/MetaStateTests.cs`

Three new fields to back the generalised `MeetsMetaRequirement` namespaces added in Task 3. Producers for `CompletedQuestsEver` and `MailFlagsEverReceived` are out of scope for Phase A — the fields exist so future plans don't have to touch the state class. `CompletedResets` gets its single producer wired in Task 12.

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/MetaStateTests.cs`:

```csharp
[Fact]
public void New_meta_state_starts_with_zero_completed_resets_and_empty_quest_mail_lists()
{
    var s = new MetaState();
    Assert.Equal(0, s.CompletedResets);
    Assert.Empty(s.CompletedQuestsEver);
    Assert.Empty(s.MailFlagsEverReceived);
}

[Fact]
public void New_tracking_fields_round_trip_through_json()
{
    var original = new MetaState
    {
        CompletedResets = 7,
        CompletedQuestsEver = { "quest_a", "quest_b" },
        MailFlagsEverReceived = { "ccPantry", "JojaMember" }
    };
    string json = System.Text.Json.JsonSerializer.Serialize(original);
    MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;
    Assert.Equal(7, restored.CompletedResets);
    Assert.Equal(new[] { "quest_a", "quest_b" }, restored.CompletedQuestsEver);
    Assert.Equal(new[] { "ccPantry", "JojaMember" }, restored.MailFlagsEverReceived);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests.New_meta_state_starts_with_zero|FullyQualifiedName~MetaStateTests.New_tracking_fields"
```

Expected: FAIL (compiler errors: members don't exist).

- [ ] **Step 3: Add the fields**

Edit `src/TheLongestYear.Core/MetaState.cs`. Add after `AnimalSpeciesEverOwned`:

```csharp
/// <summary>
/// Number of completed resets in this playthrough. Incremented inside
/// WorldResetService.PerformReset (Plan 06A). Backs the season:&lt;n&gt; meta-requirement
/// namespace (player has done at least N resets).
/// </summary>
public int CompletedResets { get; set; }

/// <summary>
/// Quest ids the player has completed across all runs in this playthrough. Backs the
/// quest:&lt;id&gt; meta-requirement namespace. Producer is part of a later plan; declared
/// here so future plans don't have to touch the state class.
/// </summary>
public List<string> CompletedQuestsEver { get; set; } = new();

/// <summary>
/// Mail flags the player has ever received across all runs in this playthrough. Backs
/// the mail:&lt;flag&gt; meta-requirement namespace. Producer is part of a later plan.
/// </summary>
public List<string> MailFlagsEverReceived { get; set; } = new();
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests"
```

Expected: PASS (all MetaStateTests).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/MetaState.cs tests/TheLongestYear.Tests/MetaStateTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add CompletedResets, CompletedQuestsEver, MailFlagsEverReceived to MetaState

Phase A persistence design §E backing state for generalised MeetsMetaRequirement.
CompletedResets gets its producer wired in WorldResetService (Plan 06A Task 12).
CompletedQuestsEver and MailFlagsEverReceived are declared empty here so future
plans (LY3 perfection chase) don't have to migrate the state schema.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Generalise `MetaState.MeetsMetaRequirement`

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Modify: `tests/TheLongestYear.Tests/MetaStateTests.cs`

Add `upgrade:`, `quest:`, `mail:`, `season:` namespaces. `species:` keeps working unchanged. Unknown namespaces still default-deny (existing behaviour).

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/MetaStateTests.cs`:

```csharp
[Fact]
public void MeetsMetaRequirement_upgrade_returns_true_when_owned()
{
    var s = new MetaState { OwnedUpgrades = { "backpack_1" } };
    Assert.True(s.MeetsMetaRequirement("upgrade:backpack_1"));
    Assert.False(s.MeetsMetaRequirement("upgrade:backpack_2"));
}

[Fact]
public void MeetsMetaRequirement_quest_checks_CompletedQuestsEver()
{
    var s = new MetaState { CompletedQuestsEver = { "quest_a" } };
    Assert.True(s.MeetsMetaRequirement("quest:quest_a"));
    Assert.False(s.MeetsMetaRequirement("quest:never_done"));
}

[Fact]
public void MeetsMetaRequirement_mail_checks_MailFlagsEverReceived_case_insensitive()
{
    var s = new MetaState { MailFlagsEverReceived = { "ccPantry" } };
    Assert.True(s.MeetsMetaRequirement("mail:ccPantry"));
    Assert.True(s.MeetsMetaRequirement("mail:ccpantry"));   // case-insensitive like species:
    Assert.False(s.MeetsMetaRequirement("mail:JojaMember"));
}

[Fact]
public void MeetsMetaRequirement_season_compares_int_threshold_to_CompletedResets()
{
    var s = new MetaState { CompletedResets = 3 };
    Assert.True(s.MeetsMetaRequirement("season:0"));
    Assert.True(s.MeetsMetaRequirement("season:3"));
    Assert.False(s.MeetsMetaRequirement("season:4"));
}

[Fact]
public void MeetsMetaRequirement_season_returns_false_when_value_is_not_an_int()
{
    var s = new MetaState { CompletedResets = 5 };
    Assert.False(s.MeetsMetaRequirement("season:abc"));
    Assert.False(s.MeetsMetaRequirement("season:"));
}

[Fact]
public void MeetsMetaRequirement_unknown_namespace_still_returns_false()
{
    var s = new MetaState();
    Assert.False(s.MeetsMetaRequirement("future:something"));
    Assert.False(s.MeetsMetaRequirement("malformed-no-colon"));
}
```

The existing `MeetsMetaRequirement_unknown_namespace_returns_false` test stays — the new test reinforces the contract for forward-compat.

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests.MeetsMetaRequirement"
```

Expected: the four NEW tests FAIL ("Expected: True, Actual: False" for the upgrade/quest/mail/season passes; the existing tests still pass).

- [ ] **Step 3: Generalise the dispatcher**

In `src/TheLongestYear.Core/MetaState.cs`, replace the body of `MeetsMetaRequirement` with:

```csharp
public bool MeetsMetaRequirement(string? requirement)
{
    if (string.IsNullOrEmpty(requirement))
        return true;
    int colon = requirement.IndexOf(':');
    if (colon <= 0)
        return false;
    string ns = requirement.Substring(0, colon);
    string value = requirement.Substring(colon + 1);
    return ns switch
    {
        "species" => AnimalSpeciesEverOwned.Contains(value, StringComparer.OrdinalIgnoreCase),
        "upgrade" => OwnedUpgrades.Contains(value, StringComparer.Ordinal),
        "quest"   => CompletedQuestsEver.Contains(value, StringComparer.Ordinal),
        "mail"    => MailFlagsEverReceived.Contains(value, StringComparer.OrdinalIgnoreCase),
        "season"  => int.TryParse(value, out int threshold) && CompletedResets >= threshold,
        _ => false
    };
}
```

Notes on comparer choice:
- `species` and `mail` are case-insensitive because vanilla Stardew is sloppy about casing in those domains (e.g. animal type strings differ across `Data/FarmAnimals` versus what `FarmAnimal.type` returns; mail flags appear with both cases in CC code).
- `upgrade` and `quest` are case-sensitive because their values are our own ids and vanilla quest ids — both kept lowercase by convention.

Update the XML doc comment above the method to reflect the wider dispatch table.

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests"
```

Expected: PASS (all MetaStateTests).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/MetaState.cs tests/TheLongestYear.Tests/MetaStateTests.cs
git commit -m "$(cat <<'EOF'
feat(core): generalise MetaState.MeetsMetaRequirement with upgrade/quest/mail/season

Phase A persistence design §E. Existing species: dispatch unchanged. Unknown
namespaces still default-deny so older code paths remain forward-compatible.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `MetaState.HighestKeptTier` helper

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Modify: `tests/TheLongestYear.Tests/MetaStateTests.cs`

Pure helper used by `RunBaselineBuilder` (Task 9). Given a prefix like `keep_hoe_` and a max tier (e.g. 4 for tools), returns the highest integer suffix N such that `keep_hoe_N` is owned. Returns 0 if none owned.

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/MetaStateTests.cs`:

```csharp
[Fact]
public void HighestKeptTier_returns_zero_when_no_keep_upgrades_owned()
{
    var s = new MetaState();
    Assert.Equal(0, s.HighestKeptTier("keep_hoe_", maxTier: 4));
}

[Fact]
public void HighestKeptTier_returns_the_highest_owned_tier()
{
    var s = new MetaState
    {
        OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2", "keep_hoe_3", "backpack_1" }
    };
    Assert.Equal(3, s.HighestKeptTier("keep_hoe_", maxTier: 4));
}

[Fact]
public void HighestKeptTier_ignores_non_matching_prefixes()
{
    var s = new MetaState { OwnedUpgrades = { "keep_axe_4", "keep_hoe_1" } };
    Assert.Equal(1, s.HighestKeptTier("keep_hoe_", maxTier: 4));
}

[Fact]
public void HighestKeptTier_caps_at_maxTier_and_ignores_higher_owned_ids()
{
    // Defensive: if a hand-edited save somehow has keep_hoe_99, we cap at maxTier.
    var s = new MetaState { OwnedUpgrades = { "keep_hoe_99" } };
    Assert.Equal(0, s.HighestKeptTier("keep_hoe_", maxTier: 4));
}

[Fact]
public void HighestKeptTier_skips_non_numeric_suffixes()
{
    var s = new MetaState { OwnedUpgrades = { "keep_hoe_iridium", "keep_hoe_2" } };
    Assert.Equal(2, s.HighestKeptTier("keep_hoe_", maxTier: 4));
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests.HighestKeptTier"
```

Expected: FAIL (compiler error: `HighestKeptTier` doesn't exist).

- [ ] **Step 3: Add the helper**

Add to `src/TheLongestYear.Core/MetaState.cs` (after `HasUpgrade`):

```csharp
/// <summary>
/// Return the highest integer N such that an upgrade with id "<paramref name="prefix"/>{N}"
/// is owned, where 1 ≤ N ≤ <paramref name="maxTier"/>. Returns 0 if none in range are
/// owned. Used by the reset baseline builder to find the highest "keep" tier the player
/// has banked for a given tool/skill/floor chain — the cap-not-grant model.
/// </summary>
public int HighestKeptTier(string prefix, int maxTier)
{
    int best = 0;
    foreach (string id in OwnedUpgrades)
    {
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            continue;
        string suffix = id.Substring(prefix.Length);
        if (!int.TryParse(suffix, out int n))
            continue;
        if (n < 1 || n > maxTier)
            continue;
        if (n > best)
            best = n;
    }
    return best;
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/MetaState.cs tests/TheLongestYear.Tests/MetaStateTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add MetaState.HighestKeptTier(prefix, maxTier) helper

Used by RunBaselineBuilder to resolve the player's highest banked tier for the
keep_{tool}_N, keep_{skill}_N, keep_mine_elevator_N chains. Pure scan over
OwnedUpgrades with prefix match + int suffix parse; caps at maxTier so a hand-
edited save can't poke a tool to UpgradeLevel 99.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: `UpgradeCatalogGenerators` + Loadout keep entries (tool tiers + fishing rod)

**Files:**
- Create: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs`
- Modify: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

16 tool-tier entries + 2 fishing rod entries (18 total) added to `UpgradeCategory.Loadout`. Chained — each tier requires the previous. Spec §B "Tool tiers" + "Fishing rod".

Per the spec: "Cost tuning (2026-05-26): every entry was bumped ~1.5× and rounded to the nearest 25." The hand-authored Loadout entries (`backpack_1=150`, `backpack_2=375`) give us the pricing curve to match. For the keep-tier chains, base each tier's cost on roughly the JP equivalent of its in-game gold cost, scaled to the JP economy:

| Tier | Vanilla gold cost | JP cost (this plan) |
|---|---|---|
| Copper (T1) | 2,000g | 150 JP |
| Steel (T2) | 5,000g | 300 JP |
| Gold (T3) | 10,000g | 525 JP |
| Iridium (T4) | 25,000g | 875 JP |

Fishing rod:
| Tier | Vanilla gold cost | JP cost |
|---|---|---|
| Fiberglass | 1,800g | 150 JP |
| Iridium    | 7,500g | 425 JP |

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`:

```csharp
[Theory]
[InlineData("keep_hoe_")]
[InlineData("keep_pickaxe_")]
[InlineData("keep_axe_")]
[InlineData("keep_watering_can_")]
public void Tool_keep_chains_have_four_tiers_each(string prefix)
{
    var rows = UpgradeCatalog.All.Where(u => u.Id.StartsWith(prefix)).ToList();
    Assert.Equal(4, rows.Count);
    Assert.Equal(new[] { prefix + "1", prefix + "2", prefix + "3", prefix + "4" },
        rows.Select(r => r.Id));
    Assert.All(rows, r => Assert.Equal(UpgradeCategory.Loadout, r.Category));
}

[Theory]
[InlineData("keep_hoe_")]
[InlineData("keep_pickaxe_")]
[InlineData("keep_axe_")]
[InlineData("keep_watering_can_")]
public void Tool_keep_chains_are_prerequisite_chained(string prefix)
{
    Assert.Null(UpgradeCatalog.TryGet(prefix + "1")!.PrerequisiteId);
    Assert.Equal(prefix + "1", UpgradeCatalog.TryGet(prefix + "2")!.PrerequisiteId);
    Assert.Equal(prefix + "2", UpgradeCatalog.TryGet(prefix + "3")!.PrerequisiteId);
    Assert.Equal(prefix + "3", UpgradeCatalog.TryGet(prefix + "4")!.PrerequisiteId);
}

[Fact]
public void Fishing_rod_keep_chain_has_two_tiers_chained()
{
    var t1 = UpgradeCatalog.TryGet("keep_fishing_rod_1");
    var t2 = UpgradeCatalog.TryGet("keep_fishing_rod_2");
    Assert.NotNull(t1);
    Assert.NotNull(t2);
    Assert.Null(t1!.PrerequisiteId);
    Assert.Equal("keep_fishing_rod_1", t2!.PrerequisiteId);
    Assert.Equal(UpgradeCategory.Loadout, t1.Category);
    Assert.Equal(UpgradeCategory.Loadout, t2.Category);
}

[Fact]
public void Tool_keep_tier_costs_climb_monotonically()
{
    foreach (string prefix in new[] { "keep_hoe_", "keep_pickaxe_", "keep_axe_", "keep_watering_can_" })
    {
        long c1 = UpgradeCatalog.TryGet(prefix + "1")!.Cost;
        long c2 = UpgradeCatalog.TryGet(prefix + "2")!.Cost;
        long c3 = UpgradeCatalog.TryGet(prefix + "3")!.Cost;
        long c4 = UpgradeCatalog.TryGet(prefix + "4")!.Cost;
        Assert.True(c1 < c2 && c2 < c3 && c3 < c4,
            $"{prefix} costs must strictly increase: got {c1},{c2},{c3},{c4}");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests.Tool_keep|FullyQualifiedName~UpgradeCatalogTests.Fishing_rod_keep"
```

Expected: FAIL (`TryGet` returns null for all of these).

- [ ] **Step 3: Create the generator**

Create `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Programmatic generators for the long chained "keep_*" entries in UpgradeCatalog
/// (Phase A persistence design §B). Hand-authoring 80+ entries would be brittle;
/// these generators emit them deterministically. Cost tables baked in here keep the
/// pricing curve documented in one place per chain.
/// </summary>
internal static class UpgradeCatalogGenerators
{
    // Tool tier costs (Copper → Steel → Gold → Iridium). Loose mirror of vanilla
    // gold costs (2k/5k/10k/25k) scaled into the JP economy. Same table used for
    // hoe / pickaxe / axe / watering can — the player pays the same JP for any
    // tool's tier-up keep.
    private static readonly long[] ToolTierCosts = { 150, 300, 525, 875 };

    private static readonly (string IdSlug, string DisplayName)[] ToolKinds =
    {
        ("hoe",          "Hoe"),
        ("pickaxe",      "Pickaxe"),
        ("axe",          "Axe"),
        ("watering_can", "Watering Can"),
    };

    // Tier 1=Copper, 2=Steel, 3=Gold, 4=Iridium. Matches Tool.UpgradeLevel
    // (StardewValley\StardewValley\Tool.cs:167) which is 0=base/rusty, 1=copper, ...
    private static readonly string[] TierNames = { "Copper", "Steel", "Gold", "Iridium" };

    // Fishing rod has just two upgrade tiers worth keeping (training rod is L0,
    // bamboo rod is L1 — the player gets a bamboo rod from Willy day 2 of every
    // run so there's no value in a "keep bamboo" entry).
    private static readonly (string Id, string DisplayName, long Cost, string? Prereq)[] FishingRodTiers =
    {
        ("keep_fishing_rod_1", "Keep Fiberglass Rod", 150, null),
        ("keep_fishing_rod_2", "Keep Iridium Rod",    425, "keep_fishing_rod_1"),
    };

    /// <summary>Yield all Loadout keep-tier entries (16 tools + 2 fishing rod = 18 rows).</summary>
    public static IEnumerable<UpgradeDefinition> LoadoutToolKeeps()
    {
        foreach (var (slug, displayName) in ToolKinds)
            for (int tier = 1; tier <= 4; tier++)
            {
                string id = $"keep_{slug}_{tier}";
                string? prereq = tier == 1 ? null : $"keep_{slug}_{tier - 1}";
                string name = $"Keep {TierNames[tier - 1]} {displayName}";
                string desc = $"Start each run with your {displayName} at the {TierNames[tier - 1]} tier " +
                              "or whatever lower tier you actually reached this run, whichever is lower.";
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Loadout, name, desc, ToolTierCosts[tier - 1], prereq);
            }

        foreach (var (id, name, cost, prereq) in FishingRodTiers)
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Loadout, name,
                "Start each run with your Fishing Rod at this tier (capped at your in-run reach).",
                cost, prereq);
    }
}
```

- [ ] **Step 4: Wire the generator into `UpgradeCatalog.Build()`**

Edit `src/TheLongestYear.Core/UpgradeCatalog.cs`. Change the `Build` method to concat the generated entries after the hand-authored list. Replace:

```csharp
    private static IReadOnlyList<UpgradeDefinition> Build() => new List<UpgradeDefinition>
    {
```

with:

```csharp
    private static IReadOnlyList<UpgradeDefinition> Build()
    {
        var entries = new List<UpgradeDefinition>
        {
```

…and at the very end of the existing list literal, replace the closing `    };` with:

```csharp
        };
        entries.AddRange(UpgradeCatalogGenerators.LoadoutToolKeeps());
        return entries;
    }
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests"
```

Expected: PASS (every existing UpgradeCatalogTests test, plus all four new tests).

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/UpgradeCatalogGenerators.cs src/TheLongestYear.Core/UpgradeCatalog.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add 18 chained Loadout keep-tier entries (4 tools × 4 tiers + 2 rods)

Phase A persistence design §B. Programmatic generation keeps the catalog file
small even with the long chains. Cost curve mirrors vanilla gold (2k/5k/10k/25k
for tools) scaled into the JP economy.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Carryover skill-level keep entries (50 rows)

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs`
- Modify: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

5 skills × 10 levels, chained L1 → L10 per skill, in `UpgradeCategory.Carryover`. Pricing curve climbs steeply at L5 / L10 to reflect the profession unlocks.

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`:

```csharp
[Theory]
[InlineData("keep_farming_level_")]
[InlineData("keep_mining_level_")]
[InlineData("keep_foraging_level_")]
[InlineData("keep_fishing_level_")]
[InlineData("keep_combat_level_")]
public void Skill_level_keep_chains_have_ten_tiers_chained(string prefix)
{
    for (int level = 1; level <= 10; level++)
    {
        var def = UpgradeCatalog.TryGet(prefix + level);
        Assert.NotNull(def);
        Assert.Equal(UpgradeCategory.Carryover, def!.Category);
        Assert.Equal(
            level == 1 ? null : prefix + (level - 1),
            def.PrerequisiteId);
    }
}

[Fact]
public void Skill_level_keep_chains_add_fifty_total_entries()
{
    int count = UpgradeCatalog.All.Count(u => u.Id.Contains("_level_"));
    Assert.Equal(50, count);
}

[Fact]
public void Skill_level_keep_costs_climb_monotonically_and_jump_at_5_and_10()
{
    long c4 = UpgradeCatalog.TryGet("keep_farming_level_4")!.Cost;
    long c5 = UpgradeCatalog.TryGet("keep_farming_level_5")!.Cost;
    long c9 = UpgradeCatalog.TryGet("keep_farming_level_9")!.Cost;
    long c10 = UpgradeCatalog.TryGet("keep_farming_level_10")!.Cost;
    Assert.True(c5 > c4 * 1.5, $"L5 should noticeably exceed L4 (profession unlock): {c4} → {c5}");
    Assert.True(c10 > c9 * 1.5, $"L10 should noticeably exceed L9 (profession unlock): {c9} → {c10}");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests.Skill_level"
```

Expected: FAIL.

- [ ] **Step 3: Extend the generator**

Add to `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`:

```csharp
    // Skill level keep costs, indexed [1..10]. Levels 5 and 10 jump because they
    // also re-trigger the profession picker (Phase A persistence design §B), so the
    // player is really buying both the level AND the profession-pick option.
    private static readonly long[] SkillLevelCosts =
    {
        0,         // index 0 unused
        50, 100, 175, 275,        // L1–L4
        500,                       // L5 — profession unlock
        650, 800, 1000, 1250,      // L6–L9
        2000                       // L10 — final profession unlock
    };

    private static readonly (string IdSlug, string DisplayName)[] SkillKinds =
    {
        ("farming",  "Farming"),
        ("mining",   "Mining"),
        ("foraging", "Foraging"),
        ("fishing",  "Fishing"),
        ("combat",   "Combat"),
    };

    /// <summary>Yield all 50 Carryover keep-skill-level entries.</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverSkillLevelKeeps()
    {
        foreach (var (slug, displayName) in SkillKinds)
            for (int level = 1; level <= 10; level++)
            {
                string id = $"keep_{slug}_level_{level}";
                string? prereq = level == 1 ? null : $"keep_{slug}_level_{level - 1}";
                string name = $"Keep {displayName} Level {level}";
                string desc = $"Start each run at {displayName} Level {level} (or whatever lower " +
                              "level you actually reached). XP is set to the level threshold — no " +
                              "half-progress preserved." +
                              (level == 5 || level == 10
                                ? $" Re-triggers the profession picker for Level {level}."
                                : "");
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Carryover, name, desc, SkillLevelCosts[level], prereq);
            }
    }
```

- [ ] **Step 4: Add to `UpgradeCatalog.Build()`**

In `src/TheLongestYear.Core/UpgradeCatalog.cs`, append the new generator after the tool keeps:

```csharp
        entries.AddRange(UpgradeCatalogGenerators.LoadoutToolKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverSkillLevelKeeps());
        return entries;
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/UpgradeCatalogGenerators.cs src/TheLongestYear.Core/UpgradeCatalog.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add 50 chained Carryover keep-skill-level entries (5 skills × L1–L10)

Phase A persistence design §B. L5 and L10 jump in cost because they also re-trigger
the profession picker — the player is buying both the level retention AND the
profession-repick option in the same row.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Carryover mine-elevator keep entries (12 rows)

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs`
- Modify: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

12 chained entries `keep_mine_elevator_10` → `..._120`. Cost climbs to match the depth.

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`:

```csharp
[Fact]
public void Mine_elevator_keep_chain_has_twelve_entries_in_steps_of_ten()
{
    for (int floor = 10; floor <= 120; floor += 10)
    {
        var def = UpgradeCatalog.TryGet($"keep_mine_elevator_{floor}");
        Assert.NotNull(def);
        Assert.Equal(UpgradeCategory.Carryover, def!.Category);
    }
    Assert.Equal(12, UpgradeCatalog.All.Count(u => u.Id.StartsWith("keep_mine_elevator_")));
}

[Fact]
public void Mine_elevator_keep_chain_is_prerequisite_chained()
{
    Assert.Null(UpgradeCatalog.TryGet("keep_mine_elevator_10")!.PrerequisiteId);
    for (int floor = 20; floor <= 120; floor += 10)
        Assert.Equal(
            $"keep_mine_elevator_{floor - 10}",
            UpgradeCatalog.TryGet($"keep_mine_elevator_{floor}")!.PrerequisiteId);
}

[Fact]
public void Mine_elevator_keep_costs_climb_monotonically()
{
    long prev = 0;
    for (int floor = 10; floor <= 120; floor += 10)
    {
        long cost = UpgradeCatalog.TryGet($"keep_mine_elevator_{floor}")!.Cost;
        Assert.True(cost > prev, $"floor {floor} cost {cost} should exceed prev {prev}");
        prev = cost;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests.Mine_elevator"
```

Expected: FAIL.

- [ ] **Step 3: Extend the generator**

Add to `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`:

```csharp
    /// <summary>Yield all 12 Carryover keep-mine-elevator-floor entries (10–120 step 10).</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverMineElevatorKeeps()
    {
        for (int floor = 10; floor <= 120; floor += 10)
        {
            string id = $"keep_mine_elevator_{floor}";
            string? prereq = floor == 10 ? null : $"keep_mine_elevator_{floor - 10}";
            // Cost ramp: 75 JP for floor 10, +100 per 10 floors → 1175 JP for floor 120.
            // Caps below the Iridium-tool tiers since this is a single floor, not the whole
            // tool tier — buy a couple of tier keeps before a deep elevator.
            long cost = 75 + ((floor - 10) / 10) * 100;
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Carryover,
                $"Keep Mine Elevator Floor {floor}",
                $"Start each run with the mine elevator accessible to floor {floor} (or your " +
                "in-run deepest floor, whichever is shallower).",
                cost, prereq);
        }
    }
```

- [ ] **Step 4: Add to `UpgradeCatalog.Build()`**

In `src/TheLongestYear.Core/UpgradeCatalog.cs`, append after the skill keeps:

```csharp
        entries.AddRange(UpgradeCatalogGenerators.LoadoutToolKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverSkillLevelKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverMineElevatorKeeps());
        return entries;
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/UpgradeCatalogGenerators.cs src/TheLongestYear.Core/UpgradeCatalog.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add 12 chained Carryover keep-mine-elevator entries (floors 10–120)

Phase A persistence design §B. Cap is the in-run PeakMineFloor (Task 1) so a
player who only reached floor 60 with keep_mine_elevator_80 banked still wakes
up with floor 60 access.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Remove the deprecated `carry_xp_*` entries

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs`
- Modify: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

Spec §B: "DEPRECATED; replaced by per-level skill keeps... remove cleanly with no migration since no one has banked them yet."

- [ ] **Step 1: Write the failing test**

Add to `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`:

```csharp
[Fact]
public void Deprecated_carry_xp_entries_have_been_removed()
{
    Assert.Null(UpgradeCatalog.TryGet("carry_xp_25"));
    Assert.Null(UpgradeCatalog.TryGet("carry_xp_50"));
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests.Deprecated_carry_xp"
```

Expected: FAIL (entries still in catalog).

- [ ] **Step 3: Delete the entries**

In `src/TheLongestYear.Core/UpgradeCatalog.cs`, delete this block (currently lines 41–44):

```csharp
        // Carryover
        new UpgradeDefinition("carry_xp_25", UpgradeCategory.Carryover, "Carryover XP I",
            "Retain 25% of your peak skill XP across runs.", 225),
        new UpgradeDefinition("carry_xp_50", UpgradeCategory.Carryover, "Carryover XP II",
            "Retain 50% of your peak skill XP across runs.", 600, "carry_xp_25"),
```

Leave a one-line comment in its place so future readers know why the section is empty:

```csharp
        // (Carryover: hand-authored entries removed in Plan 06A — replaced by the 50
        // programmatically-generated keep_<skill>_level_N entries below.)
```

- [ ] **Step 4: Run tests to verify all pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS. The existing `Every_category_has_at_least_one_entry` test still passes because the 50 skill keeps + 12 mine elevator keeps fill `Carryover`.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/UpgradeCatalog.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "$(cat <<'EOF'
feat(core): remove deprecated carry_xp_25/50 entries

Replaced by the 50 keep_<skill>_level_N entries added in Plan 06A Task 6.
No migration needed — these were catalog-only with no shipped effect, and no
real save has banked them per the design handoff.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: `RunBaseline` + `PlayerSnapshot` + `RunBaselineBuilder`

**Files:**
- Create: `src/TheLongestYear.Core/RunBaseline.cs`
- Create: `src/TheLongestYear.Core/PlayerSnapshot.cs`
- Create: `src/TheLongestYear.Core/RunBaselineBuilder.cs`
- Create: `tests/TheLongestYear.Tests/RunBaselineTests.cs`
- Create: `tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs`

Three pure types form the testable seam between Core and the mod-side reset code:
- `RunBaseline` — full description of what the reset should apply.
- `PlayerSnapshot` — the in-run peak tool tiers + skill levels read from the live player BEFORE the wipe (the cap side of cap-not-grant; mine elevator's peak comes from `RunState.PeakMineFloor` and stays on RunState).
- `RunBaselineBuilder` — derives `RunBaseline` from `(MetaState, RunState, PlayerSnapshot, defaultStartingMoney)`.

- [ ] **Step 1: Create the value object**

Create `src/TheLongestYear.Core/RunBaseline.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// The full description of the player + world's starting state at the top of a new run,
/// derived from <see cref="MetaState"/> (banked upgrades) and <see cref="RunState"/>
/// (in-run peaks). The mod-side reset code translates this into game-state writes.
///
/// Pure data — no game refs. Defaults represent a brand-new save with zero keep-upgrades
/// purchased: 500g, 12 inventory slots, all tools at base tier, no skill levels, no kept
/// buildings, no animals, no horse, no kitchen, no bus, no mine elevator.
/// </summary>
public sealed class RunBaseline
{
    public int StartingGold { get; init; } = 500;
    public int MaxItems { get; init; } = 12;

    /// <summary>Tool kind slug → <c>Tool.UpgradeLevel</c> the player starts the run holding.
    /// Slug list: <c>hoe</c>, <c>pickaxe</c>, <c>axe</c>, <c>watering_can</c> (0..4 each),
    /// and <c>fishing_rod</c> (2 = Fiberglass, 3 = Iridium — bamboo at UpgradeLevel 1 is
    /// vanilla Willy day-2 grant, not represented). Missing keys mean "leave at the vanilla
    /// baseline that Game1.loadForNewGame produced" (rusty hoe/pickaxe/axe/watering can; no rod).</summary>
    public IReadOnlyDictionary<string, int> ToolTiers { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Vanilla skill-index (0=Farming, 1=Fishing, 2=Foraging, 3=Mining, 4=Combat)
    /// → level 1..10 to restore at run start. XP is floored to that level's threshold via
    /// <c>Farmer.getBaseExperienceForLevel</c>.</summary>
    public IReadOnlyDictionary<int, int> SkillLevels { get; init; }
        = new Dictionary<int, int>();

    /// <summary>Skill indexes (subset of <see cref="SkillLevels"/>) whose restored level
    /// is 5 or 10 — the reset code queues a <c>LevelUpMenu</c> for each so the player can
    /// re-pick their profession.</summary>
    public IReadOnlyList<int> ProfessionPickerSkillsToRequeue { get; init; }
        = new List<int>();

    /// <summary>Mine elevator floor (10..120 in steps of 10) accessible at run start.
    /// 0 means "no elevator" (vanilla baseline: -1 sentinel).</summary>
    public int MineElevatorFloor { get; init; }

    /// <summary>True if the player gets the Kitchen house upgrade on day 1.</summary>
    public bool KitchenOnDay1 { get; init; }

    /// <summary>True if the four Vault bundles should be marked paid (bus restored).</summary>
    public bool BusUnlocked { get; init; }

    /// <summary>True if the early-horse upgrade should spawn a horse + stable on day 1.</summary>
    public bool EarlyHorse { get; init; }

    /// <summary>Building blueprint names to pre-build on the farm (e.g. "Coop", "Deluxe Barn").
    /// Each ends up at a deterministic tile per <see cref="BuildingPreplacement"/>.</summary>
    public IReadOnlyList<string> KeptBuildings { get; init; } = new List<string>();

    /// <summary>Animal species + count to place into the matching housing on day 1.
    /// Tuple = (vanilla animal type string, building blueprint required).</summary>
    public IReadOnlyList<StartingAnimal> StartingAnimals { get; init; } = new List<StartingAnimal>();
}

/// <summary>One starting-animal entry. The reset code finds a building of <c>HousingType</c>
/// on the farm and adds an animal of <c>VanillaType</c>.</summary>
public sealed record StartingAnimal(string VanillaType, string HousingType);
```

- [ ] **Step 2: Write the value-object tests**

Create `tests/TheLongestYear.Tests/RunBaselineTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunBaselineTests
{
    [Fact]
    public void Defaults_match_vanilla_baseline()
    {
        var b = new RunBaseline();
        Assert.Equal(500, b.StartingGold);
        Assert.Equal(12, b.MaxItems);
        Assert.Empty(b.ToolTiers);
        Assert.Empty(b.SkillLevels);
        Assert.Empty(b.ProfessionPickerSkillsToRequeue);
        Assert.Equal(0, b.MineElevatorFloor);
        Assert.False(b.KitchenOnDay1);
        Assert.False(b.BusUnlocked);
        Assert.False(b.EarlyHorse);
        Assert.Empty(b.KeptBuildings);
        Assert.Empty(b.StartingAnimals);
    }
}
```

- [ ] **Step 3: Run the value-object test to confirm it passes**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~RunBaselineTests"
```

Expected: PASS.

- [ ] **Step 3b: Create the `PlayerSnapshot` value object**

Create `src/TheLongestYear.Core/PlayerSnapshot.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Snapshot of in-run peak achievements read from the live <c>Farmer</c> just before the
/// reset wipes them. The in-run cap side of cap-not-grant: an owned <c>keep_iron_hoe</c>
/// upgrade only restores Iron next run if <see cref="ToolTiers"/>["hoe"] reached 2 (Steel)
/// or higher this run. (Mine elevator's peak lives on <c>RunState.PeakMineFloor</c> and
/// stays there because it has to survive the reset itself.)
///
/// Pure data — no game refs. The mod-side <c>WorldResetService</c> populates this from
/// <c>Game1.player</c> before clearing the player.
/// </summary>
public sealed class PlayerSnapshot
{
    /// <summary>Tool kind slug → highest <c>Tool.UpgradeLevel</c> the player held this run.
    /// Same slug set as <c>RunBaseline.ToolTiers</c>. Missing key = 0.</summary>
    public IReadOnlyDictionary<string, int> ToolTiers { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Vanilla skill-index (0..4) → highest level the player reached this run.
    /// Missing key = 0.</summary>
    public IReadOnlyDictionary<int, int> SkillLevels { get; init; }
        = new Dictionary<int, int>();

    /// <summary>Convenience: zero-everything snapshot (used in tests and for first-ever
    /// reset where there's no meaningful "peak" yet).</summary>
    public static PlayerSnapshot Empty { get; } = new PlayerSnapshot();
}
```

- [ ] **Step 4: Write the failing builder tests**

Create `tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunBaselineBuilderTests
{
    private const int Farming = 0;
    private const int Fishing = 1;
    private const int Foraging = 2;
    private const int Mining = 3;
    private const int Combat = 4;

    // Snapshot helpers — most tests want a "player held everything at max" baseline so
    // they can test catalog-side filtering in isolation without re-stating peaks each time.
    private static PlayerSnapshot SnapshotAtPeak() => new PlayerSnapshot
    {
        ToolTiers = new Dictionary<string, int>
        {
            ["hoe"] = 4, ["pickaxe"] = 4, ["axe"] = 4, ["watering_can"] = 4, ["fishing_rod"] = 3
        },
        SkillLevels = new Dictionary<int, int>
        {
            [Farming] = 10, [Fishing] = 10, [Foraging] = 10, [Mining] = 10, [Combat] = 10
        }
    };

    [Fact]
    public void Empty_meta_state_produces_pure_vanilla_baseline()
    {
        var baseline = RunBaselineBuilder.Build(
            new MetaState(), new RunState(), PlayerSnapshot.Empty, defaultStartingMoney: 500);

        Assert.Equal(500, baseline.StartingGold);
        Assert.Equal(12, baseline.MaxItems);
        Assert.Empty(baseline.ToolTiers);
        Assert.Empty(baseline.SkillLevels);
        Assert.Equal(0, baseline.MineElevatorFloor);
    }

    [Fact]
    public void Backpack_1_grants_24_slot_inventory()
    {
        var meta = new MetaState { OwnedUpgrades = { "backpack_1" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(24, b.MaxItems);
    }

    [Fact]
    public void Backpack_2_grants_36_slot_inventory_taking_precedence()
    {
        var meta = new MetaState { OwnedUpgrades = { "backpack_1", "backpack_2" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(36, b.MaxItems);
    }

    [Fact]
    public void Starter_gold_1_adds_500g_to_default()
    {
        var meta = new MetaState { OwnedUpgrades = { "starter_gold_1" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(1000, b.StartingGold);
    }

    [Fact]
    public void Starter_gold_2_replaces_starter_gold_1_for_a_total_of_2000g()
    {
        var meta = new MetaState { OwnedUpgrades = { "starter_gold_1", "starter_gold_2" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(2000, b.StartingGold);
    }

    [Fact]
    public void Tool_keep_tier_appears_in_ToolTiers_when_peak_reached_it()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2", "keep_hoe_3", "keep_pickaxe_1" }
        };
        // Peak this run: Gold Hoe (UpgradeLevel 3) + Copper Pickaxe (1). No axe held.
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["hoe"] = 3, ["pickaxe"] = 1 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(3, b.ToolTiers["hoe"]);
        Assert.Equal(1, b.ToolTiers["pickaxe"]);
        Assert.False(b.ToolTiers.ContainsKey("axe"));
    }

    [Fact]
    public void Tool_keep_tier_is_capped_at_in_run_peak()
    {
        // Player owns keep_hoe_1 through keep_hoe_4 (Iridium) but only reached
        // Steel (UpgradeLevel 2) this run. Cap to Steel.
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2", "keep_hoe_3", "keep_hoe_4" }
        };
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["hoe"] = 2 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(2, b.ToolTiers["hoe"]);
    }

    [Fact]
    public void Tool_keep_tier_with_zero_peak_yields_no_entry()
    {
        // Owns keep_hoe_2 but somehow ended the run with no hoe upgrade (sold it?).
        // Snapshot peak = 0 → no entry written, vanilla rusty hoe baseline.
        var meta = new MetaState { OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.False(b.ToolTiers.ContainsKey("hoe"));
    }

    [Fact]
    public void Fishing_rod_keep_tier_1_writes_UpgradeLevel_2_when_peak_allows()
    {
        // keep_fishing_rod_1 = "Keep Fiberglass Rod" = UpgradeLevel 2 (bamboo at L1 is
        // vanilla Willy day-2 grant). Peak of 2 (Fiberglass) → baseline writes UpgradeLevel 2.
        var meta = new MetaState { OwnedUpgrades = { "keep_fishing_rod_1" } };
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["fishing_rod"] = 2 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(2, b.ToolTiers["fishing_rod"]);
    }

    [Fact]
    public void Fishing_rod_keep_tier_2_writes_UpgradeLevel_3_when_peak_allows()
    {
        var meta = new MetaState { OwnedUpgrades = { "keep_fishing_rod_1", "keep_fishing_rod_2" } };
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["fishing_rod"] = 3 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(3, b.ToolTiers["fishing_rod"]);
    }

    [Fact]
    public void Skill_keep_level_appears_in_SkillLevels_capped_at_in_run_peak()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_farming_level_1", "keep_farming_level_2", "keep_combat_level_5" }
        };
        // Reached Farming 2 (capped), Combat 3 (cap kicks in below the kept L5).
        var snapshot = new PlayerSnapshot
        {
            SkillLevels = new Dictionary<int, int> { [Farming] = 2, [Combat] = 3 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(2, b.SkillLevels[Farming]);
        Assert.Equal(3, b.SkillLevels[Combat]);
        Assert.DoesNotContain(Fishing, b.SkillLevels.Keys);
    }

    [Fact]
    public void Profession_picker_requeue_includes_L5_and_L10_only_when_capped_level_hits_them()
    {
        var meta = new MetaState
        {
            OwnedUpgrades =
            {
                "keep_farming_level_1", "keep_farming_level_2", "keep_farming_level_3",
                "keep_farming_level_4", "keep_farming_level_5",
                "keep_mining_level_1", "keep_mining_level_2", "keep_mining_level_3",
                "keep_mining_level_4", "keep_mining_level_5", "keep_mining_level_6",
                "keep_mining_level_7", "keep_mining_level_8", "keep_mining_level_9",
                "keep_mining_level_10",
                "keep_combat_level_1", "keep_combat_level_2"   // L2, not L5/10
            }
        };
        // Reached Farming 5, Mining 10, Combat 2.
        var snapshot = new PlayerSnapshot
        {
            SkillLevels = new Dictionary<int, int> { [Farming] = 5, [Mining] = 10, [Combat] = 2 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Contains(Farming, b.ProfessionPickerSkillsToRequeue);   // L5 kept
        Assert.Contains(Mining, b.ProfessionPickerSkillsToRequeue);    // L10 kept
        Assert.DoesNotContain(Combat, b.ProfessionPickerSkillsToRequeue);
    }

    [Fact]
    public void Profession_picker_skipped_when_peak_did_not_reach_the_threshold()
    {
        // Owns keep_farming_level_5 but only reached Farming 4 this run. Capped to 4,
        // no profession picker queued.
        var meta = new MetaState
        {
            OwnedUpgrades =
            {
                "keep_farming_level_1", "keep_farming_level_2", "keep_farming_level_3",
                "keep_farming_level_4", "keep_farming_level_5"
            }
        };
        var snapshot = new PlayerSnapshot
        {
            SkillLevels = new Dictionary<int, int> { [Farming] = 4 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(4, b.SkillLevels[Farming]);
        Assert.DoesNotContain(Farming, b.ProfessionPickerSkillsToRequeue);
    }

    [Fact]
    public void Mine_elevator_keep_is_capped_at_in_run_peak_floor()
    {
        var meta = new MetaState
        {
            OwnedUpgrades =
            {
                "keep_mine_elevator_10","keep_mine_elevator_20","keep_mine_elevator_30",
                "keep_mine_elevator_40","keep_mine_elevator_50","keep_mine_elevator_60",
                "keep_mine_elevator_70","keep_mine_elevator_80"   // owns up to F80
            }
        };
        var run = new RunState { PeakMineFloor = 55 };  // only reached F55 this run

        // Restored floor = floor down to the nearest 10 of min(80, 55) = 50.
        var b = RunBaselineBuilder.Build(meta, run, PlayerSnapshot.Empty, 500);
        Assert.Equal(50, b.MineElevatorFloor);
    }

    [Fact]
    public void Mine_elevator_floor_zero_when_no_keep_owned()
    {
        var b = RunBaselineBuilder.Build(
            new MetaState(),
            new RunState { PeakMineFloor = 90 },   // reached but never bought a keep
            PlayerSnapshot.Empty,
            500);
        Assert.Equal(0, b.MineElevatorFloor);
    }

    [Fact]
    public void Kitchen_bus_horse_flags_track_their_owned_upgrades()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_kitchen", "keep_bus_unlocked", "early_horse" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.True(b.KitchenOnDay1);
        Assert.True(b.BusUnlocked);
        Assert.True(b.EarlyHorse);
    }

    [Fact]
    public void KeptBuildings_uses_highest_owned_tier_per_housing_chain()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_coop", "keep_big_coop", "keep_barn" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Contains("Big Coop", b.KeptBuildings);
        Assert.DoesNotContain("Coop", b.KeptBuildings);          // superseded
        Assert.DoesNotContain("Deluxe Coop", b.KeptBuildings);   // not owned
        Assert.Contains("Barn", b.KeptBuildings);
    }

    [Fact]
    public void StartingAnimals_include_owned_start_animals_with_their_required_housing()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_coop", "start_chicken", "keep_barn", "start_cow" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Contains(b.StartingAnimals, a => a.VanillaType == "White Chicken" && a.HousingType == "Coop");
        Assert.Contains(b.StartingAnimals, a => a.VanillaType == "White Cow" && a.HousingType == "Barn");
    }
}
```

- [ ] **Step 5: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~RunBaselineBuilderTests"
```

Expected: FAIL (compiler errors: `RunBaselineBuilder` doesn't exist).

- [ ] **Step 6: Create the builder**

Create `src/TheLongestYear.Core/RunBaselineBuilder.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Pure builder that derives a <see cref="RunBaseline"/> from <see cref="MetaState"/> (banked
/// upgrades), <see cref="RunState"/> (per-run peak for mine floor), and <see cref="PlayerSnapshot"/>
/// (per-run peaks for tool tiers + skill levels). All three peak sources feed the cap-not-grant
/// rule: a keep upgrade only restores what the player actually achieved this run. No game refs.
/// </summary>
public static class RunBaselineBuilder
{
    // Skill indexes — match Farmer.farmingSkill/etc. constants in the decompile
    // (StardewValley\StardewValley\Farmer.cs:85-95).
    private const int Farming = 0;
    private const int Fishing = 1;
    private const int Foraging = 2;
    private const int Mining = 3;
    private const int Combat = 4;

    private static readonly (string Slug, int Skill)[] SkillSlugs =
    {
        ("farming",  Farming),
        ("fishing",  Fishing),
        ("foraging", Foraging),
        ("mining",   Mining),
        ("combat",   Combat),
    };

    private static readonly string[] ToolSlugs = { "hoe", "pickaxe", "axe", "watering_can" };

    // Fishing rod chain offset: keep_fishing_rod_1 = Fiberglass = UpgradeLevel 2,
    // keep_fishing_rod_2 = Iridium = UpgradeLevel 3. (UpgradeLevel 1 = bamboo, given
    // by Willy on day 2 of vanilla — no keep needed.)
    private const int FishingRodKeepToUpgradeLevelOffset = 1;
    private const int FishingRodMaxKeepTier = 2;

    // Coop and Barn chains. Each entry: (keep_id, building blueprint name). The highest
    // owned in each chain wins.
    private static readonly (string UpgradeId, string Blueprint)[] CoopChain =
    {
        ("keep_coop",        "Coop"),
        ("keep_big_coop",    "Big Coop"),
        ("keep_deluxe_coop", "Deluxe Coop"),
    };

    private static readonly (string UpgradeId, string Blueprint)[] BarnChain =
    {
        ("keep_barn",        "Barn"),
        ("keep_big_barn",    "Big Barn"),
        ("keep_deluxe_barn", "Deluxe Barn"),
    };

    // start_<id> → (vanilla FarmAnimal type, required housing blueprint).
    // The vanilla types come from Data/FarmAnimals — verify each before shipping.
    private static readonly Dictionary<string, (string VanillaType, string HousingType)> StartingAnimalMap =
        new()
        {
            ["start_chicken"]       = ("White Chicken", "Coop"),
            ["start_void_chicken"]  = ("Void Chicken",  "Coop"),
            ["start_duck"]          = ("Duck",          "Big Coop"),
            ["start_dinosaur"]      = ("Dinosaur",      "Big Coop"),
            ["start_rabbit"]        = ("Rabbit",        "Deluxe Coop"),
            ["start_ostrich"]       = ("Ostrich",       "Deluxe Coop"),
            ["start_cow"]           = ("White Cow",     "Barn"),
            ["start_goat"]          = ("Goat",          "Big Barn"),
            ["start_sheep"]         = ("Sheep",         "Deluxe Barn"),
            ["start_pig"]           = ("Pig",           "Deluxe Barn"),
        };

    public static RunBaseline Build(MetaState meta, RunState run, PlayerSnapshot peaks, int defaultStartingMoney)
    {
        int gold = defaultStartingMoney
            + (meta.HasUpgrade("starter_gold_2") ? 1500
               : meta.HasUpgrade("starter_gold_1") ? 500
               : 0);

        int maxItems = meta.HasUpgrade("backpack_2") ? 36
                      : meta.HasUpgrade("backpack_1") ? 24
                      : 12;

        // Tool tiers (basic 4) — owned-tier capped at in-run peak. Values are written
        // as the literal UpgradeLevel the apply code should set on the Tool: 1=Copper,
        // 2=Steel, 3=Gold, 4=Iridium. Zero means "no keep written" (vanilla rusty).
        var toolTiers = new Dictionary<string, int>();
        foreach (string slug in ToolSlugs)
        {
            int owned = meta.HighestKeptTier($"keep_{slug}_", maxTier: 4);
            if (owned <= 0) continue;
            int peak = peaks.ToolTiers.TryGetValue(slug, out int p) ? p : 0;
            int capped = System.Math.Min(owned, peak);
            if (capped > 0)
                toolTiers[slug] = capped;
        }

        // Fishing rod — translate keep-tier to UpgradeLevel (offset of 1) and cap at
        // in-run peak's UpgradeLevel.
        int rodKeep = meta.HighestKeptTier("keep_fishing_rod_", maxTier: FishingRodMaxKeepTier);
        if (rodKeep > 0)
        {
            int rodKeepAsUpgradeLevel = rodKeep + FishingRodKeepToUpgradeLevelOffset;
            int rodPeak = peaks.ToolTiers.TryGetValue("fishing_rod", out int rp) ? rp : 0;
            int rodCapped = System.Math.Min(rodKeepAsUpgradeLevel, rodPeak);
            if (rodCapped > 0)
                toolTiers["fishing_rod"] = rodCapped;
        }

        // Skill levels + profession re-pick queue — capped at in-run peak per skill.
        var skillLevels = new Dictionary<int, int>();
        var requeue = new List<int>();
        foreach (var (slug, skillIndex) in SkillSlugs)
        {
            int owned = meta.HighestKeptTier($"keep_{slug}_level_", maxTier: 10);
            if (owned <= 0) continue;
            int peak = peaks.SkillLevels.TryGetValue(skillIndex, out int sp) ? sp : 0;
            int capped = System.Math.Min(owned, peak);
            if (capped <= 0) continue;

            skillLevels[skillIndex] = capped;
            // Profession picker fires for the L5 and L10 thresholds that the CAPPED
            // level actually crosses. Owning keep_farming_level_5 but capped to L4
            // means no profession picker for Farming this reset.
            if (capped >= 5)
                requeue.Add(skillIndex);
        }

        // Mine elevator floor (cap-not-grant against in-run peak from RunState).
        int ownedFloor = HighestOwnedMineFloor(meta);
        int cappedFloor = System.Math.Min(ownedFloor, FloorDown10(run.PeakMineFloor));

        // Kept buildings — highest tier per chain wins
        var keptBuildings = new List<string>();
        AddTopOfChain(meta, CoopChain, keptBuildings);
        AddTopOfChain(meta, BarnChain, keptBuildings);

        // Starting animals — every owned start_<species> goes in (the prerequisite chain
        // already enforces the matching housing was bought, so the housing is in keptBuildings).
        var startingAnimals = new List<StartingAnimal>();
        foreach (var (upgradeId, mapping) in StartingAnimalMap)
        {
            if (meta.HasUpgrade(upgradeId))
                startingAnimals.Add(new StartingAnimal(mapping.VanillaType, mapping.HousingType));
        }

        return new RunBaseline
        {
            StartingGold = gold,
            MaxItems = maxItems,
            ToolTiers = toolTiers,
            SkillLevels = skillLevels,
            ProfessionPickerSkillsToRequeue = requeue,
            MineElevatorFloor = cappedFloor,
            KitchenOnDay1 = meta.HasUpgrade("keep_kitchen"),
            BusUnlocked = meta.HasUpgrade(VaultRules.KeepBusUnlockedId),
            EarlyHorse = meta.HasUpgrade("early_horse"),
            KeptBuildings = keptBuildings,
            StartingAnimals = startingAnimals,
        };
    }

    // Floors come in steps of 10 (keep_mine_elevator_10/20/30/…). Round PeakMineFloor
    // DOWN to the nearest 10 so a player who reached floor 47 with keep_50 owned ends up
    // at floor 40, not 47 (the elevator only stops at multiples of 5 in vanilla anyway —
    // 5/10/15/… — but our keep tiers are documented as 10-step).
    private static int FloorDown10(int floor) => (floor / 10) * 10;

    private static int HighestOwnedMineFloor(MetaState meta)
    {
        int best = 0;
        for (int floor = 10; floor <= 120; floor += 10)
            if (meta.HasUpgrade($"keep_mine_elevator_{floor}"))
                best = floor;
        return best;
    }

    private static void AddTopOfChain(
        MetaState meta,
        (string UpgradeId, string Blueprint)[] chain,
        List<string> output)
    {
        // Iterate from highest to lowest, return first hit.
        for (int i = chain.Length - 1; i >= 0; i--)
            if (meta.HasUpgrade(chain[i].UpgradeId))
            {
                output.Add(chain[i].Blueprint);
                return;
            }
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~RunBaseline"
```

Expected: PASS (all RunBaselineTests + all RunBaselineBuilderTests, ~17 total).

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear.Core/RunBaseline.cs src/TheLongestYear.Core/PlayerSnapshot.cs src/TheLongestYear.Core/RunBaselineBuilder.cs tests/TheLongestYear.Tests/RunBaselineTests.cs tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs
git commit -m "$(cat <<'EOF'
feat(core): RunBaseline + PlayerSnapshot + RunBaselineBuilder — pure reset-state derivation

Phase A persistence design §A + §B. RunBaseline is the contract between Core
(decides what the player should start with) and the mod-side reset code (writes
it to Farmer + world). PlayerSnapshot carries the in-run peak tool tiers + skill
levels captured before the wipe. RunState.PeakMineFloor carries the mine peak.

Builder enforces cap-not-grant on all three: a player who bought keep_iridium_hoe
but only reached Steel this run gets Steel back, not Iridium. The fishing rod
tier-to-UpgradeLevel offset (keep_fishing_rod_1 = Fiberglass = UpgradeLevel 2)
is handled in the builder so the apply side stays uniform across tool kinds.

Profession picker requeue list only populates for L5/L10 skills where the CAPPED
level (not the owned-keep level) actually crosses the threshold.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Rewrite `FarmerReset` to apply `RunBaseline`

**Files:**
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs`

The existing static `ToBaseline(Farmer p, int startingMoney)` becomes an instance class that takes a `RunBaseline`. All the field-clear logic stays; the apply logic at the end gets the new baseline writes. **No tests** here — `FarmerReset` touches `Farmer`/`Game1` which we don't unit-test (the builder is the pure seam, already TDD'd). Manual verification happens in Task 16.

- [ ] **Step 1: Rewrite the class**

Replace the entire contents of `src/TheLongestYear/Loop/FarmerReset.cs` with:

```csharp
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Applies a <see cref="RunBaseline"/> to the persistent <see cref="Farmer"/> at the top
    /// of a new run. Game1.loadForNewGame rebuilds the world but leaves the player's
    /// money/skills/inventory/relationships intact, so we clear them here, then re-apply the
    /// baseline (backpack, tool tiers, skill levels with XP flooring, starting gold).
    /// Plan 07 will carve the Junimo Stash out of the inventory wipe.
    /// </summary>
    internal sealed class FarmerReset
    {
        private readonly IMonitor _monitor;

        public FarmerReset(IMonitor monitor) => _monitor = monitor;

        public void Apply(Farmer p, RunBaseline baseline)
        {
            p.Money = baseline.StartingGold;

            // Inventory — wipe CONTENTS but set the slot count from the baseline (Stash
            // preservation is Plan 07). p.Items.Clear() removes the slot list itself, which
            // leaves MaxItems lookups returning 0 → addItemToInventory always fails (round-3
            // playtest bug); reset MaxItems then re-pad nulls.
            p.MaxItems = baseline.MaxItems;
            p.Items.Clear();
            for (int i = 0; i < p.MaxItems; i++)
                p.Items.Add(null);

            // Skills — clear everything first.
            for (int i = 0; i < p.experiencePoints.Count; i++)
                p.experiencePoints[i] = 0;
            p.farmingLevel.Value = 0;
            p.miningLevel.Value = 0;
            p.fishingLevel.Value = 0;
            p.foragingLevel.Value = 0;
            p.combatLevel.Value = 0;
            p.luckLevel.Value = 0;
            p.professions.Clear();

            // Re-grant kept skill levels + floor XP to the level's threshold.
            // Farmer.getBaseExperienceForLevel is the vanilla XP-for-level table
            // (decompile: StardewValley\StardewValley\Farmer.cs:3046, used at line 7233).
            foreach (var kvp in baseline.SkillLevels)
            {
                int skillIndex = kvp.Key;
                int level = kvp.Value;
                p.experiencePoints[skillIndex] = Farmer.getBaseExperienceForLevel(level);
                SetSkillLevel(p, skillIndex, level);
            }

            // Re-grant kept tool tiers. Player's toolList still has the vanilla baseline
            // tools (rusty); just bump their UpgradeLevel. Tool.UpgradeLevel is settable
            // directly (decompile: StardewValley\StardewValley\Tool.cs:167).
            ApplyToolTiers(p, baseline.ToolTiers);

            // Relationships, mail, events, quests.
            p.friendshipData.Clear();
            p.mailReceived.Clear();
            p.eventsSeen.Clear();
            p.questLog.Clear();

            // Suppress the vanilla intro cutscene from replaying every loop (matches TitleMenu's new-game path).
            p.eventsSeen.Add("60367");

            // Vitals to full.
            p.stamina = p.maxStamina.Value;
            p.health = p.maxHealth;

            // House upgrade — set Kitchen on day 1 if kept_kitchen owned. The actual
            // FarmHouse layout switch happens in WorldResetService (it has to resetForPlayerEntry
            // after setting the level so the kitchen tiles appear).
            if (baseline.KitchenOnDay1)
                p.HouseUpgradeLevel = 1;

            _monitor.Log(
                $"FarmerReset: gold={baseline.StartingGold}, slots={baseline.MaxItems}, " +
                $"tools=[{string.Join(",", baseline.ToolTiers)}], " +
                $"skills=[{string.Join(",", baseline.SkillLevels)}], " +
                $"kitchen={baseline.KitchenOnDay1}.",
                LogLevel.Trace);
        }

        private static void SetSkillLevel(Farmer p, int skillIndex, int level)
        {
            switch (skillIndex)
            {
                case 0: p.farmingLevel.Value = level; break;
                case 1: p.fishingLevel.Value = level; break;
                case 2: p.foragingLevel.Value = level; break;
                case 3: p.miningLevel.Value = level; break;
                case 4: p.combatLevel.Value = level; break;
                // Luck (5) intentionally excluded — no level keeps for it per the design.
            }
        }

        private static void ApplyToolTiers(Farmer p, IReadOnlyDictionary<string, int> tiers)
        {
            // Find each basic tool in the player's toolList by type and bump UpgradeLevel.
            // loadForNewGame gives the player a Hoe, Pickaxe, Axe, WateringCan, and
            // Scythe (MeleeWeapon, no tier). NO FishingRod — vanilla Willy mails the
            // bamboo rod on day 2 — so we handle that one separately below.
            bool hasRodInInventory = false;
            foreach (var item in p.Items)
            {
                if (item is Hoe         h  && tiers.TryGetValue("hoe",          out int ht)) h.UpgradeLevel  = ht;
                if (item is Pickaxe     pk && tiers.TryGetValue("pickaxe",      out int pkt)) pk.UpgradeLevel = pkt;
                if (item is Axe         a  && tiers.TryGetValue("axe",          out int at)) a.UpgradeLevel  = at;
                if (item is WateringCan w  && tiers.TryGetValue("watering_can", out int wt)) w.UpgradeLevel  = wt;
                if (item is FishingRod  fr)
                {
                    hasRodInInventory = true;
                    if (tiers.TryGetValue("fishing_rod", out int frt))
                        fr.UpgradeLevel = frt;
                }
            }

            // Fishing rod: if we need to grant one and the player has no rod yet (the
            // common day-1 case), create a fresh rod at the requested UpgradeLevel and
            // slot it into the first empty inventory slot. Also pre-mail the "willyBackRoom"
            // flag so Willy's day-2 bamboo-rod event doesn't fire on top of this.
            if (!hasRodInInventory && tiers.TryGetValue("fishing_rod", out int rodLevel))
            {
                var rod = new FishingRod { UpgradeLevel = rodLevel };
                // Find the first null slot — p.Items has nulls between live items because
                // FarmerReset re-padded them.
                for (int i = 0; i < p.Items.Count; i++)
                {
                    if (p.Items[i] == null)
                    {
                        p.Items[i] = rod;
                        break;
                    }
                }
                p.mailReceived.Add("willyBackRoom");
            }
        }
    }
}
```

- [ ] **Step 2: Verify the project still compiles (it won't link yet — callers need updating)**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

Expected: FAIL with one specific error in `WorldResetService.cs` at the call site:
`FarmerReset.ToBaseline(Game1.player, startingMoney);`
referencing the old static method. The fix lands in Task 12.

- [ ] **Step 3: Don't commit yet** — the project doesn't build. Continue to Task 11.

---

## Task 11: `ProfessionPickerScheduler` — re-trigger `LevelUpMenu` for kept L5/L10 skills

**Files:**
- Create: `src/TheLongestYear/Loop/ProfessionPickerScheduler.cs`

The vanilla profession picker is `LevelUpMenu(int skill, int level)` (decompile: `StardewValley\StardewValley.Menus\LevelUpMenu.cs:95`). We can't open it during the reset itself (the player isn't in a position to interact yet — `loadForNewGame` is mid-flight). Schedule the menus to fire on the next `DayStarted` event on day 1 (i.e. immediately after the reset finishes and the wake-up cutscene resolves).

- [ ] **Step 1: Create the scheduler**

Create `src/TheLongestYear/Loop/ProfessionPickerScheduler.cs`:

```csharp
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Queues vanilla <see cref="LevelUpMenu"/> instances for kept skills that landed at
    /// Level 5 or 10 — re-runs the profession picker so the player can change their picks
    /// each loop. The reset code can't open menus directly (loadForNewGame is mid-flight),
    /// so we enqueue here and the next DayStarted handler in <see cref="RunController"/>
    /// drains the queue by stacking the menus via <c>Game1.endOfNightMenus.Push</c> — the
    /// same path vanilla uses when natural level-ups happen at sleep.
    ///
    /// One menu is required per profession threshold: Level 5 gives the first profession
    /// choice, Level 10 gives the specialisation. A skill kept at L10 needs BOTH menus
    /// queued (vanilla pushes the L5 menu first then the L10 menu).
    /// </summary>
    internal sealed class ProfessionPickerScheduler
    {
        private readonly IMonitor _monitor;
        private readonly Queue<(int Skill, int Level)> _pending = new();

        public ProfessionPickerScheduler(IMonitor monitor) => _monitor = monitor;

        public int PendingCount => _pending.Count;

        /// <summary>Queue picker menus for the given skill index based on the kept level.
        /// L5 keep → one menu. L10 keep → two menus (L5 then L10).</summary>
        public void Enqueue(int skillIndex, int level)
        {
            if (level >= 5)
                _pending.Enqueue((skillIndex, 5));
            if (level >= 10)
                _pending.Enqueue((skillIndex, 10));
        }

        /// <summary>Push all pending menus onto Game1.endOfNightMenus in queue order. They
        /// pop in LIFO, so the LAST pushed shows first — push in REVERSE so the player sees
        /// L5 before L10 within a skill, and Farming before Mining etc. (alphabetic enqueue).
        /// Safe to call when queue is empty (no-op).</summary>
        public void DrainOnDayStart()
        {
            if (_pending.Count == 0)
                return;

            // Collect to a list so we can iterate in reverse.
            var pickers = new List<(int Skill, int Level)>(_pending);
            _pending.Clear();

            for (int i = pickers.Count - 1; i >= 0; i--)
                Game1.endOfNightMenus.Push(new LevelUpMenu(pickers[i].Skill, pickers[i].Level));

            _monitor.Log(
                $"ProfessionPickerScheduler: queued {pickers.Count} profession picker menu(s) for the player.",
                LogLevel.Info);

            // Trigger the menu stack to start drawing. endOfNightMenus is normally drained by
            // the post-sleep sequence; since we're queuing on DayStarted we kick it manually
            // by activating the top one. Game1.showEndOfNightStuff() is the public entrypoint.
            Game1.showEndOfNightStuff();
        }
    }
}
```

- [ ] **Step 2: Verify build still fails at the same WorldResetService call site**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

Expected: still FAIL with the FarmerReset.ToBaseline error — but no NEW errors from this file.

- [ ] **Step 3: Don't commit yet** — combine commits across Tasks 10–12 once `WorldResetService` is updated.

---

## Task 12: `WorldResetService` builds the baseline + applies world-side effects

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs`

The reset service goes from "wipe + place player" to "wipe + apply baseline + place player." Specifically it:
- Builds the `RunBaseline` from `MetaState` + `RunState` peaks.
- Hands it to the new instance `FarmerReset`.
- Increments `MetaState.CompletedResets` (single producer for the `season:N` namespace).
- Sets `Game1.netWorldState.Value.LowestMineLevelForOrder` from `baseline.MineElevatorFloor`.
- Pre-marks `RunState.VaultBundlesPaid` with all four bundles when `BusUnlocked` is true.
- Pre-builds kept buildings on the Farm.
- Spawns horse + stable if `EarlyHorse`.
- Places starting animals into matching housing.
- Runs the profession picker queue setup.
- Resets the FarmHouse for player entry (covers kitchen upgrade).

- [ ] **Step 1: Add the imports + fields you'll need**

Edit `src/TheLongestYear/Loop/WorldResetService.cs`. Update the top:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using TheLongestYear.Core;
```

Add fields next to the existing `_meta`/`_ccUnlock` ones:

```csharp
        private readonly RunState _run;
        private readonly GameplayConfig _config;
        private readonly FarmerReset _farmerReset;
        private readonly ProfessionPickerScheduler _professionPicker;

        public ProfessionPickerScheduler ProfessionPicker => _professionPicker;
```

Update the constructor signature + body:

```csharp
        public WorldResetService(
            IMonitor monitor,
            TheLongestYear.Core.MetaState meta,
            TheLongestYear.Core.RunState run,
            TheLongestYear.Core.GameplayConfig config,
            CommunityCenterUnlock ccUnlock,
            string modDirectory,
            FarmerReset farmerReset,
            ProfessionPickerScheduler professionPicker)
        {
            _monitor = monitor;
            _meta = meta;
            _run = run;
            _config = config;
            _ccUnlock = ccUnlock;
            _modDirectory = modDirectory;
            _farmerReset = farmerReset;
            _professionPicker = professionPicker;
        }
```

- [ ] **Step 2: Replace `PerformReset` to use the baseline**

Change the `PerformReset(int startingMoney)` signature to `PerformReset()` and rewrite the section starting at "// 3. Farmer baseline" (the line currently `FarmerReset.ToBaseline(Game1.player, startingMoney);`) and replace through end-of-method with:

```csharp
            // 3. Capture the in-run peaks from the live player BEFORE the wipe — the cap
            //    side of cap-not-grant. The Farmer-side wipe happens inside
            //    _farmerReset.Apply, so peak-reading has to land here.
            PlayerSnapshot peaks = CapturePeaks(Game1.player);

            // 4. Build the reset baseline + apply Farmer-side state (gold, items, tool
            //    tiers, skill levels, kitchen flag).
            RunBaseline baseline = RunBaselineBuilder.Build(_meta, _run, peaks, _config.StartingMoney);
            _farmerReset.Apply(Game1.player, baseline);

            // 5. Profession picker re-trigger queue. Enqueued here; the actual menus
            //    surface on the next DayStarted (RunController drains after reset).
            foreach (int skill in baseline.ProfessionPickerSkillsToRequeue)
                _professionPicker.Enqueue(skill, baseline.SkillLevels[skill]);

            // 6. Mine progress. Restore elevator floor from baseline (cap-not-grant
            //    against in-run peak). MineShaft.lowestLevelReached property setter +
            //    LowestMineLevelForOrder field (NetWorldState.cs:362) drive the elevator
            //    panel options.
            Game1.netWorldState.Value.LowestMineLevelForOrder = -1;
            MineShaft.clearActiveMines();
            if (baseline.MineElevatorFloor > 0)
            {
                Game1.netWorldState.Value.LowestMineLevelForOrder = baseline.MineElevatorFloor;
                Game1.player.deepestMineLevel = System.Math.Max(
                    Game1.player.deepestMineLevel, baseline.MineElevatorFloor);
            }

            // 7. Vault gate pre-pay if bus is kept unlocked.
            if (baseline.BusUnlocked)
            {
                _run.VaultBundlesPaid.Clear();
                _run.VaultBundlesPaid.Add(VaultRules.Vault2500);
                _run.VaultBundlesPaid.Add(VaultRules.Vault5000);
                _run.VaultBundlesPaid.Add(VaultRules.Vault10000);
                _run.VaultBundlesPaid.Add(VaultRules.Vault25000);
            }

            // 8. Pre-build kept buildings on the Farm. Coords are deterministic — we always
            //    use the same tiles so subsequent runs land buildings in the same spots.
            ApplyKeptBuildings(baseline.KeptBuildings);

            // 9. Early horse + stable.
            if (baseline.EarlyHorse)
                ApplyEarlyHorse();

            // 10. Place starting animals into matching housing.
            ApplyStartingAnimals(baseline.StartingAnimals);

            // 11. Bump CompletedResets — the single producer for the season:N meta-requirement.
            _meta.CompletedResets += 1;

            // 12. Place the player home, awake, in the rebuilt FarmHouse. resetForPlayerEntry
            //     also rebuilds the FarmHouse layout to match HouseUpgradeLevel — picking up
            //     the kitchen if the baseline set it.
            GameLocation home = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.currentLocation = home;
            Game1.currentLocation = home;
            Game1.player.Position = new Vector2(9f, 9f) * 64f;
            home.resetForPlayerEntry();

            // Re-apply the CC unlock so the loop preserves day-1 CC access (loadForNewGame + FarmerReset wiped it).
            _ccUnlock.Apply();

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}. " +
                $"Reset #{_meta.CompletedResets}.",
                LogLevel.Info);
        }
```

- [ ] **Step 3: Add the building/horse/animal helper methods**

Append these private methods inside the `WorldResetService` class:

```csharp
        // Deterministic tile coords for each kept-building blueprint. Picked to fit
        // the default vanilla farm layout without overlapping the bus stop or the
        // starting clearing. If two chains both place (Coop + Barn) they don't
        // overlap each other.
        private static readonly Dictionary<string, Vector2> BuildingTiles = new()
        {
            ["Coop"]         = new Vector2(54f, 9f),
            ["Big Coop"]     = new Vector2(54f, 9f),
            ["Deluxe Coop"]  = new Vector2(54f, 9f),
            ["Barn"]         = new Vector2(62f, 12f),
            ["Big Barn"]     = new Vector2(62f, 12f),
            ["Deluxe Barn"]  = new Vector2(62f, 12f),
        };

        private static readonly Vector2 StableTile = new(48f, 7f);

        // Read in-run peaks from the live player so the baseline builder can apply
        // cap-not-grant. Walks p.Items looking for each tool kind and reads its
        // UpgradeLevel; reads skill level fields directly.
        private static PlayerSnapshot CapturePeaks(Farmer p)
        {
            var toolTiers = new Dictionary<string, int>();
            foreach (var item in p.Items)
            {
                if (item is StardewValley.Tools.Hoe         h)  toolTiers["hoe"]          = System.Math.Max(toolTiers.TryGetValue("hoe", out var v0) ? v0 : 0, h.UpgradeLevel);
                if (item is StardewValley.Tools.Pickaxe     pk) toolTiers["pickaxe"]      = System.Math.Max(toolTiers.TryGetValue("pickaxe", out var v1) ? v1 : 0, pk.UpgradeLevel);
                if (item is StardewValley.Tools.Axe         a)  toolTiers["axe"]          = System.Math.Max(toolTiers.TryGetValue("axe", out var v2) ? v2 : 0, a.UpgradeLevel);
                if (item is StardewValley.Tools.WateringCan w)  toolTiers["watering_can"] = System.Math.Max(toolTiers.TryGetValue("watering_can", out var v3) ? v3 : 0, w.UpgradeLevel);
                if (item is StardewValley.Tools.FishingRod  fr) toolTiers["fishing_rod"]  = System.Math.Max(toolTiers.TryGetValue("fishing_rod", out var v4) ? v4 : 0, fr.UpgradeLevel);
            }

            var skillLevels = new Dictionary<int, int>
            {
                [0] = p.farmingLevel.Value,
                [1] = p.fishingLevel.Value,
                [2] = p.foragingLevel.Value,
                [3] = p.miningLevel.Value,
                [4] = p.combatLevel.Value,
            };

            return new PlayerSnapshot { ToolTiers = toolTiers, SkillLevels = skillLevels };
        }

        private void ApplyKeptBuildings(IReadOnlyList<string> buildings)
        {
            Farm farm = Game1.getFarm();
            foreach (string blueprint in buildings)
            {
                if (!BuildingTiles.TryGetValue(blueprint, out Vector2 tile))
                {
                    _monitor.Log($"Reset: no tile mapped for kept building '{blueprint}', skipping.",
                        LogLevel.Warn);
                    continue;
                }

                // Already there? (e.g. previous reset placed it and loadForNewGame somehow
                // preserved it.) Skip — never duplicate.
                if (farm.buildings.Any(b => b.buildingType.Value == blueprint))
                    continue;

                var b = new Building(blueprint, tile);
                b.daysOfConstructionLeft.Value = 0;   // skip the construction animation
                b.load();                              // initialises interior
                farm.buildings.Add(b);
            }
        }

        private void ApplyEarlyHorse()
        {
            Farm farm = Game1.getFarm();
            // Skip if stable already there (idempotent across re-resets).
            if (farm.buildings.OfType<Stable>().Any())
                return;

            var stable = new Stable(StableTile);
            stable.daysOfConstructionLeft.Value = 0;
            stable.load();
            farm.buildings.Add(stable);
            stable.grabHorse();   // spawns the Horse NPC matched to the stable's HorseId
        }

        private void ApplyStartingAnimals(IReadOnlyList<StartingAnimal> animals)
        {
            if (animals.Count == 0) return;
            Farm farm = Game1.getFarm();

            foreach (var animal in animals)
            {
                Building housing = farm.buildings.FirstOrDefault(
                    b => b.buildingType.Value == animal.HousingType
                      || ChainTier(b.buildingType.Value) >= ChainTier(animal.HousingType));
                if (housing == null)
                {
                    _monitor.Log(
                        $"Reset: no '{animal.HousingType}'-or-better building found for " +
                        $"starting animal '{animal.VanillaType}'; skipping.",
                        LogLevel.Warn);
                    continue;
                }

                long animalId = Utility.RandomLong();
                var fa = new FarmAnimal(animal.VanillaType, animalId, Game1.player.UniqueMultiplayerID);

                // Add into the housing's animal collection. AnimalHouse exposes .animals.
                if (housing.indoors.Value is AnimalHouse house)
                {
                    house.animals.Add(animalId, fa);
                    fa.home = housing;
                    fa.homeLocation.Value = new Vector2((int)housing.tileX.Value, (int)housing.tileY.Value);
                    fa.setRandomPosition(house);
                }

                // Track that we've ever owned this species (drives start_<animal> meta-requirement
                // for future shrine offers).
                if (!_meta.AnimalSpeciesEverOwned.Contains(animal.VanillaType, StringComparer.OrdinalIgnoreCase))
                    _meta.AnimalSpeciesEverOwned.Add(animal.VanillaType);
            }
        }

        // Rank Coop=1, Big Coop=2, Deluxe Coop=3 (and same for Barn). "Or better" check
        // for animal placement — a Big Coop satisfies "needs Coop", a Deluxe Coop
        // satisfies "needs Big Coop", etc.
        private static int ChainTier(string blueprint) => blueprint switch
        {
            "Coop"         => 1,
            "Big Coop"     => 2,
            "Deluxe Coop"  => 3,
            "Barn"         => 1,
            "Big Barn"     => 2,
            "Deluxe Barn"  => 3,
            _ => 0
        };
```

- [ ] **Step 4: Update `ModEntry.OnSaveLoaded` to inject the new constructor params**

Edit `src/TheLongestYear/ModEntry.cs`. Find the line:

```csharp
            _reset = new WorldResetService(this.Monitor, _meta.State, _ccUnlock, this.Helper.DirectoryPath);
```

…and replace it with:

```csharp
            var farmerReset = new FarmerReset(this.Monitor);
            var professionPicker = new ProfessionPickerScheduler(this.Monitor);
            _reset = new WorldResetService(
                this.Monitor, _meta.State, _meta.Run, _config, _ccUnlock,
                this.Helper.DirectoryPath, farmerReset, professionPicker);
```

Also find every call site of `_reset.PerformReset(_config.StartingMoney)` and change to `_reset.PerformReset()`. There are two: one in `RunController.OnDayStarted` (in `src/TheLongestYear/Loop/RunController.cs`) and one in `ModEntry.FullResetAndPresentOffer` (in `src/TheLongestYear/ModEntry.cs`).

For `RunController.OnDayStarted`, the call is:

```csharp
                _reset.PerformReset(_config.StartingMoney);
```

Change to:

```csharp
                _reset.PerformReset();
                _reset.ProfessionPicker.DrainOnDayStart();
```

For `ModEntry.FullResetAndPresentOffer`:

```csharp
            _reset.PerformReset(_config.StartingMoney);
```

Change to:

```csharp
            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
```

- [ ] **Step 5: Verify it builds**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

Expected: build SUCCESS, no errors. Warnings about Harmony or NetField are fine if already present in baseline.

- [ ] **Step 6: Verify tests still pass (the Core ones — no game required)**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS (all existing tests + the new ones from Tasks 1–9 = 223 + ~30 new).

- [ ] **Step 7: Commit Tasks 10–12 together**

```bash
git add src/TheLongestYear/Loop/FarmerReset.cs src/TheLongestYear/Loop/ProfessionPickerScheduler.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat(mod): apply RunBaseline on reset — tools, skills, buildings, animals, mine, bus

FarmerReset becomes an instance class taking a RunBaseline (gold, slots, tool
UpgradeLevel writes, skill level + XP floor via getBaseExperienceForLevel).
WorldResetService builds the baseline from MetaState + RunState peaks and applies
the world-side effects: kitchen upgrade, mine elevator floor cap, vault bundles
pre-paid, pre-built coops/barns at deterministic tiles, horse + stable, starting
animals placed in matching housing (with AnimalSpeciesEverOwned tracking).

ProfessionPickerScheduler enqueues vanilla LevelUpMenu(skill, level) for each
kept L5/L10 skill and drains the queue on the post-reset DayStarted via
Game1.endOfNightMenus + Game1.showEndOfNightStuff() — same path vanilla uses
for natural level-ups at sleep.

CompletedResets increments inside PerformReset — the single producer for the
new season:N meta-requirement namespace (Task 3).

Plan 06A Tasks 10–12 (FarmerReset + ProfessionPickerScheduler + WorldResetService).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 13: `PeakMineFloorTracker` — observe `Warped` into a `MineShaft`

**Files:**
- Create: `src/TheLongestYear/Loop/PeakMineFloorTracker.cs`
- Modify: `src/TheLongestYear/ModEntry.cs`

SMAPI's `Player.Warped` event fires whenever the player changes location. When the new location is a `MineShaft`, read its `mineLevel` and call `RunState.RecordMineFloor`. The recorder already takes the max so re-warping into a shallower floor is a no-op.

- [ ] **Step 1: Create the tracker**

Create `src/TheLongestYear/Loop/PeakMineFloorTracker.cs`:

```csharp
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Updates <see cref="RunState.PeakMineFloor"/> when the player enters a deeper
    /// MineShaft. Feeds the cap-not-grant calculation for keep_mine_elevator_N on the
    /// next reset (Plan 06A persistence design §B).
    /// </summary>
    internal sealed class PeakMineFloorTracker
    {
        private readonly IMonitor _monitor;
        private readonly RunState _run;

        public PeakMineFloorTracker(IMonitor monitor, RunState run)
        {
            _monitor = monitor;
            _run = run;
        }

        public void OnWarped(object sender, WarpedEventArgs e)
        {
            if (e.NewLocation is not MineShaft mine)
                return;
            int before = _run.PeakMineFloor;
            _run.RecordMineFloor(mine.mineLevel);
            if (_run.PeakMineFloor != before)
                _monitor.Log(
                    $"PeakMineFloor advanced to {_run.PeakMineFloor} (entered MineShaft level {mine.mineLevel}).",
                    LogLevel.Trace);
        }
    }
}
```

- [ ] **Step 2: Wire it from `ModEntry.OnSaveLoaded`**

Edit `src/TheLongestYear/ModEntry.cs`. Add a field near the others:

```csharp
        private PeakMineFloorTracker _peakMineFloorTracker;
```

In `OnSaveLoaded`, after the `_runController = new RunController(...)` line, add:

```csharp
            _peakMineFloorTracker = new PeakMineFloorTracker(this.Monitor, _meta.Run);
            this.Helper.Events.Player.Warped += _peakMineFloorTracker.OnWarped;
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

Expected: build SUCCESS.

- [ ] **Step 4: Verify tests still pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Loop/PeakMineFloorTracker.cs src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat(mod): track RunState.PeakMineFloor via SMAPI Player.Warped

PeakMineFloorTracker subscribes to Player.Warped and records mine.mineLevel into
RunState.PeakMineFloor whenever the player enters a deeper MineShaft. Feeds the
cap-not-grant calculation for keep_mine_elevator_N on the next reset.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 14: `JunimoShrineMenu` — hide chain-locked + meta-locked rows

**Files:**
- Modify: `src/TheLongestYear/UI/JunimoShrineMenu.cs`

Spec §F: change the shop to **hide** entries whose `PrerequisiteId` isn't owned, and entries whose `MetaRequirement` isn't satisfied. The player sees only the next tier they can buy. Owned entries also disappear (you can't re-buy them).

The current implementation calls `UpgradeCatalog.ByCategory(_activeCategory)` directly. Add a filtered accessor at the top of the menu class that wraps the catalog query with the visibility rules.

- [ ] **Step 1: Add a filter method**

In `src/TheLongestYear/UI/JunimoShrineMenu.cs`, add this private helper near the other private methods (e.g. before `VisibleRows`):

```csharp
        /// <summary>
        /// All catalog entries in the active category that should be VISIBLE in the shrine
        /// shop: not owned, prereq satisfied (or no prereq), meta-requirement satisfied
        /// (or no meta-requirement). Chain-locked rows disappear entirely — the player only
        /// sees the next purchasable tier in each chain.
        /// </summary>
        private IReadOnlyList<UpgradeDefinition> VisibleCatalogForActiveCategory()
        {
            MetaState state = _store.State;
            var visible = new List<UpgradeDefinition>();
            foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(_activeCategory))
            {
                if (state.HasUpgrade(def.Id))
                    continue;
                if (def.PrerequisiteId != null && !state.HasUpgrade(def.PrerequisiteId))
                    continue;
                if (!state.MeetsMetaRequirement(def.MetaRequirement))
                    continue;
                visible.Add(def);
            }
            return visible;
        }
```

- [ ] **Step 2: Route `VisibleRows` + `ClampScroll` through the filter**

Replace `ClampScroll` body with:

```csharp
        private void ClampScroll()
        {
            int total = VisibleCatalogForActiveCategory().Count;
            int maxStart = Math.Max(0, total - _rowsPerPage);
            if (_scrollIndex < 0) _scrollIndex = 0;
            if (_scrollIndex > maxStart) _scrollIndex = maxStart;
        }
```

Replace `VisibleRows` body with:

```csharp
        private IReadOnlyList<UpgradeDefinition> VisibleRows(out int total, out int startIndex)
        {
            IReadOnlyList<UpgradeDefinition> all = VisibleCatalogForActiveCategory();
            total = all.Count;
            startIndex = _scrollIndex;
            int count = Math.Min(_rowsPerPage, total - startIndex);
            return all.Skip(startIndex).Take(count).ToList();
        }
```

- [ ] **Step 3: Simplify `DrawRow`**

Since hidden rows never reach `DrawRow`, the per-row "owned" / "prereq missing" branches can be simplified — every visible row is buyable-or-unaffordable. Replace the existing body with:

```csharp
        private void DrawRow(SpriteBatch b, ClickableComponent slot, UpgradeDefinition def)
        {
            bool affordable = _store.State.JunimoPoints >= def.Cost;

            Color tint = affordable ? Color.White : Color.White * 0.55f;

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, tint, 1f, false);

            Utility.drawTextWithShadow(b, def.DisplayName, Game1.dialogueFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 12), Game1.textColor);

            string statusLine = $"Cost: {def.Cost} JP" + (!affordable ? "  (insufficient)" : "");
            Utility.drawTextWithShadow(b, statusLine, Game1.smallFont,
                new Vector2(slot.bounds.X + 16, slot.bounds.Y + 56),
                affordable ? Game1.textColor : Color.Red);
        }
```

- [ ] **Step 4: Simplify `performHoverAction` tooltip**

The "Requires: foo" line in the hover tooltip can never trigger now (such rows are hidden). In `performHoverAction`, change:

```csharp
                    _hoverText = $"{def.DisplayName}\n{def.Description}\nCost: {def.Cost} JP"
                        + (def.PrerequisiteId != null ? $"\nRequires: {def.PrerequisiteId}" : "");
```

to:

```csharp
                    _hoverText = $"{def.DisplayName}\n{def.Description}\nCost: {def.Cost} JP";
```

- [ ] **Step 5: Verify build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

Expected: build SUCCESS.

- [ ] **Step 6: Verify tests still pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/TheLongestYear/UI/JunimoShrineMenu.cs
git commit -m "$(cat <<'EOF'
feat(ui): shrine menu hides chain-locked, meta-locked, and owned upgrades

Phase A persistence design §F. The player only sees the next tier they can buy
in each keep chain, rather than the whole locked-out chain. Owned upgrades
disappear too (can't re-buy). DrawRow simplified — no more "owned" / "prereq
missing" branches needed since those rows never render.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 15: Integration verification — `tly_commands.txt` checklist

**Files:**
- Modify: `src/TheLongestYear/tly_commands.txt` (DO NOT commit this file — it's a runtime input the user/agent writes to drive the live game; check `.gitignore` for the entry first)

This task is a **manual verification checklist** that the executor walks through on the live mod (after building + deploying). No code changes here — the goal is to confirm the spec §"Success criteria for Phase A" actually hold. The agent should run this checklist *after* the user signals they're ready for a meaningful playtest (per the workflow rule in handoff-night3: "Reserve the user's in-game testing for when it's MEANINGFUL"). Steps 1–4 are mechanical and can be driven entirely via the debug bridge; steps 5–6 ask the user to confirm a single observation each.

- [ ] **Step 1: Re-confirm full test suite green before deploy**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS (223 baseline + ~30 new = ~253 tests).

- [ ] **Step 2: Confirm build is clean for the SMAPI mod**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

Expected: SUCCESS. The `EnableModDeploy` MSBuild target should auto-copy the DLL to the Stardew Mods folder.

- [ ] **Step 3: Drive the verification through `tly_commands.txt`**

Write a verification script the user can trigger by launching SMAPI. With Stardew running on PC + a save loaded:

```
tly_listupgrades
tly_addjp 100000
tly_buyupgrade keep_hoe_1
tly_buyupgrade keep_hoe_2
tly_buyupgrade keep_hoe_3
tly_buyupgrade keep_farming_level_1
tly_buyupgrade keep_farming_level_2
tly_buyupgrade keep_farming_level_3
tly_buyupgrade keep_farming_level_4
tly_buyupgrade keep_farming_level_5
tly_buyupgrade keep_mine_elevator_10
tly_buyupgrade keep_mine_elevator_20
tly_buyupgrade keep_mine_elevator_30
tly_buyupgrade keep_coop
tly_buyupgrade keep_kitchen
tly_buyupgrade keep_bus_unlocked
tly_buyupgrade early_horse
tly_buyupgrade starter_gold_1
tly_buyupgrade backpack_1
tly_meta
tly_reset
tly_meta
tly_runstate
```

Write that into `src/TheLongestYear/tly_commands.txt`, launch the game once via `pwsh -NoProfile -File launch-smapi.ps1`, let the bridge consume the commands, then pull `SMAPI-latest.txt` via `close-smapi.ps1` and read the log.

- [ ] **Step 4: Confirm each spec success criterion in the log + screenshot**

Read `StardewDeliveryService/bin/Release/net6.0/SMAPI-latest.txt` (per project memory: PC SMAPI log path) and verify, in order:

| Criterion | Where to look |
|---|---|
| Tool tier matches highest-purchased keep, capped at peak | `FarmerReset` trace line should show `tools=[hoe=3]` (or whatever you bought). In-game, equip the hoe — it should be visually the Gold Hoe. |
| Skill levels + XP match highest-purchased keep | `FarmerReset` trace shows `skills=[(0,5)]`. In-game, open the player menu — Farming should show 5 with XP bar at level 5 threshold (exact 770 XP per `getBaseExperienceForLevel(5)`). |
| Mine elevator accessible to highest threshold | After `tly_reset` log shows `LowestMineLevelForOrder=30`. Enter the mine, take the elevator — it should offer floors 5/10/15/20/25/30. |
| Backpack at right size per owned upgrade | `FarmerReset` trace shows `slots=24`. Visual: inventory bar shows two extra rows. |
| Starter gold at right amount | `FarmerReset` trace shows `gold=1000` (500 default + 500 from `starter_gold_1`). |
| Buildings + animals match | After reset, walk to the farm. Coop should be at (54, 9). |
| Vault bus restored | Run `tly_runstate` — `VaultBundlesPaid=[34,35,36,37]` printed. |
| Profession picker fires on day 1 for each L5/L10 keep | After reset, the LevelUpMenu(0, 5) for Farming should appear. |
| Shrine UI hides locked entries | Run `tly_openshop` — Carryover tab should show only "Keep Farming Level 6" + "Keep Mine Elevator Floor 40" (the next-tier-up entries). Earlier tiers + later tiers should be hidden. |

- [ ] **Step 5: Document any deviations as TODO entries in `TODO.md`**

If anything in the criteria table above doesn't hold in-game, **do not patch silently**. Append a TODO row under "Open" in `TheLongestYear/TODO.md` describing what failed and what the log showed, and stop. The user reviews before the fix branch starts.

If everything passes, add one line under "Resolved / closed":

```
- Plan 06A persistence + per-stat keep upgrades shipped (2026-05-27 → date of Phase A finish).
```

- [ ] **Step 6: Final commit + handoff doc**

If TODO got an entry, commit it. If the success-line was added, commit that. Then update `TheLongestYear/STATUS.md` (if present — create if missing) to reflect "Phase A complete; Phase B (Cookbook + Craftbook) next."

```bash
git add TheLongestYear/TODO.md
git commit -m "$(cat <<'EOF'
docs: log Plan 06A verification outcome

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Done criteria for this plan

When all 16 tasks are checked off:

1. `dotnet test` is green (~253 tests).
2. `dotnet build` is clean for both projects.
3. The 16 tool-tier + 2 fishing-rod + 50 skill-level + 12 mine-elevator = 80 new chained keep entries exist in `UpgradeCatalog`.
4. `MetaState.MeetsMetaRequirement` dispatches `species:`, `upgrade:`, `quest:`, `mail:`, `season:`, and `_` (default-deny) correctly.
5. `tly_reset` followed by sleeping through day 1 produces the state described in spec §"Success criteria for Phase A".
6. The shrine UI shows only buyable + next-tier upgrades per category.
7. The branch is `feat/v1-plan-06a-persistence-effects` with one commit per Task 1–14 (with Tasks 10–12 sharing one commit per the build-dependency note) and the verification commit from Task 15.

Phase B (Cookbook + Craftbook) is a separate plan written later. Phase C is deferred to LY3.
