# Keep Wallet Items and Stardrops Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eighteen new Junimo Shrine rows that keep the nine wallet items, the two event-granted powers (Bear's Knowledge, Spring Onion Mastery) and the seven Stardrops across a rewind, per item, bought with JP.

**Architecture:** One Core table (`WalletKeepTable`) drives the catalog generators and the baseline builder, mirroring `BookKeepTable`. Reach gating gets two keyed metrics (`mail:`, `event:`) and one bare metric (`stardrop_mines`). `RunBaseline` carries the kept mail flags, kept event ids and Stardrop count; `FarmerReset` re-applies them after its existing wipes. The two power events join `EventGatingTables.Default.ReplayableEventIds` so an unbought one is earned again each loop.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony (not needed here), xunit tests in `tests/TheLongestYear.Tests`.

**Spec:** `docs/superpowers/specs/2026-08-27-keep-wallet-stardrops-design.md`

## Global Constraints

- Work on `master`. Bump `src/TheLongestYear/manifest.json` `Version` by one patch on EVERY code commit (start 0.16.19). Docs-only commits do not bump.
- Never push. Never use em dashes in player-facing text or docs.
- Prices: Convenience 150, Yield 350, Skull Key 750, each Stardrop 500. Total 6,950 JP.
- Row ids: `keep_wallet_<slug>` and `keep_stardrop_<source>` exactly as in the spec table.
- Every player-visible string goes through `i18n/default.json`; `I18nGuardTests` fails on a missing or unreachable key.
- Run tests with `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj` from the repo root. Build the mod with `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` (this also deploys to the PC Mods folder).

---

### Task 1: `WalletKeepTable` in Core

**Files:**
- Create: `src/TheLongestYear.Core/WalletKeepTable.cs`
- Test: `tests/TheLongestYear.Tests/WalletKeepTests.cs`

**Interfaces:**
- Produces:
  - `public enum WalletKeepKind { Mail, Event, Stardrop }`
  - `public sealed record WalletKeep(string UpgradeId, WalletKeepKind Kind, string Reach, IReadOnlyList<string> MailFlags, string? EventId, long Cost, string? PrerequisiteId)`
  - `public static class WalletKeepTable { IReadOnlyList<WalletKeep> Entries; const string WalletIdPrefix = "keep_wallet_"; const string StardropIdPrefix = "keep_stardrop_"; const string MailMetric = "mail"; const string EventMetric = "event"; const string StardropMinesMetric = "stardrop_mines"; const string BearEventId = "2120303"; const string SpringOnionEventId = "3910979"; const int StardropStamina = 34; const int BaseStamina = 270; }`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WalletKeepTests
{
    [Fact]
    public void Table_has_eighteen_rows_totalling_6950_jp()
    {
        Assert.Equal(18, WalletKeepTable.Entries.Count);
        Assert.Equal(6950, WalletKeepTable.Entries.Sum(e => e.Cost));
        Assert.Equal(18, WalletKeepTable.Entries.Select(e => e.UpgradeId).Distinct().Count());
        Assert.Equal(11, WalletKeepTable.Entries.Count(e => e.UpgradeId.StartsWith("keep_wallet_")));
        Assert.Equal(7, WalletKeepTable.Entries.Count(e => e.Kind == WalletKeepKind.Stardrop));
    }

    [Theory]
    [InlineData("keep_wallet_dwarvish", 150, "mail:HasDwarvishTranslationGuide")]
    [InlineData("keep_wallet_bearsknowledge", 150, "event:2120303")]
    [InlineData("keep_wallet_springonion", 150, "event:3910979")]
    [InlineData("keep_wallet_rustykey", 350, "mail:HasRustyKey")]
    [InlineData("keep_wallet_skullkey", 750, "mail:HasSkullKey")]
    [InlineData("keep_stardrop_fair", 500, "mail:CF_Fair")]
    [InlineData("keep_stardrop_mines", 500, "stardrop_mines")]
    [InlineData("keep_stardrop_museum", 500, "mail:museumComplete")]
    public void Rows_have_the_spec_price_and_reach(string id, long cost, string reach)
    {
        WalletKeep e = WalletKeepTable.Entries.Single(x => x.UpgradeId == id);
        Assert.Equal(cost, e.Cost);
        Assert.Equal(reach, e.Reach);
    }

    [Fact]
    public void Only_the_wizard_chain_has_prerequisites()
    {
        Assert.Equal("keep_wallet_rustykey",
            WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_darktalisman").PrerequisiteId);
        Assert.Equal("keep_wallet_darktalisman",
            WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_magicink").PrerequisiteId);
        Assert.All(WalletKeepTable.Entries.Where(e =>
                e.UpgradeId != "keep_wallet_darktalisman" && e.UpgradeId != "keep_wallet_magicink"),
            e => Assert.Null(e.PrerequisiteId));
    }

    [Fact]
    public void Skull_key_keeps_the_door_too_and_mines_stardrop_keeps_cf_mines()
    {
        var skull = WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_skullkey");
        Assert.Equal(new[] { "HasSkullKey", "HasUnlockedSkullDoor" }, skull.MailFlags);
        var mines = WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_stardrop_mines");
        Assert.Equal(new[] { "CF_Mines" }, mines.MailFlags);
        var bear = WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_bearsknowledge");
        Assert.Empty(bear.MailFlags);
        Assert.Equal("2120303", bear.EventId);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter WalletKeepTests`
Expected: build error, `WalletKeepTable` does not exist.

- [ ] **Step 3: Write the table**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

public enum WalletKeepKind { Mail, Event, Stardrop }

/// <summary>One wallet item, event-granted power, or Stardrop source the shrine can keep.</summary>
public sealed record WalletKeep(
    string UpgradeId, WalletKeepKind Kind, string Reach,
    IReadOnlyList<string> MailFlags, string? EventId, long Cost, string? PrerequisiteId);

/// <summary>
/// Single source of truth for the Keep wallet / Stardrop rows (spec 2026-08-27
/// keep-wallet-stardrops). Feeds the catalog generators (what is sold) and RunBaselineBuilder
/// (what is re-granted). Wallet items are Farmer.mailReceived flags (decompile Farmer.cs
/// 1278..1400); Bear's Knowledge and Spring Onion Mastery are Data/Powers SEEN_EVENT grants;
/// each Stardrop source marks itself claimed with a CF_* mail (Utility.cs 5834..5872), the
/// museum with "museumComplete". A kept Stardrop re-adds that marker so the source stays shut.
/// </summary>
public static class WalletKeepTable
{
    public const string WalletIdPrefix = "keep_wallet_";
    public const string StardropIdPrefix = "keep_stardrop_";
    public const string MailMetric = "mail";
    public const string EventMetric = "event";
    public const string StardropMinesMetric = "stardrop_mines";
    public const string BearEventId = "2120303";
    public const string SpringOnionEventId = "3910979";
    public const int BaseStamina = 270;
    public const int StardropStamina = 34;

    private const long Convenience = 150;
    private const long Yield = 350;
    private const long SkullKey = 750;
    private const long Stardrop = 500;

    private static string Mail(string flag) => $"{MailMetric}:{flag}";
    private static string Event(string id) => $"{EventMetric}:{id}";

    private static WalletKeep Wallet(string slug, string flag, long cost, string? prereqSlug = null, params string[] extraFlags)
    {
        var flags = new List<string> { flag };
        flags.AddRange(extraFlags);
        return new WalletKeep(WalletIdPrefix + slug, WalletKeepKind.Mail, Mail(flag), flags, null, cost,
            prereqSlug == null ? null : WalletIdPrefix + prereqSlug);
    }

    private static WalletKeep Power(string slug, string eventId) =>
        new(WalletIdPrefix + slug, WalletKeepKind.Event, Event(eventId), new List<string>(), eventId, Convenience, null);

    private static WalletKeep Drop(string source, string flag, string? reach = null) =>
        new(StardropIdPrefix + source, WalletKeepKind.Stardrop, reach ?? Mail(flag), new List<string> { flag }, null, Stardrop, null);

    public static IReadOnlyList<WalletKeep> Entries { get; } = new List<WalletKeep>
    {
        // Convenience.
        Wallet("dwarvish", "HasDwarvishTranslationGuide", Convenience),
        Wallet("magnifyingglass", "HasMagnifyingGlass", Convenience),
        Power("bearsknowledge", BearEventId),
        Power("springonion", SpringOnionEventId),
        // Yield.
        Wallet("specialcharm", "HasSpecialCharm", Yield),
        Wallet("rustykey", "HasRustyKey", Yield),
        Wallet("clubcard", "HasClubCard", Yield),
        Wallet("darktalisman", "HasDarkTalisman", Yield, "rustykey"),
        Wallet("magicink", "HasMagicInk", Yield, "darktalisman"),
        Wallet("townkey", "HasTownKey", Yield),
        // Power.
        Wallet("skullkey", "HasSkullKey", SkullKey, null, "HasUnlockedSkullDoor"),
        Drop("fair", "CF_Fair"),
        Drop("fish", "CF_Fish"),
        Drop("mines", "CF_Mines", StardropMinesMetric),   // vanilla accepts CF_Mines OR the level-100 chest
        Drop("sewer", "CF_Sewer"),
        Drop("spouse", "CF_Spouse"),
        Drop("statue", "CF_Statue"),
        Drop("museum", "museumComplete"),
    };

    public static WalletKeep? TryGet(string upgradeId) => Entries.FirstOrDefault(e => e.UpgradeId == upgradeId);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter WalletKeepTests`
Expected: 4 tests (11 cases) PASS.

- [ ] **Step 5: Bump manifest to 0.16.19 and commit**

```bash
git add src/TheLongestYear.Core/WalletKeepTable.cs tests/TheLongestYear.Tests/WalletKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.19: WalletKeepTable, the single source for the keep wallet / Stardrop rows"
```

---

### Task 2: Catalog rows and i18n strings

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs` (after `CarryoverBookKeeps`, ~line 154)
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs:277` (add two `AddRange` lines after the book keeps)
- Modify: `src/TheLongestYear/i18n/default.json` (after the `upgrade-tpl.keep-book.desc` line, ~242)
- Test: `tests/TheLongestYear.Tests/WalletKeepTests.cs`

**Interfaces:**
- Consumes: `WalletKeepTable.Entries` (Task 1).
- Produces: `UpgradeCatalogGenerators.CarryoverWalletKeeps()` and `CarryoverStardropKeeps()`, each `IEnumerable<UpgradeDefinition>`; catalog rows `keep_wallet_*` / `keep_stardrop_*` with `upgrade.<id>.name` / `.desc` keys.

- [ ] **Step 1: Add the failing catalog test**

Append to `WalletKeepTests`:

```csharp
    [Fact]
    public void Catalog_carries_every_row_as_a_reach_gated_carryover_row()
    {
        foreach (WalletKeep e in WalletKeepTable.Entries)
        {
            UpgradeDefinition? def = UpgradeCatalog.TryGet(e.UpgradeId);
            Assert.NotNull(def);
            Assert.Equal(UpgradeCategory.Carryover, def!.Category);
            Assert.Equal(e.Cost, def.Cost);
            Assert.Equal(e.PrerequisiteId, def.PrerequisiteId);
            Assert.Equal(e.Reach, def.RunReachRequirement);
        }
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter WalletKeepTests`
Expected: `Catalog_carries_every_row...` FAILS (`Assert.NotNull` on the first id).

- [ ] **Step 3: Add the generators**

Insert after `CarryoverBookKeeps()` in `UpgradeCatalogGenerators.cs`:

```csharp
    /// <summary>Eleven Keep &lt;wallet item / power&gt; rows (spec 2026-08-27 keep-wallet-stardrops).
    /// Names and descriptions are hand-authored per id in default.json (upgrade.{id}.name/.desc).</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverWalletKeeps()
    {
        foreach (WalletKeep keep in WalletKeepTable.Entries)
        {
            if (keep.Kind == WalletKeepKind.Stardrop) continue;
            yield return new UpgradeDefinition(
                keep.UpgradeId, UpgradeCategory.Carryover, keep.Cost, keep.PrerequisiteId,
                metaRequirement: null, runReachRequirement: keep.Reach);
        }
    }

    /// <summary>Seven Keep Stardrop (source) rows, one per vanilla Stardrop source.</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverStardropKeeps()
    {
        foreach (WalletKeep keep in WalletKeepTable.Entries)
        {
            if (keep.Kind != WalletKeepKind.Stardrop) continue;
            yield return new UpgradeDefinition(
                keep.UpgradeId, UpgradeCategory.Carryover, keep.Cost, keep.PrerequisiteId,
                metaRequirement: null, runReachRequirement: keep.Reach);
        }
    }
```

In `UpgradeCatalog.cs` after `entries.AddRange(UpgradeCatalogGenerators.CarryoverBookKeeps());` add:

```csharp
        entries.AddRange(UpgradeCatalogGenerators.CarryoverWalletKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverStardropKeeps());
```

- [ ] **Step 4: Add the 36 i18n keys**

Insert after `"upgrade-tpl.keep-book.desc": ...,` in `src/TheLongestYear/i18n/default.json` (keep valid JSON; every line ends with a comma except where it is the last in the object):

```json
    "upgrade.keep_wallet_dwarvish.name": "Keep Dwarvish Translation Guide",
    "upgrade.keep_wallet_dwarvish.desc": "Start each loop already able to understand the Dwarf and shop with him.",
    "upgrade.keep_wallet_magnifyingglass.name": "Keep Magnifying Glass",
    "upgrade.keep_wallet_magnifyingglass.desc": "Start each loop with the Magnifying Glass, so secret notes can turn up from day one.",
    "upgrade.keep_wallet_bearsknowledge.name": "Keep Bear's Knowledge",
    "upgrade.keep_wallet_bearsknowledge.desc": "Salmonberries and blackberries sell for three times as much, every loop. Without this, the bear forgets you and has to be met again.",
    "upgrade.keep_wallet_springonion.name": "Keep Spring Onion Mastery",
    "upgrade.keep_wallet_springonion.desc": "Spring onions sell for five times as much, every loop. Without this, the lesson is forgotten and has to be learned again.",
    "upgrade.keep_wallet_specialcharm.name": "Keep Special Charm",
    "upgrade.keep_wallet_specialcharm.desc": "Start each loop with the Special Charm and its little extra luck every day.",
    "upgrade.keep_wallet_rustykey.name": "Keep Rusty Key",
    "upgrade.keep_wallet_rustykey.desc": "Start each loop with the Rusty Key. The sewers, and Krobus, are open from day one.",
    "upgrade.keep_wallet_clubcard.name": "Keep Club Card",
    "upgrade.keep_wallet_clubcard.desc": "Start each loop with the Club Card. The casino is open from day one.",
    "upgrade.keep_wallet_darktalisman.name": "Keep Dark Talisman",
    "upgrade.keep_wallet_darktalisman.desc": "Start each loop with the Dark Talisman. The witch's swamp is open from day one.",
    "upgrade.keep_wallet_magicink.name": "Keep Magic Ink",
    "upgrade.keep_wallet_magicink.desc": "Start each loop with the Magic Ink. The Wizard can build his buildings from day one.",
    "upgrade.keep_wallet_townkey.name": "Keep Town Key",
    "upgrade.keep_wallet_townkey.desc": "Start each loop with Mr. Qi's Town Key. The Walnut Room is open from day one.",
    "upgrade.keep_wallet_skullkey.name": "Keep Skull Key",
    "upgrade.keep_wallet_skullkey.desc": "Start each loop with the Skull Key in your wallet and the Skull Cavern door already open.",
    "upgrade.keep_stardrop_fair.name": "Keep Stardrop (Fair)",
    "upgrade.keep_stardrop_fair.desc": "The Stardew Valley Fair's Stardrop stays with you: +34 max energy every loop. The Fair will not sell you another.",
    "upgrade.keep_stardrop_fish.name": "Keep Stardrop (Fishing)",
    "upgrade.keep_stardrop_fish.desc": "Willy's Stardrop stays with you: +34 max energy every loop. He will not send another.",
    "upgrade.keep_stardrop_mines.name": "Keep Stardrop (Mines)",
    "upgrade.keep_stardrop_mines.desc": "The level 100 Stardrop stays with you: +34 max energy every loop. The chest stays empty.",
    "upgrade.keep_stardrop_sewer.name": "Keep Stardrop (Krobus)",
    "upgrade.keep_stardrop_sewer.desc": "Krobus's Stardrop stays with you: +34 max energy every loop. He will not sell another.",
    "upgrade.keep_stardrop_spouse.name": "Keep Stardrop (Spouse)",
    "upgrade.keep_stardrop_spouse.desc": "Your spouse's Stardrop stays with you: +34 max energy every loop. It will not be given again.",
    "upgrade.keep_stardrop_statue.name": "Keep Stardrop (Secret Woods)",
    "upgrade.keep_stardrop_statue.desc": "The Old Master Cannoli Stardrop stays with you: +34 max energy every loop. The statue stays satisfied.",
    "upgrade.keep_stardrop_museum.name": "Keep Stardrop (Museum)",
    "upgrade.keep_stardrop_museum.desc": "Gunther's Stardrop stays with you: +34 max energy every loop. The museum reward is marked as claimed.",
```

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: all PASS, including `I18nGuardTests` (every generated `upgrade.<id>` key now resolves and none is unreachable). If a guard test names a key, fix the JSON typo it points at.

- [ ] **Step 6: Bump manifest to 0.16.20 and commit**

```bash
git add src/TheLongestYear.Core/UpgradeCatalogGenerators.cs src/TheLongestYear.Core/UpgradeCatalog.cs src/TheLongestYear/i18n/default.json tests/TheLongestYear.Tests/WalletKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.20: eighteen Keep wallet / Stardrop shrine rows with names and descriptions"
```

---

### Task 3: Reach requirements `mail:`, `event:`, `stardrop_mines`

**Files:**
- Modify: `src/TheLongestYear.Core/RunReachRequirement.cs:32-36`
- Modify: `src/TheLongestYear/Integration/RunReachEvaluator.cs:60-75` (the `switch`)
- Test: `tests/TheLongestYear.Tests/WalletKeepTests.cs`

**Interfaces:**
- Consumes: `WalletKeepTable.MailMetric`, `EventMetric`, `StardropMinesMetric`.
- Produces: `RunReachRequirement.Parse("mail:X")` -> Metric `mail`, Key `X`, Threshold 1; `Parse("event:N")` likewise; `Parse("stardrop_mines")` -> Metric `stardrop_mines`, Key null, Threshold 1.

- [ ] **Step 1: Add the failing parse tests**

Append to `WalletKeepTests`:

```csharp
    [Theory]
    [InlineData("mail:HasSkullKey", "mail", "HasSkullKey")]
    [InlineData("event:2120303", "event", "2120303")]
    public void Keyed_reach_forms_parse_with_threshold_one(string raw, string metric, string key)
    {
        RunReachRequirement? r = RunReachRequirement.Parse(raw);
        Assert.NotNull(r);
        Assert.Equal(metric, r!.Metric);
        Assert.Equal(key, r.Key);
        Assert.Equal(1, r.Threshold);
    }

    [Fact]
    public void Bare_stardrop_mines_reach_parses_with_threshold_one()
    {
        RunReachRequirement? r = RunReachRequirement.Parse("stardrop_mines");
        Assert.NotNull(r);
        Assert.Equal("stardrop_mines", r!.Metric);
        Assert.Null(r.Key);
        Assert.Equal(1, r.Threshold);
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter WalletKeepTests`
Expected: the three new cases FAIL (`Parse` returns null).

- [ ] **Step 3: Extend the parser**

Replace the keyed-flag `if` in `RunReachRequirement.Parse` with:

```csharp
        // Bare-flag form: a metric that is itself a yes/no (the evaluator supplies 0/1).
        if (parts.Length == 1 && parts[0] == WalletKeepTable.StardropMinesMetric)
            return new RunReachRequirement(parts[0], null, 1);
        // Keyed-flag form for metrics whose value is a name, not a number (the evaluator
        // supplies 0/1): scythe:golden, building:Coop, book:Book_Speed, mail:HasSkullKey,
        // event:2120303, ...
        if (parts.Length == 2 && parts[1].Length > 0
            && (parts[0] == "scythe" || parts[0] == "building" || parts[0] == BookKeepTable.ReachMetric
                || parts[0] == WalletKeepTable.MailMetric || parts[0] == WalletKeepTable.EventMetric))
            return new RunReachRequirement(parts[0], parts[1], 1);
```

- [ ] **Step 4: Extend the evaluator**

In `RunReachEvaluator.Meets`'s `switch`, after the `"book"` arm add:

```csharp
                "mail"     => HasMail(p, r.Key) ? 1 : 0,          // wallet items / Stardrop source markers
                "event"    => p.eventsSeen.Contains(r.Key) ? 1 : 0, // Data/Powers SEEN_EVENT grants
                "stardrop_mines" => (HasMail(p, "CF_Mines")
                                     || p.chestConsumedMineLevels.GetValueOrDefault(100, false)) ? 1 : 0,
```

and add the helper to the class:

```csharp
        /// <summary>Some wallet getters read Game1.MasterPlayer (HasRustyKey, HasSkullKey, the Dwarvish
        /// guide); single-player that is the same Farmer, so check both sides to be safe.</summary>
        private static bool HasMail(Farmer p, string? flag) =>
            flag != null && (p.mailReceived.Contains(flag)
                || (Game1.MasterPlayer != null && Game1.MasterPlayer.mailReceived.Contains(flag)));
```

- [ ] **Step 5: Run tests and build the mod**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj` then `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
Expected: all tests PASS; build 0 errors.

- [ ] **Step 6: Bump manifest to 0.16.21 and commit**

```bash
git add src/TheLongestYear.Core/RunReachRequirement.cs src/TheLongestYear/Integration/RunReachEvaluator.cs tests/TheLongestYear.Tests/WalletKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.21: mail:, event: and stardrop_mines reach metrics so the new keeps show only once earned"
```

---

### Task 4: Baseline fields and builder

**Files:**
- Modify: `src/TheLongestYear.Core/RunBaseline.cs` (after `KeptBookStats`, ~line 76)
- Modify: `src/TheLongestYear.Core/RunBaselineBuilder.cs:138-160`
- Test: `tests/TheLongestYear.Tests/WalletKeepTests.cs`

**Interfaces:**
- Produces on `RunBaseline`: `IReadOnlyList<string> KeptMailFlags`, `IReadOnlyList<string> KeptEventIds`, `int KeptStardropCount`.

- [ ] **Step 1: Add the failing builder tests**

Append to `WalletKeepTests`:

```csharp
    [Fact]
    public void Builder_maps_owned_rows_to_flags_events_and_stardrop_count()
    {
        var meta = new MetaState { OwnedUpgrades =
            { "keep_wallet_skullkey", "keep_wallet_bearsknowledge", "keep_stardrop_fair", "keep_stardrop_mines" } };
        RunBaseline b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(new[] { "HasSkullKey", "HasUnlockedSkullDoor", "CF_Fair", "CF_Mines" }, b.KeptMailFlags);
        Assert.Equal(new[] { "2120303" }, b.KeptEventIds);
        Assert.Equal(2, b.KeptStardropCount);
    }

    [Fact]
    public void Builder_leaves_everything_empty_when_nothing_is_owned()
    {
        RunBaseline b = RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Empty(b.KeptMailFlags);
        Assert.Empty(b.KeptEventIds);
        Assert.Equal(0, b.KeptStardropCount);
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter WalletKeepTests`
Expected: build error, `KeptMailFlags` does not exist.

- [ ] **Step 3: Add the fields**

In `RunBaseline.cs` after `KeptBookStats`:

```csharp
    /// <summary>mailReceived flags to re-add after the mail wipe: wallet items (HasSkullKey, ...)
    /// and Stardrop source markers (CF_Fair, museumComplete, ...) for owned keep_wallet_* /
    /// keep_stardrop_* rows (spec 2026-08-27 keep-wallet-stardrops).</summary>
    public IReadOnlyList<string> KeptMailFlags { get; init; } = new List<string>();

    /// <summary>Event ids to re-mark seen after the re-seed: Bear's Knowledge (2120303) and
    /// Spring Onion Mastery (3910979) when their keep is owned. Data/Powers grants those on SEEN_EVENT.</summary>
    public IReadOnlyList<string> KeptEventIds { get; init; } = new List<string>();

    /// <summary>Owned keep_stardrop_* rows; max stamina starts at 270 + 34 per kept Stardrop.</summary>
    public int KeptStardropCount { get; init; }
```

- [ ] **Step 4: Fill them in the builder**

In `RunBaselineBuilder.Build`, after the kept-books loop and before `return new RunBaseline`:

```csharp
        // Kept wallet items / powers / Stardrops: owned rows contribute their mail markers, the
        // two power events their id, and each Stardrop row +1 to the stamina count.
        var keptMail = new List<string>();
        var keptEvents = new List<string>();
        int keptStardrops = 0;
        foreach (WalletKeep keep in WalletKeepTable.Entries)
        {
            if (!meta.HasUpgrade(keep.UpgradeId)) continue;
            keptMail.AddRange(keep.MailFlags);
            if (keep.EventId != null) keptEvents.Add(keep.EventId);
            if (keep.Kind == WalletKeepKind.Stardrop) keptStardrops++;
        }
```

and in the initializer after `KeptBookStats = keptBooks,`:

```csharp
            KeptMailFlags = keptMail,
            KeptEventIds = keptEvents,
            KeptStardropCount = keptStardrops,
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: all PASS.

- [ ] **Step 6: Bump manifest to 0.16.22 and commit**

```bash
git add src/TheLongestYear.Core/RunBaseline.cs src/TheLongestYear.Core/RunBaselineBuilder.cs tests/TheLongestYear.Tests/WalletKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.22: RunBaseline carries kept mail flags, power events and Stardrop count"
```

---

### Task 5: Bear's Knowledge and Spring Onion Mastery become replayable

**Files:**
- Modify: `src/TheLongestYear.Core/EventGating.cs:112-127`
- Test: `tests/TheLongestYear.Tests/WalletKeepTests.cs`

- [ ] **Step 1: Add the failing test**

```csharp
    [Fact]
    public void Power_granting_events_are_replayable_so_an_unbought_power_is_earned_again()
    {
        Assert.True(EventGatingTables.Default.IsReplayable(WalletKeepTable.BearEventId));
        Assert.True(EventGatingTables.Default.IsReplayable(WalletKeepTable.SpringOnionEventId));
        Assert.False(EventGatingTables.Default.IsHeldUntilSpring5(WalletKeepTable.BearEventId));
        Assert.False(EventGatingTables.Default.IsFurnaceTeach(WalletKeepTable.BearEventId));
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter Power_granting_events`
Expected: FAIL on the first `Assert.True`.

- [ ] **Step 3: Add the ids to Default**

Replace the `Default` property in `EventGating.cs`:

```csharp
    /// <summary>The live tables, wired with the audited vanilla ids. The furnace teach is replayable
    /// (excluded from the seen re-seed) but suppressed while the recipe is already known this run.
    /// The Demetrius cave scene plays ONCE (held to Spring 5); from loop 2 on it stays seen
    /// (event-hygiene pass 2026-06-10) and the per-loop mushrooms-vs-bats re-choice is offered by
    /// the lightweight <c>CaveChoicePrompt</c> on cave entry instead of replaying the cutscene.
    /// Bear's Knowledge (Woods 2120303) and Spring Onion Mastery (Forest 3910979) are Data/Powers
    /// SEEN_EVENT grants with no mail/recipe in their scripts, so the scan never flags them; they
    /// are replayable here so the power is wiped with the loop unless its keep_wallet_* row is
    /// owned (FarmerReset re-marks a kept one seen; spec 2026-08-27 keep-wallet-stardrops).</summary>
    public static EventGatingTables Default { get; } = new EventGatingTables(
        replayable: new[] { FurnaceTeachEventId, WalletKeepTable.BearEventId, WalletKeepTable.SpringOnionEventId },
        holdUntilSpring5: new[] { DemetriusCaveEventId },
        furnace: new[] { FurnaceTeachEventId });
```

- [ ] **Step 4: Run the full suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: all PASS (check no existing test asserts `ReplayableEventIds.Count == 1`; if one does, update it to 3 with a comment naming the two power events).

- [ ] **Step 5: Bump manifest to 0.16.23 and commit**

```bash
git add src/TheLongestYear.Core/EventGating.cs tests/TheLongestYear.Tests/WalletKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.23: Bear's Knowledge and Spring Onion Mastery scenes replay each loop unless kept"
```

---

### Task 6: `FarmerReset` re-grants

**Files:**
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs` (after `p.mailReceived.Clear()` ~151; after the `eventsSeen` re-seed ~203; the `maxStamina` line ~224; the summary log ~262)

No unit test (the mod project is not unit-tested; the live smoke in Task 8 covers it). Build must be clean.

- [ ] **Step 1: Re-add kept mail after the wipe**

Directly after `p.mailReceived.Clear();` (keep `eventsSeen.Clear()` and `questLog.Clear()` in place):

```csharp
            // Kept wallet items / Stardrop source markers (spec 2026-08-27 keep-wallet-stardrops):
            // the wipe above took every flag; put back only the bought ones. A CF_* marker put back
            // here is what stops that Stardrop source paying out again next loop.
            foreach (string flag in baseline.KeptMailFlags)
                p.mailReceived.Add(flag);
```

- [ ] **Step 2: Re-mark kept power events seen after the re-seed**

Directly after `p.eventsSeen.Add("60367");`:

```csharp
            // Kept power events (Bear's Knowledge 2120303, Spring Onion Mastery 3910979): both are
            // replayable, so the re-seed above skipped them; a bought keep re-marks the scene seen,
            // which is exactly how Data/Powers grants the power.
            foreach (string id in baseline.KeptEventIds)
                p.eventsSeen.Add(id);
```

- [ ] **Step 3: Stamina from the kept Stardrop count**

Replace:

```csharp
            // Stardrops are tracked by CF_* mail (wiped above), making them re-collectable
            // each loop — without this their +34s would stack in maxStamina the same way.
            p.maxStamina.Value = 270;
```

with:

```csharp
            // Stardrops are tracked by CF_* mail (wiped above), making them re-collectable
            // each loop; without this their +34s would stack in maxStamina the same way. Kept
            // Stardrops (keep_stardrop_* rows) add their +34 back here, and their CF_* marker was
            // re-added with the kept mail so the source stays shut.
            p.maxStamina.Value = WalletKeepTable.BaseStamina
                + WalletKeepTable.StardropStamina * baseline.KeptStardropCount;
```

- [ ] **Step 4: Extend the summary log**

After `$"books=[{string.Join(",", baseline.KeptBookStats)}], " +` add:

```csharp
                $"wallet=[{string.Join(",", baseline.KeptMailFlags)}], " +
                $"events=[{string.Join(",", baseline.KeptEventIds)}], " +
                $"stardrops={baseline.KeptStardropCount}, " +
```

- [ ] **Step 5: Build**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
Expected: 0 errors (add `using TheLongestYear.Core;` if the file lacks it; it already uses `BookKeep` so it should not).

- [ ] **Step 6: Bump manifest to 0.16.24 and commit**

```bash
git add src/TheLongestYear/Loop/FarmerReset.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.24: FarmerReset re-grants kept wallet items, power events and Stardrop stamina"
```

---

### Task 7: `tly_wallet` debug command

**Files:**
- Create: `src/TheLongestYear/Debug/WalletDebugCommand.cs`
- Modify: `src/TheLongestYear/ModEntry.cs:265` (register next to `tly_readbook`) and the `switch` at ~1697

`ModEntry.cs` is 3,200 lines; put the body in its own static class and keep the ModEntry change to two lines.

- [ ] **Step 1: Write the command class**

```csharp
using System.Text;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Debug
{
    /// <summary>tly_wallet: set a wallet item, power event or Stardrop source marker the way the
    /// game would, so the keep_wallet_* / keep_stardrop_* rows can be smoked from the console.
    /// No args prints every marker in WalletKeepTable with its current state.</summary>
    internal static class WalletDebugCommand
    {
        public const string Usage =
            "Debug: set a wallet/Stardrop marker. Usage: tly_wallet [<MailFlag> | event:<id> | stardrop:<source>]. No args lists all.";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farmer p = Game1.player;
            if (args.Length == 0) { Print(monitor, p); return; }

            string arg = args[0];
            if (arg.StartsWith("event:", System.StringComparison.Ordinal))
            {
                string id = arg.Substring("event:".Length);
                p.eventsSeen.Add(id);
                monitor.Log($"tly_wallet: event {id} marked seen.", LogLevel.Info);
                return;
            }
            if (arg.StartsWith("stardrop:", System.StringComparison.Ordinal))
            {
                string source = arg.Substring("stardrop:".Length);
                WalletKeep? keep = WalletKeepTable.TryGet(WalletKeepTable.StardropIdPrefix + source);
                if (keep == null || keep.Kind != WalletKeepKind.Stardrop)
                {
                    monitor.Log($"tly_wallet: unknown Stardrop source '{source}' (fair, fish, mines, sewer, spouse, statue, museum).", LogLevel.Warn);
                    return;
                }
                foreach (string flag in keep.MailFlags) p.mailReceived.Add(flag);
                p.maxStamina.Value += WalletKeepTable.StardropStamina;
                p.stamina = p.maxStamina.Value;
                monitor.Log($"tly_wallet: Stardrop '{source}' claimed ({string.Join(",", keep.MailFlags)}), max stamina now {p.maxStamina.Value}.", LogLevel.Info);
                return;
            }
            p.mailReceived.Add(arg);
            monitor.Log($"tly_wallet: mail '{arg}' added.", LogLevel.Info);
        }

        private static void Print(IMonitor monitor, Farmer p)
        {
            var sb = new StringBuilder("tly_wallet: ");
            foreach (WalletKeep keep in WalletKeepTable.Entries)
            {
                sb.Append(keep.UpgradeId).Append('=');
                if (keep.EventId != null)
                    sb.Append(p.eventsSeen.Contains(keep.EventId) ? "seen" : "unseen");
                else
                    foreach (string flag in keep.MailFlags)
                        sb.Append(flag).Append(p.mailReceived.Contains(flag) ? "+" : "-");
                sb.Append(' ');
            }
            sb.Append("chest100=").Append(p.chestConsumedMineLevels.GetValueOrDefault(100, false))
              .Append(" maxStamina=").Append(p.maxStamina.Value);
            monitor.Log(sb.ToString(), LogLevel.Info);
        }
    }
}
```

- [ ] **Step 2: Register it in ModEntry**

Next to the `tly_readbook` registration (~line 265):

```csharp
            helper.ConsoleCommands.Add("tly_wallet", TheLongestYear.Debug.WalletDebugCommand.Usage,
                (cmd, a) => TheLongestYear.Debug.WalletDebugCommand.Run(this.Monitor, a));
```

In the command `switch` (~line 1697), next to `case "tly_readbook":`:

```csharp
                case "tly_wallet": TheLongestYear.Debug.WalletDebugCommand.Run(this.Monitor, args); break;
```

(If the `Debug` folder does not exist, create it. If a namespace `TheLongestYear.Debug` collides with `System.Diagnostics.Debug` usage in ModEntry, name the namespace `TheLongestYear.DebugCommands` instead and update both references.)

- [ ] **Step 3: Build and run tests**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` then `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: 0 errors, all PASS.

- [ ] **Step 4: Bump manifest to 0.16.25 and commit**

```bash
git add src/TheLongestYear/Debug/WalletDebugCommand.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.25: tly_wallet debug command for the wallet / Stardrop keep smoke"
```

---

### Task 8: Live smoke on the throwaway save

**Files:** none changed unless a bug is found. Evidence goes into `STATUS.md` (Task 9).

Follow `TODO.md` gotchas and `tools/game.ps1`. Deploy is done by the Release build in Task 7.

- [ ] **Step 1: Load the throwaway save** with `tly_loadsave` (never the Load menu), on Spring 1.
- [ ] **Step 2: Set markers**: `tly_wallet HasSkullKey`, `tly_wallet stardrop:fair`, `tly_wallet event:2120303`, `tly_wallet event:3910979`. `tly_wallet` (no args) must show `keep_wallet_skullkey=HasSkullKey+HasUnlockedSkullDoor-`, `keep_stardrop_fair=CF_Fair+`, both events `seen`, `maxStamina=304`.
- [ ] **Step 3: Buy**: `tly_addjp 2000`, `tly_buyupgrade keep_wallet_skullkey`, `tly_buyupgrade keep_stardrop_fair`, `tly_buyupgrade keep_wallet_bearsknowledge`. Do NOT buy the spring onion keep.
- [ ] **Step 4: Reset**: `tly_reset`, pick a theme in the hub. In the SMAPI log find the `FarmerReset:` trace line; expect `wallet=[HasSkullKey,HasUnlockedSkullDoor,CF_Fair] events=[2120303] stardrops=1`.
- [ ] **Step 5: Verify**: `tly_wallet` shows `HasSkullKey+HasUnlockedSkullDoor+`, `CF_Fair+`, bear `seen`, spring onion `unseen`, `maxStamina=304`. Open the wallet tab of the inventory menu (screenshot) and confirm the Skull Key is drawn and the Powers tab shows Bear's Knowledge.
- [ ] **Step 6: Shop gate**: before buying anything, on a Fail night (or `tly_failreset` if that is the debug path) confirm the shrine lists ONLY the rows whose marker is set (Skull Key, Stardrop (Fair), Bear's Knowledge, Spring Onion Mastery) and none of the other fourteen.
- [ ] **Step 7:** Record PASS/FAIL per step with the log lines. Any FAIL: fix, bump, commit, re-run that step.

---

### Task 9: Docs

**Files:**
- Modify: `CHANGELOG.md` (`## Unreleased` section at the top)
- Modify: `README.md` (What's New block ~line 17, and the Shrine / Carryover feature list)
- Modify: `docs/nexus-description.bbcode` (same two places, identical content in BBCode)
- Modify: `STATUS.md` (top section: built, smoke table), `TODO.md` (the `▶ NEXT SESSION` entry becomes `BUILT 0.16.19 to 0.16.25, not released`)

- [ ] **Step 1: CHANGELOG `## Unreleased`**

```markdown
### Added

- **Keep your wallet items and Stardrops.** Eighteen new keeps at the Junimo Shrine: one per wallet item (Rusty Key, Skull Key, Club Card, Special Charm, Dark Talisman, Magic Ink, Dwarvish Translation Guide, Town Key, Magnifying Glass), one each for Bear's Knowledge and Spring Onion Mastery, and one per Stardrop source (Fair, fishing, mines, Krobus, spouse, Secret Woods, museum). A row appears on a Fail night once you have earned that item this loop; buy it and it survives every rewind. 150 to 750 JP. A kept Stardrop also keeps its source marked as claimed, so the same Stardrop cannot be collected again next loop. Keeping the Skull Key keeps the Skull Cavern door open too.
- **`tly_wallet` console command** to set or list wallet, power and Stardrop markers (debug).

### Changed

- **Bear's Knowledge and Spring Onion Mastery no longer survive a rewind for free.** The game grants them by "you have seen this scene", and the rewind used to re-mark those scenes as seen, so both powers came back every loop unpaid. They are now wiped with the loop like every other power; the bear and the river lesson can be found again, or the keep can be bought.
```

- [ ] **Step 2: README What's New + feature line**

Replace the `## What's New in 0.16.17` block heading with `## What's New in <next release version>` (leave the version for the release step if unknown; write `0.17.0` only if Jeff has declared a minor) and lead with the wallet/Stardrops bullet from the CHANGELOG, then the previous bullets. Add to the Shrine keeps list a line: `- **Keep wallet items and Stardrops** — per item, from 150 JP; a kept Stardrop's source stays claimed.` Mirror both edits in `docs/nexus-description.bbcode` with `[b]...[/b]` and `[list][*]...[/list]` markup, content identical.

- [ ] **Step 3: STATUS and TODO**

STATUS top: `**Branch:** master; 0.16.25 committed locally, NOT pushed, NOT released`, plus a section `## Keep wallet items + Stardrops (0.16.19 to 0.16.25): built, unit-tested, LIVE SMOKE <PASSED|FAILED> <date>` with the Task 8 table. TODO: retitle the entry `### BUILT 2026-08-27 (0.16.19 to 0.16.25, not released): keep wallet items and Stardrops with a JP purchase` and add one paragraph pointing at the spec, plan and smoke.

- [ ] **Step 4: Commit (docs only, no bump)**

```bash
git add CHANGELOG.md README.md docs/nexus-description.bbcode STATUS.md TODO.md
git commit -m "docs: keep wallet items + Stardrops in CHANGELOG Unreleased, README and Nexus description in step, smoke evidence"
```

---

## Self-review

- Spec coverage: rows/prices (T1, T2), names (T2), reach (T3), baseline (T4), replayable events (T5), reset re-grant incl. door + stamina + log (T6), debug command (T7), live smoke (T8), docs incl. the Bear/Spring Onion behaviour change (T9). Edge cases in the spec need no code.
- Types: `WalletKeep` fields used in T2/T4/T7 match T1; `KeptMailFlags` / `KeptEventIds` / `KeptStardropCount` consistent across T4/T6; metrics `mail` / `event` / `stardrop_mines` consistent across T1/T3.
- Version chain: 0.16.19 through 0.16.25, one per code commit.
