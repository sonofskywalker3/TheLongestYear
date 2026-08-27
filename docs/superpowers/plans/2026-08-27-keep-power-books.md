# Keep Power Books Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nineteen bought, reach-gated shrine keeps, one per vanilla power book, whose `Book_*` stat survives the loop reset once owned.

**Architecture:** One Core table (`BookKeepTable`) feeds both the catalog generator and the baseline builder. The reset keeps its wipe-by-default stat rule and re-grants kept book stats from `RunBaseline.KeptBookStats` afterwards, exactly like `MasteryExp`. Row names come from the game through a new `item:` token prefix and an injected item-name provider.

**Tech Stack:** C# / .NET, SMAPI 4, xunit tests in `tests/TheLongestYear.Tests`.

**Spec:** `docs/superpowers/specs/2026-08-27-keep-power-books-design.md`

## Global Constraints

- Work on `master`. Commit locally after every task; never push or release (needs Jeff's "yes, push").
- Patch-bump `src/TheLongestYear/manifest.json` `Version` in every commit that changes code (0.16.8 -> 0.16.9 -> 0.16.10 ...). Docs-only commits do not bump.
- No em dashes in any player-facing string, doc, or commit message.
- Core (`src/TheLongestYear.Core`) has no game or SMAPI references.
- Tests: `dotnet test tests/TheLongestYear.Tests` from the repo root (1113 passing at start).
- Build while the game is running: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`.
- Every commit ends with the `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` footer.

---

### Task 1: `item:` token prefix and item-name provider

**Files:**
- Modify: `src/TheLongestYear.Core/Strings.cs`
- Modify: `src/TheLongestYear.Core/UpgradeDefinition.cs` (ResolveTokens, ~line 51)
- Modify: `src/TheLongestYear/i18n/default.json` (two keys after `upgrade-tpl.keep-mastery.desc`)
- Test: `tests/TheLongestYear.Tests/UpgradeCatalogI18nTests.cs`

**Interfaces:**
- Produces: `Strings.InitItemNames(Func<string,string> provider)`, `Strings.ResetItemNames()`, `Strings.ItemName(string qualifiedId)` (returns the id when no provider). Token values `"item:(O)Book_Speed"` resolve through it.

- [ ] **Step 1: Write the failing tests** (append to `UpgradeCatalogI18nTests.cs`)

```csharp
[Fact]
public void Item_token_resolves_through_item_name_provider()
{
    Strings.InitItemNames(id => id == "(O)Book_Speed" ? "Way Of The Wind pt. 1" : id);
    try
    {
        var def = new UpgradeDefinition("t_item", UpgradeCategory.Carryover,
            "upgrade-tpl.keep-book.name", "upgrade-tpl.keep-book.desc",
            new Dictionary<string, string> { ["book"] = "item:(O)Book_Speed" }, 10);
        Assert.Equal("Keep Way Of The Wind pt. 1", def.DisplayName);
    }
    finally { Strings.ResetItemNames(); }
}

[Fact]
public void Item_token_falls_back_to_the_id_without_a_provider()
{
    Strings.ResetItemNames();
    var def = new UpgradeDefinition("t_item2", UpgradeCategory.Carryover,
        "upgrade-tpl.keep-book.name", "upgrade-tpl.keep-book.desc",
        new Dictionary<string, string> { ["book"] = "item:(O)Book_Speed" }, 10);
    Assert.Equal("Keep (O)Book_Speed", def.DisplayName);
}
```

- [ ] **Step 2: Add the i18n keys** to `default.json` right after `upgrade-tpl.keep-mastery.desc`:

```json
    "upgrade-tpl.keep-book.name": "Keep {{book}}",
    "upgrade-tpl.keep-book.desc": "Start each loop with {{book}} already read. Its power stays with you.",
```

- [ ] **Step 3: Run the tests, expect a compile failure** (`InitItemNames` missing): `dotnet test tests/TheLongestYear.Tests --filter UpgradeCatalogI18nTests`

- [ ] **Step 4: Implement** in `Strings.cs`:

```csharp
    private static Func<string, string>? _itemNames;

    /// <summary>Item display-name provider (glue wires ItemRegistry). Uninitialised, ItemName
    /// echoes the qualified id, loud and never a crash, same contract as Get.</summary>
    public static void InitItemNames(Func<string, string> provider)
        => _itemNames = provider ?? throw new ArgumentNullException(nameof(provider));

    public static void ResetItemNames() => _itemNames = null;

    public static string ItemName(string qualifiedId)
        => _itemNames == null ? qualifiedId : _itemNames(qualifiedId);
```

and in `UpgradeDefinition.ResolveTokens`:

```csharp
        const string I18nTokenPrefix = "i18n:";
        const string ItemTokenPrefix = "item:";
        var resolved = new Dictionary<string, string>(tokens.Count);
        foreach (var kv in tokens)
        {
            if (kv.Value.StartsWith(I18nTokenPrefix, StringComparison.Ordinal))
                resolved[kv.Key] = Strings.Get(kv.Value.Substring(I18nTokenPrefix.Length));
            else if (kv.Value.StartsWith(ItemTokenPrefix, StringComparison.Ordinal))
                resolved[kv.Key] = Strings.ItemName(kv.Value.Substring(ItemTokenPrefix.Length));
            else
                resolved[kv.Key] = kv.Value;
        }
        return resolved;
```

Update the summary comment above `ResolveTokens` to mention both prefixes.

- [ ] **Step 5: Run the full suite.** The i18n guard's "no unreachable key" check may fail until Task 2 references the two new keys from source; if so, continue to Task 2 and commit Task 1 and Task 2 together.

- [ ] **Step 6: Commit** (bump manifest to 0.16.9)

```bash
git add src/TheLongestYear.Core/Strings.cs src/TheLongestYear.Core/UpgradeDefinition.cs src/TheLongestYear/i18n/default.json tests/TheLongestYear.Tests/UpgradeCatalogI18nTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.9: item: token prefix resolves vanilla item names for catalog rows"
```

---

### Task 2: `BookKeepTable` and the catalog generator

**Files:**
- Create: `src/TheLongestYear.Core/BookKeepTable.cs`
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs` (add `CarryoverBookKeeps()` after `CarryoverMasteryKeeps`)
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs:276` (add `entries.AddRange(UpgradeCatalogGenerators.CarryoverBookKeeps());` after the mastery line)
- Test: `tests/TheLongestYear.Tests/BookKeepTests.cs` (new)

**Interfaces:**
- Produces: `BookKeepTable.Entries : IReadOnlyList<BookKeep>` where `record BookKeep(string StatKey, string UpgradeId, long Cost, string? PrerequisiteId)`; `BookKeepTable.UpgradeIdFor(string statKey)`; `BookKeepTable.ReachFor(string statKey)`; constants `StatKeyPrefix = "Book_"`, `UpgradeIdPrefix = "keep_book_"`, `ReachMetric = "book"`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BookKeepTests
{
    [Fact]
    public void Table_has_nineteen_books_totalling_6850_jp()
    {
        Assert.Equal(19, BookKeepTable.Entries.Count);
        Assert.Equal(6850, BookKeepTable.Entries.Sum(e => e.Cost));
        Assert.Equal(19, BookKeepTable.Entries.Select(e => e.StatKey).Distinct().Count());
        Assert.All(BookKeepTable.Entries, e => Assert.StartsWith("Book_", e.StatKey));
    }

    [Theory]
    [InlineData("Book_PriceCatalogue", "keep_book_pricecatalogue", 150)]
    [InlineData("Book_Woodcutting", "keep_book_woodcutting", 350)]
    [InlineData("Book_Bombs", "keep_book_bombs", 500)]
    [InlineData("Book_Defense", "keep_book_defense", 600)]
    [InlineData("Book_Speed", "keep_book_speed", 750)]
    [InlineData("Book_Speed2", "keep_book_speed2", 750)]
    public void Bands_price_each_book(string statKey, string id, long cost)
    {
        BookKeep e = BookKeepTable.Entries.Single(x => x.StatKey == statKey);
        Assert.Equal(id, e.UpgradeId);
        Assert.Equal(cost, e.Cost);
    }

    [Fact]
    public void Only_speed_two_has_a_prerequisite()
    {
        Assert.Equal("keep_book_speed",
            BookKeepTable.Entries.Single(e => e.StatKey == "Book_Speed2").PrerequisiteId);
        Assert.All(BookKeepTable.Entries.Where(e => e.StatKey != "Book_Speed2"),
            e => Assert.Null(e.PrerequisiteId));
    }

    [Fact]
    public void Catalog_carries_every_book_as_a_reach_gated_carryover_row()
    {
        foreach (BookKeep e in BookKeepTable.Entries)
        {
            UpgradeDefinition? def = UpgradeCatalog.TryGet(e.UpgradeId);
            Assert.NotNull(def);
            Assert.Equal(UpgradeCategory.Carryover, def!.Category);
            Assert.Equal(e.Cost, def.Cost);
            Assert.Equal(e.PrerequisiteId, def.PrerequisiteId);
            Assert.Equal($"book:{e.StatKey}", def.RunReachRequirement);
        }
    }
}
```

- [ ] **Step 2: Run, expect compile failure** (`BookKeepTable` missing).

- [ ] **Step 3: Create `BookKeepTable.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One power book the shrine can keep across a reset.</summary>
public sealed record BookKeep(string StatKey, string UpgradeId, long Cost, string? PrerequisiteId);

/// <summary>
/// The single source of truth for the Keep &lt;book&gt; rows (spec 2026-08-27 keep-power-books).
/// Feeds both the catalog generator (what is sold) and RunBaselineBuilder (what is re-granted),
/// so the two can never drift. Stat keys are vanilla StatKeys.Book_* (decompile StatKeys.cs);
/// each is a binary flag set by Object.readBook. Prices are three bands by the power's value
/// over a year, not the book's gold price: Convenience 150, Yield 350, Power 500..750.
/// </summary>
public static class BookKeepTable
{
    public const string StatKeyPrefix = "Book_";
    public const string UpgradeIdPrefix = "keep_book_";
    public const string ReachMetric = "book";

    private const long Convenience = 150;
    private const long Yield = 350;

    private static readonly (string StatKey, long Cost, string? PrereqStatKey)[] Rows =
    {
        // Convenience.
        ("Book_PriceCatalogue",  Convenience, null),
        ("Book_AnimalCatalogue", Convenience, null),
        ("Book_Trash",           Convenience, null),
        ("Book_Grass",           Convenience, null),
        ("Book_Horse",           Convenience, null),
        // Yield.
        ("Book_Woodcutting", Yield, null),
        ("Book_WildSeeds",   Yield, null),
        ("Book_Roe",         Yield, null),
        ("Book_Crabbing",    Yield, null),
        ("Book_Diamonds",    Yield, null),
        ("Book_Mystery",     Yield, null),
        ("Book_Artifact",    Yield, null),
        ("Book_Void",        Yield, null),
        ("Book_Marlon",      Yield, null),
        ("Book_Friendship",  Yield, null),
        // Power.
        ("Book_Bombs",   500, null),
        ("Book_Defense", 600, null),
        ("Book_Speed",   750, null),
        ("Book_Speed2",  750, "Book_Speed"),   // vanilla sells pt. 2 only after pt. 1
    };

    public static IReadOnlyList<BookKeep> Entries { get; } = Rows
        .Select(r => new BookKeep(r.StatKey, UpgradeIdFor(r.StatKey), r.Cost,
            r.PrereqStatKey == null ? null : UpgradeIdFor(r.PrereqStatKey)))
        .ToList();

    /// <summary>keep_book_&lt;stat key after "Book_", lower-cased&gt;.</summary>
    public static string UpgradeIdFor(string statKey)
    {
        if (!statKey.StartsWith(StatKeyPrefix, StringComparison.Ordinal))
            throw new ArgumentException($"Not a Book_* stat key: {statKey}", nameof(statKey));
        return UpgradeIdPrefix + statKey.Substring(StatKeyPrefix.Length).ToLowerInvariant();
    }

    public static string ReachFor(string statKey) => $"{ReachMetric}:{statKey}";
}
```

- [ ] **Step 4: Add the generator** to `UpgradeCatalogGenerators.cs` after `CarryoverMasteryKeeps`:

```csharp
    /// <summary>Yield the 19 Carryover Keep-&lt;book&gt; rows from <see cref="BookKeepTable"/>.
    /// Names come from the game via the item: token (vanilla display name, localized).</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverBookKeeps()
    {
        foreach (BookKeep book in BookKeepTable.Entries)
        {
            var tokens = new Dictionary<string, string> { ["book"] = $"item:(O){book.StatKey}" };
            yield return new UpgradeDefinition(
                book.UpgradeId, UpgradeCategory.Carryover,
                "upgrade-tpl.keep-book.name", "upgrade-tpl.keep-book.desc", tokens,
                book.Cost, book.PrerequisiteId,
                metaRequirement: null, runReachRequirement: BookKeepTable.ReachFor(book.StatKey));
        }
    }
```

and in `UpgradeCatalog.Build()` after the mastery line: `entries.AddRange(UpgradeCatalogGenerators.CarryoverBookKeeps());`

- [ ] **Step 5: Run the full suite, expect green.** Without a provider a row's DisplayName resolves to `Keep (O)Book_X`, which is not the raw key, so the catalog i18n test passes.

- [ ] **Step 6: Commit** (manifest 0.16.10)

```bash
git add src/TheLongestYear.Core/BookKeepTable.cs src/TheLongestYear.Core/UpgradeCatalogGenerators.cs src/TheLongestYear.Core/UpgradeCatalog.cs tests/TheLongestYear.Tests/BookKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.10: nineteen Keep <book> shrine rows from BookKeepTable"
```

---

### Task 3: `book:` reach requirement

**Files:**
- Modify: `src/TheLongestYear.Core/RunReachRequirement.cs:33` (keyed-flag form)
- Modify: `src/TheLongestYear/Integration/RunReachEvaluator.cs:69` (metric switch)
- Test: `tests/TheLongestYear.Tests/RunReachRequirementTests.cs`, `tests/TheLongestYear.Tests/KeepShopFilterTests.cs`

**Interfaces:**
- Consumes: `BookKeepTable.ReachMetric`.
- Produces: `RunReachRequirement.Parse("book:Book_Speed")` yields Metric `book`, Key `Book_Speed`, Threshold 1.

- [ ] **Step 1: Failing tests.** Add to the existing `[Theory]` in `RunReachRequirementTests.cs`:

```csharp
    [InlineData("book:Book_Speed", "book", "Book_Speed", 1)]
```

and to `KeepShopFilterTests.cs`:

```csharp
    [Fact]
    public void Book_keep_appears_only_once_the_book_was_read_this_loop()
    {
        var meta = new MetaState();
        var reached = new Dictionary<string, int>();
        List<string> Buyable() => KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Carryover, meta, Reach(reached))
            .Select(d => d.Id).ToList();

        Assert.DoesNotContain("keep_book_speed", Buyable());
        reached["book:Book_Speed"] = 1;
        Assert.Contains("keep_book_speed", Buyable());
        Assert.DoesNotContain("keep_book_speed2", Buyable());   // chained on pt. 1

        meta.OwnedUpgrades.Add("keep_book_speed");
        reached["book:Book_Speed2"] = 1;
        Assert.Contains("keep_book_speed2", Buyable());
        Assert.DoesNotContain("keep_book_speed", Buyable());    // owned rows are not buyable
    }
```

- [ ] **Step 2: Run, expect the theory row and the filter test to fail** (Parse returns null for `book:`).

- [ ] **Step 3: Implement.** In `RunReachRequirement.Parse`, extend the keyed-flag condition:

```csharp
        if (parts.Length == 2 && parts[1].Length > 0
            && (parts[0] == "scythe" || parts[0] == "building" || parts[0] == BookKeepTable.ReachMetric))
            return new RunReachRequirement(parts[0], parts[1], 1);
```

Update the comment above it to list `book:Book_Speed`. In `RunReachEvaluator.Meets` add a switch arm:

```csharp
                "book"     => p.stats.Get(r.Key) != 0 ? 1 : 0,   // vanilla Book_* flag, set by Object.readBook
```

- [ ] **Step 4: Run the full suite, expect green.**

- [ ] **Step 5: Commit** (manifest 0.16.11)

```bash
git add src/TheLongestYear.Core/RunReachRequirement.cs src/TheLongestYear/Integration/RunReachEvaluator.cs tests/TheLongestYear.Tests/RunReachRequirementTests.cs tests/TheLongestYear.Tests/KeepShopFilterTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.11: book:<stat> reach gate for Keep <book> rows"
```

---

### Task 4: Baseline, builder, and the reset re-grant

**Files:**
- Modify: `src/TheLongestYear.Core/RunBaseline.cs` (new property after `MasteryLevel`)
- Modify: `src/TheLongestYear.Core/RunBaselineBuilder.cs` (fill it in `Build`)
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs` (after the mastery block; summary log)
- Modify: `src/TheLongestYear.Core/StatResetRules.cs` (header comment only)
- Test: `tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs`

**Interfaces:**
- Produces: `RunBaseline.KeptBookStats : IReadOnlyList<string>` (stat keys, default empty).

- [ ] **Step 1: Failing tests** (append to `RunBaselineBuilderTests.cs`; add `using System.Linq;`)

```csharp
    [Fact]
    public void No_book_keeps_means_no_kept_book_stats()
    {
        var b = RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Empty(b.KeptBookStats);
    }

    [Fact]
    public void Owned_book_keeps_map_to_their_stat_keys()
    {
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("keep_book_speed");
        meta.OwnedUpgrades.Add("keep_book_pricecatalogue");
        meta.OwnedUpgrades.Add("keep_mastery_1");   // unrelated keep, must not leak in
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(new[] { "Book_PriceCatalogue", "Book_Speed" }, b.KeptBookStats.OrderBy(k => k).ToArray());
    }
```

- [ ] **Step 2: Run, expect compile failure** (`KeptBookStats` missing).

- [ ] **Step 3: Implement.** `RunBaseline.cs`, after `MasteryLevel`:

```csharp
    /// <summary>Vanilla <c>Book_*</c> stat keys to set to 1 after the stat wipe (Keep &lt;book&gt;
    /// rows, spec 2026-08-27). Empty when no book keep is owned.</summary>
    public IReadOnlyList<string> KeptBookStats { get; init; } = new List<string>();
```

`RunBaselineBuilder.Build`, before `return new RunBaseline`:

```csharp
        // Kept power books: every owned keep_book_* row contributes its Book_* stat key.
        var keptBooks = new List<string>();
        foreach (BookKeep book in BookKeepTable.Entries)
            if (meta.HasUpgrade(book.UpgradeId))
                keptBooks.Add(book.StatKey);
```

and in the initializer: `KeptBookStats = keptBooks,`.

`FarmerReset.Apply`, directly after the mastery `if` block:

```csharp
            // Power books: Keep <book> rows (spec 2026-08-27). The wipe above removed every
            // Book_* flag (StatResetRules stays wipe-by-default); re-grant only the kept ones, the
            // same shape as the MasteryExp re-seed. Set, not Increment: the flag is binary and
            // Object.readBook treats any non-zero as "already read".
            foreach (string statKey in baseline.KeptBookStats)
                p.stats.Set(statKey, 1);
```

and in the summary log after `mastery={baseline.MasteryLevel}, ` add
`$"books=[{string.Join(",", baseline.KeptBookStats)}], "`.

`StatResetRules.cs` header, after the "Falls out WIPED by default" paragraph:

```csharp
        // KEPT-THEN-REGRANTED (spec 2026-08-27 keep-power-books): Book_* flags are still wiped
        // here; FarmerReset then re-sets the ones the player BOUGHT at the shrine from
        // RunBaseline.KeptBookStats, the same shape as the MasteryExp re-seed. The wipe-by-default
        // rule is unchanged; a kept book is a baseline re-grant, never a leak.
```

- [ ] **Step 4: Run the full suite and build the mod**, expect green and a clean build.

- [ ] **Step 5: Commit** (manifest 0.16.12)

```bash
git add src/TheLongestYear.Core/RunBaseline.cs src/TheLongestYear.Core/RunBaselineBuilder.cs src/TheLongestYear/Loop/FarmerReset.cs src/TheLongestYear.Core/StatResetRules.cs tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.12: reset re-grants kept Book_* stats from the baseline"
```

---

### Task 5: Glue: item names from `ItemRegistry`, `tly_readbook`

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (after `Strings.Init` ~line 84; console command registration ~line 261; `ExecuteDebugLine` switch ~line 1683; new handler next to `CmdBuyUpgrade`)

**Interfaces:**
- Consumes: `Strings.InitItemNames`, `BookKeepTable.Entries`, `BookKeepTable.StatKeyPrefix`, `BookKeepTable.ReachFor`, `BookKeepTable.UpgradeIdFor`.

- [ ] **Step 1: Wire the item-name provider** right after the `Strings.Init(...)` call:

```csharp
            // Vanilla item display names for catalog rows that use the item: token (Keep <book>).
            TheLongestYear.Core.Strings.InitItemNames(id => ItemRegistry.GetDataOrErrorItem(id).DisplayName);
```

- [ ] **Step 2: Register the command** next to `tly_buyupgrade`:

```csharp
            helper.ConsoleCommands.Add("tly_readbook", "Debug: mark a power book as read (sets its Book_* stat). No args lists every Book_* stat. Usage: tly_readbook [Book_Id]", this.CmdReadBook);
```

add `case "tly_readbook": this.CmdReadBook(command, args); break;` to `ExecuteDebugLine`, and the handler after `CmdBuyUpgrade`:

```csharp
        private void CmdReadBook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farmer p = Game1.player;
            if (args.Length == 0)
            {
                var sb = new System.Text.StringBuilder("tly_readbook: ");
                foreach (BookKeep book in BookKeepTable.Entries)
                    sb.Append(book.StatKey).Append('=').Append(p.stats.Get(book.StatKey)).Append(' ');
                this.Monitor.Log(sb.ToString().TrimEnd(), LogLevel.Info);
                return;
            }
            string key = args[0];
            if (!key.StartsWith(BookKeepTable.StatKeyPrefix, System.StringComparison.Ordinal))
            {
                this.Monitor.Log($"tly_readbook: '{key}' is not a Book_* stat key.", LogLevel.Warn);
                return;
            }
            p.stats.Set(key, 1);
            this.Monitor.Log($"tly_readbook: {key}=1 (reach '{BookKeepTable.ReachFor(key)}' now met; buy {BookKeepTable.UpgradeIdFor(key)} at the shrine or via tly_buyupgrade).", LogLevel.Info);
        }
```

- [ ] **Step 3: Build** (`-p:EnableModDeploy=false` if the game is open) and run the suite; expect clean.

- [ ] **Step 4: Commit** (manifest 0.16.13)

```bash
git add src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.13: item names from ItemRegistry; tly_readbook debug command"
```

---

### Task 6: Live smoke on the Rodger throwaway save

**Files:** none (verification). Tools: `tools/deploy.ps1`, `tools/send-smapi-command.ps1`, `tools/game.ps1`, SMAPI log at `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`.

- [ ] **Step 1: Ask Jeff before driving the desktop** (house rule). Then `pwsh -NoProfile -File tools/deploy.ps1` (builds, deploys, launches).
- [ ] **Step 2: Load the throwaway save:** `send-smapi-command.ps1 "tly_loadsave"` (Rodger None_* save; never the Load menu).
- [ ] **Step 3: Chain:** `send-smapi-command.ps1 "tly_readbook Book_Speed" "tly_readbook" "tly_addjp 1000" "tly_buyupgrade keep_book_speed" "tly_meta"`. Expect the log to show `Book_Speed=1`, other books 0, and `keep_book_speed` owned.
- [ ] **Step 4: Reset:** `send-smapi-command.ps1 "tly_reset"`. In the log find the `FarmerReset:` line and confirm `books=[Book_Speed]`.
- [ ] **Step 5:** `send-smapi-command.ps1 "tly_readbook"`; expect `Book_Speed=1` and every other book 0 (unbought books wiped).
- [ ] **Step 6 (optional, needs the desktop):** open the shrine on a Fail night and eyeball a "Keep Way Of The Wind pt. 1" row. Skip if Jeff does not want the desktop driven; the provider is unit-tested.
- [ ] **Step 7:** Record the result table in `TODO.md` under the brainstorm entry and mark it built; update the `STATUS.md` top section.

---

### Task 7: Docs

**Files:**
- Modify: `README.md` (What's New + Carryover list), `docs/nexus-description.bbcode` (identical content), `CHANGELOG.md` (`## Unreleased`), `TODO.md`, `STATUS.md`.

- [ ] **Step 1: CHANGELOG** under `## Unreleased`:

```
- Keep <book>: nineteen new Carryover keeps, one per vanilla power book (Way of the Wind, Friendship 101, ...). A row appears on a Fail night once you have read that book this loop; once bought, the book's power survives every reset. Priced 150 to 750 JP by band. Debug: tly_readbook.
```

- [ ] **Step 2: README + Nexus:** add the same paragraph to "What's New" and one line to the Carryover keep list, Markdown in README, BBCode in the Nexus file, identical wording. No em dashes.
- [ ] **Step 3: Commit** (docs only, no bump): `git commit -m "docs: Keep <book> rows in What's New, CHANGELOG, TODO/STATUS"`.
