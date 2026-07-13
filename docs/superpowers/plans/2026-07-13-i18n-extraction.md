# i18n String Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move every player-visible string in The Longest Year to SMAPI `i18n/default.json` so translations are a JSON file, never a DLL edit.

**Architecture:** A static `Strings` facade in `TheLongestYear.Core` holds an injected provider delegate; `ModEntry` initializes it from `Helper.Translation`, tests initialize it from the real `default.json`. Display strings become key lookups resolved lazily at read time (so locale switches work and static-init order can't bite). English wording stays **byte-identical** — pure extraction.

**Tech Stack:** C# / .NET 6, SMAPI 4.x `ITranslationHelper` ({{token}} interpolation), xunit 2.4 (tests reference Core only — no SMAPI), GMCM API.

**Spec:** `docs/superpowers/specs/2026-07-13-i18n-extraction-design.md` — read it first; its key-prefix table and never-translate list govern every task.

## Global Constraints

- English output must be **byte-identical** to current — no rewording, no whitespace changes, no punctuation changes. When in doubt, copy the literal verbatim.
- **Never translate** (leave hardcoded): `Monitor.Log` text, console/debug-bridge commands + usage text (`ModEntry.ExecuteDebugLine`), save/modData keys, quest ids (`tly.*`), upgrade/modifier ids and requirement tokens (`tool:hoe:1`), item ids/qualifiers (`(O)`, `174`), event ids, mail flags, `Response(...)` first-arg keys, asset paths, sound cues, event command tokens (`warp`, `pause`, `$b`).
- Dialogue codes `@` (player name), `#$b#` (page break), `$h/$s/$a` (poses) move INTO the JSON values and must survive verbatim.
- Interpolation uses SMAPI `{{token}}` syntax only — no `string.Format`, no `$"…"` around translated fragments.
- Per house rules: every task = ONE commit, bump PATCH in `src/TheLongestYear/manifest.json` (`Version`) before building, `git add` specific files only. Current master = 0.11.45; first task commit = 0.11.46, incrementing per task.
- Build: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` (auto-deploys to PC Mods — fine). Tests: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`.
- Repo root for all paths below: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`.

---

### Task 1: `Strings` facade in Core (TDD)

**Files:**
- Create: `src/TheLongestYear.Core/Strings.cs`
- Test: `tests/TheLongestYear.Tests/StringsTests.cs`

**Interfaces:**
- Produces: `TheLongestYear.Core.Strings` — `void Init(Func<string, IReadOnlyDictionary<string, string>?, string> provider)`, `string Get(string key)`, `string Get(string key, IReadOnlyDictionary<string, string> tokens)`, `void Reset()` (test hook). All later tasks call `Strings.Get`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/TheLongestYear.Tests/StringsTests.cs
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StringsTests
{
    public StringsTests() => Strings.Reset();

    [Fact]
    public void Get_WithoutInit_ReturnsKeyItself()
    {
        Assert.Equal("menu.hub.title", Strings.Get("menu.hub.title"));
    }

    [Fact]
    public void Get_UsesInjectedProvider()
    {
        Strings.Init((key, _) => key == "hud.festival-over" ? "The festival is over." : key);
        Assert.Equal("The festival is over.", Strings.Get("hud.festival-over"));
    }

    [Fact]
    public void Get_PassesTokensToProvider()
    {
        IReadOnlyDictionary<string, string>? seen = null;
        Strings.Init((key, tokens) => { seen = tokens; return "x"; });
        Strings.Get("menu.shrine.cost", new Dictionary<string, string> { ["cost"] = "150" });
        Assert.NotNull(seen);
        Assert.Equal("150", seen!["cost"]);
    }

    [Fact]
    public void Get_TokenlessOverload_PassesNullTokens()
    {
        bool sawNull = false;
        Strings.Init((key, tokens) => { sawNull = tokens == null; return "x"; });
        Strings.Get("any.key");
        Assert.True(sawNull);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter StringsTests`
Expected: FAIL — `Strings` does not exist (compile error).

- [ ] **Step 3: Implement**

```csharp
// src/TheLongestYear.Core/Strings.cs
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Translation lookup facade. Core has no SMAPI reference, so ModEntry injects a provider
/// backed by ITranslationHelper at startup; tests inject a dictionary loaded from the real
/// i18n/default.json (see I18nFixture). Uninitialized, Get returns the key itself — loud
/// in-game ("menu.hub.title" on screen), never a crash.
/// </summary>
public static class Strings
{
    private static Func<string, IReadOnlyDictionary<string, string>?, string>? _provider;

    public static void Init(Func<string, IReadOnlyDictionary<string, string>?, string> provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>Test hook — clears the provider so tests can assert uninitialized behavior.</summary>
    public static void Reset() => _provider = null;

    public static string Get(string key)
        => _provider == null ? key : _provider(key, null);

    public static string Get(string key, IReadOnlyDictionary<string, string> tokens)
        => _provider == null ? key : _provider(key, tokens);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter StringsTests`
Expected: 4 PASS. Then run the FULL suite (`dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`): all 546+4 pass (nothing consumes Strings yet).

- [ ] **Step 5: Bump manifest to 0.11.46, build, commit**

```bash
# edit src/TheLongestYear/manifest.json "Version": "0.11.46"
dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release
git add src/TheLongestYear.Core/Strings.cs tests/TheLongestYear.Tests/StringsTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.11.46: i18n scaffold 1/2 — Strings facade in Core (injected provider, key-echo fallback)"
```

---

### Task 2: `i18n/default.json` + ModEntry wiring + test fixture

**Files:**
- Create: `src/TheLongestYear/i18n/default.json`
- Create: `tests/TheLongestYear.Tests/I18nFixture.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (top of `Entry`, ~line 280 before service construction)
- Test: `tests/TheLongestYear.Tests/I18nFixtureTests.cs`

**Interfaces:**
- Consumes: `Strings.Init` from Task 1.
- Produces: `I18nFixture` (xunit collection fixture named `"i18n"`) — every later test class that asserts on display text adds `[Collection("i18n")]`. Also the convention that `default.json` is at `src/TheLongestYear/i18n/default.json` and supports `//`-comment lines (strip on load).

- [ ] **Step 1: Create the seed default.json**

```jsonc
// src/TheLongestYear/i18n/default.json
{
    // ============================================================================
    // The Longest Year — English source of truth.
    // TRANSLATORS: copy this file to <locale>.json (e.g. zh.json), translate the
    // VALUES only. Preserve exactly: {{token}} placeholders, @ (player name),
    // #$b# (page break), $h/$s/$a (portrait poses). Keys missing from your file
    // fall back to English per-key. See docs/TRANSLATING.md.
    // ============================================================================

    // -- hud --------------------------------------------------------------------
    "hud.festival-over": "The festival is over."
}
```

(One real key so the fixture has something to assert; categories fill in per task.)

- [ ] **Step 2: Write the fixture + failing test**

```csharp
// tests/TheLongestYear.Tests/I18nFixture.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Loads the REAL i18n/default.json into Strings so tests assert on real English
/// text and double as a missing-key detector (a missing key comes back as the raw key and
/// fails the text assertion).</summary>
public sealed class I18nFixture
{
    public IReadOnlyDictionary<string, string> Map { get; }

    public I18nFixture()
    {
        Map = Load();
        var map = Map;
        Strings.Init((key, tokens) =>
        {
            if (!map.TryGetValue(key, out string? value))
                return key;
            if (tokens != null)
                foreach (var kv in tokens)
                    value = value.Replace("{{" + kv.Key + "}}", kv.Value, StringComparison.Ordinal);
            return value;
        });
    }

    public static string DefaultJsonPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "TheLongestYear", "i18n", "default.json"));

    private static IReadOnlyDictionary<string, string> Load()
    {
        // SMAPI allows // comments in i18n JSON; System.Text.Json needs them skipped.
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using var doc = JsonDocument.Parse(File.ReadAllText(DefaultJsonPath), options);
        var map = new Dictionary<string, string>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            map[prop.Name] = prop.Value.GetString() ?? "";
        return map;
    }
}

[CollectionDefinition("i18n")]
public class I18nCollection : ICollectionFixture<I18nFixture> { }
```

```csharp
// tests/TheLongestYear.Tests/I18nFixtureTests.cs
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class I18nFixtureTests
{
    [Fact]
    public void Fixture_LoadsRealDefaultJson()
    {
        Assert.Equal("The festival is over.", Strings.Get("hud.festival-over"));
    }
}
```

- [ ] **Step 3: Run to verify pass** (`--filter I18nFixtureTests`; also full suite — note `StringsTests` calls `Strings.Reset()` in its ctor, which is why it must NOT join the `"i18n"` collection).

- [ ] **Step 4: Wire ModEntry**

In `ModEntry.Entry`, as the FIRST statement (before any service reads catalog text):

```csharp
TheLongestYear.Core.Strings.Init((key, tokens) =>
    tokens == null
        ? this.Helper.Translation.Get(key).ToString()
        : this.Helper.Translation.Get(key, tokens).ToString());
```

- [ ] **Step 5: Build and verify packaging**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
Then verify: `Test-Path "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\i18n\default.json"` → True (ModBuildConfig auto-includes `i18n/`; if False, add `<Content Include="i18n\**" CopyToOutputDirectory="PreserveNewest" />` to `src/TheLongestYear/TheLongestYear.csproj` and rebuild).

- [ ] **Step 6: Bump to 0.11.47, commit** (files: `i18n/default.json`, `I18nFixture.cs`, `I18nFixtureTests.cs`, `ModEntry.cs`, `manifest.json`; message `"v0.11.47: i18n scaffold 2/2 — default.json, ModEntry provider wiring, test fixture"`).

---

### Task 3: Upgrade catalog — key-based lazy names (hand-authored rows)

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeDefinition.cs`
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs` (all ~60 `new UpgradeDefinition(...)` rows in `Build()`)
- Modify: `src/TheLongestYear/i18n/default.json` (add `upgrade.*` section)
- Test: existing catalog tests + `tests/TheLongestYear.Tests/UpgradeCatalogI18nTests.cs`

**Interfaces:**
- Consumes: `Strings.Get(string)`, `Strings.Get(string, IReadOnlyDictionary<string,string>)`.
- Produces: new `UpgradeDefinition` constructor signature (below). `DisplayName`/`Description` remain `string` properties — **all consumers (menus, tests) are source-compatible**. Task 4 uses the same constructor with explicit keys+tokens.

- [ ] **Step 1: Rewrite UpgradeDefinition for lazy key resolution**

Replace the stored-string properties and constructor (`UpgradeDefinition.cs:21-62`) with:

```csharp
public string Id { get; }
public UpgradeCategory Category { get; }
public long Cost { get; }
public string? PrerequisiteId { get; }
public string? MetaRequirement { get; }
public string? RunReachRequirement { get; }

private readonly string _nameKey;
private readonly string _descKey;
private readonly IReadOnlyDictionary<string, string>? _tokens;

/// <summary>Resolved lazily so locale changes take effect without a rebuild of the catalog.</summary>
public string DisplayName => _tokens == null ? Strings.Get(_nameKey) : Strings.Get(_nameKey, _tokens);
public string Description => _tokens == null ? Strings.Get(_descKey) : Strings.Get(_descKey, _tokens);

/// <summary>Hand-authored row: keys derive from the id — upgrade.{id}.name / upgrade.{id}.desc.</summary>
public UpgradeDefinition(
    string id, UpgradeCategory category, long cost,
    string? prerequisiteId = null, string? metaRequirement = null, string? runReachRequirement = null)
    : this(id, category, $"upgrade.{id}.name", $"upgrade.{id}.desc", tokens: null, cost,
           prerequisiteId, metaRequirement, runReachRequirement)
{ }

/// <summary>Template row (generators): explicit template keys + tokens.</summary>
public UpgradeDefinition(
    string id, UpgradeCategory category, string nameKey, string descKey,
    IReadOnlyDictionary<string, string>? tokens, long cost,
    string? prerequisiteId = null, string? metaRequirement = null, string? runReachRequirement = null)
{
    if (string.IsNullOrWhiteSpace(id))
        throw new ArgumentException("Id must be non-empty.", nameof(id));
    if (string.IsNullOrWhiteSpace(nameKey))
        throw new ArgumentException("Name key must be non-empty.", nameof(nameKey));
    if (cost < 0)
        throw new ArgumentOutOfRangeException(nameof(cost), cost, "Cost must be non-negative.");
    Id = id; Category = category; Cost = cost;
    _nameKey = nameKey; _descKey = descKey; _tokens = tokens;
    PrerequisiteId = prerequisiteId; MetaRequirement = metaRequirement; RunReachRequirement = runReachRequirement;
}
```

Add `using System.Collections.Generic;` and `using TheLongestYear.Core;`-internal reference as needed.

- [ ] **Step 2: Convert every hand-authored row in `UpgradeCatalog.Build()`**

Mechanical rule for each of the ~60 rows: delete the `displayName` and `description` arguments; move their literal values **verbatim** into `default.json` as `"upgrade.{id}.name"` and `"upgrade.{id}.desc"`. Example (from `UpgradeCatalog.cs:58-63`):

```csharp
// BEFORE
new UpgradeDefinition("backpack_1", UpgradeCategory.Loadout, "Backpack I",
    "Start each loop with the 24-slot backpack.", 150,
    metaRequirement: null, runReachRequirement: "backpack:1"),
// AFTER
new UpgradeDefinition("backpack_1", UpgradeCategory.Loadout, 150,
    metaRequirement: null, runReachRequirement: "backpack:1"),
```

```jsonc
// -- upgrades (hand-authored) — keys derive from the upgrade id ---------------
"upgrade.backpack_1.name": "Backpack I",
"upgrade.backpack_1.desc": "Start each loop with the 24-slot backpack.",
```

Note the argument order shift (cost moves up); positional `prerequisiteId` args like `"starter_gold_1"` in `UpgradeCatalog.cs:80` keep working positionally. Do ALL rows in the file — completeness is enforced by Step 4's test and Task 13's guard.

- [ ] **Step 3: Add the completeness test**

```csharp
// tests/TheLongestYear.Tests/UpgradeCatalogI18nTests.cs
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class UpgradeCatalogI18nTests
{
    public UpgradeCatalogI18nTests(I18nFixture fixture) => _fixture = fixture;
    private readonly I18nFixture _fixture;

    [Fact]
    public void EveryCatalogRow_ResolvesNameAndDescription()
    {
        foreach (var def in UpgradeCatalog.All)
        {
            Assert.False(def.DisplayName.StartsWith("upgrade."),
                $"{def.Id}: DisplayName did not resolve — missing key '{def.DisplayName}' in default.json");
            Assert.False(def.Description.StartsWith("upgrade."),
                $"{def.Id}: Description did not resolve — missing key '{def.Description}' in default.json");
            Assert.DoesNotContain("{{", def.DisplayName);
            Assert.DoesNotContain("{{", def.Description);
        }
    }

    [Fact]
    public void KnownRow_KeepsByteIdenticalEnglish()
    {
        var def = UpgradeCatalog.TryGet("backpack_1")!;
        Assert.Equal("Backpack I", def.DisplayName);
        Assert.Equal("Start each loop with the 24-slot backpack.", def.Description);
    }
}
```

- [ ] **Step 4: Run the FULL test suite.** Existing catalog tests that construct `UpgradeDefinition` directly need their arguments updated to the new signature (same mechanical rule). Any test asserting display text now needs `[Collection("i18n")]`. Expected: all pass.

- [ ] **Step 5: Bump to 0.11.48, build, commit** (message `"v0.11.48: i18n — upgrade catalog hand-authored rows to upgrade.{id}.name/.desc keys (lazy resolution)"`).

---

### Task 4: Upgrade generator templates

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`
- Modify: `src/TheLongestYear/i18n/default.json`

**Interfaces:**
- Consumes: the template constructor from Task 3.

- [ ] **Step 1: Convert the four generators.** Each `$"…"` name/desc becomes a template key + token dictionary. The token VALUES that are English words (tier names, tool/skill display names) become keys too. Full transformations:

`LoadoutToolKeeps()` (`UpgradeCatalogGenerators.cs:43-62`):

```csharp
foreach (var (slug, displayName) in ToolKinds)
    for (int tier = 1; tier <= 4; tier++)
    {
        string id = $"keep_{slug}_{tier}";
        string? prereq = tier == 1 ? null : $"keep_{slug}_{tier - 1}";
        var tokens = new Dictionary<string, string>
        {
            ["tier"] = Strings.Get($"tier.{tier}"),
            ["tool"] = Strings.Get($"tool.{slug}"),
        };
        yield return new UpgradeDefinition(
            id, UpgradeCategory.Loadout,
            "upgrade-tpl.keep-tool.name", "upgrade-tpl.keep-tool.desc", tokens,
            ToolTierCosts[tier - 1], prereq,
            metaRequirement: null, runReachRequirement: $"tool:{slug}:{tier}");
    }
```

**Caution:** tokens are resolved at Build() time here (nested lookups), which would freeze locale for these two words. Instead resolve nested tokens lazily: pass token PLACEHOLDER keys and resolve them in `UpgradeDefinition.DisplayName`. Concretely — extend the resolution in `UpgradeDefinition`:

```csharp
private IReadOnlyDictionary<string, string>? ResolveTokens()
{
    if (_tokens == null) return null;
    var resolved = new Dictionary<string, string>(_tokens.Count);
    foreach (var kv in _tokens)
        resolved[kv.Key] = kv.Value.StartsWith("i18n:", StringComparison.Ordinal)
            ? Strings.Get(kv.Value.Substring(5))
            : kv.Value;
    return resolved;
}
public string DisplayName { get { var t = ResolveTokens(); return t == null ? Strings.Get(_nameKey) : Strings.Get(_nameKey, t); } }
public string Description { get { var t = ResolveTokens(); return t == null ? Strings.Get(_descKey) : Strings.Get(_descKey, t); } }
```

and in the generators pass `["tier"] = $"i18n:tier.{tier}", ["tool"] = $"i18n:tool.{slug}"`. Numeric tokens (`["level"] = level.ToString()`, `["floor"] = floor.ToString()`) pass through untouched.

`CarryoverSkillLevelKeeps()` (`:86-103`): name key `upgrade-tpl.keep-skill.name`, desc key `upgrade-tpl.keep-skill.desc` for L1-4/6-9 and `upgrade-tpl.keep-skill.desc-profession` for L5/L10 (the conditional clause becomes a second full key — cleaner for translators than concatenation); tokens `skill` (`i18n:skill.{slug}`), `level`.

`CarryoverMineElevatorKeeps()` (`:106-123`): keys `upgrade-tpl.keep-elevator.name`/`.desc`, token `floor`.

`CarryoverMasteryKeeps()` (`:131-142`): keys `upgrade-tpl.keep-mastery.name`/`.desc`, token `level`.

`FishingRodTiers` (`:35-40`): the three rows have per-row names — treat as hand-authored: keys `upgrade.keep_fishing_rod_0.name` etc., shared desc key `upgrade-tpl.keep-rod.desc` = `"Start each loop with your Fishing Rod at this tier."`.

- [ ] **Step 2: Add the JSON** (values verbatim from the current `$"…"` templates):

```jsonc
// -- upgrade templates (generators) + word tokens -----------------------------
"upgrade-tpl.keep-tool.name": "Keep {{tier}} {{tool}}",
"upgrade-tpl.keep-tool.desc": "Start each loop with your {{tool}} at the {{tier}} tier.",
"upgrade-tpl.keep-skill.name": "Keep {{skill}} Level {{level}}",
"upgrade-tpl.keep-skill.desc": "Start each loop at {{skill}} Level {{level}}. XP is set to the level threshold — no half-progress preserved.",
"upgrade-tpl.keep-skill.desc-profession": "Start each loop at {{skill}} Level {{level}}. XP is set to the level threshold — no half-progress preserved. Re-triggers the profession picker for Level {{level}}.",
"upgrade-tpl.keep-elevator.name": "Keep Mine Elevator Floor {{floor}}",
"upgrade-tpl.keep-elevator.desc": "Start each loop with the mine elevator accessible to floor {{floor}}.",
"upgrade-tpl.keep-mastery.name": "Keep Mastery {{level}}",
"upgrade-tpl.keep-mastery.desc": "Start each loop at Mastery Level {{level}}. Persists across loops once kept.",
"upgrade-tpl.keep-rod.desc": "Start each loop with your Fishing Rod at this tier.",
"upgrade.keep_fishing_rod_0.name": "Keep Bamboo Pole",
"upgrade.keep_fishing_rod_1.name": "Keep Fiberglass Rod",
"upgrade.keep_fishing_rod_2.name": "Keep Iridium Rod",
"tier.1": "Copper", "tier.2": "Steel", "tier.3": "Gold", "tier.4": "Iridium",
"tool.hoe": "Hoe", "tool.pickaxe": "Pickaxe", "tool.axe": "Axe", "tool.watering_can": "Watering Can",
"skill.farming": "Farming", "skill.mining": "Mining", "skill.foraging": "Foraging",
"skill.fishing": "Fishing", "skill.combat": "Combat"
```

**Verify tier/tool key values against the actual `TierNames`/`ToolKinds` arrays at the top of `UpgradeCatalogGenerators.cs` (lines 1-29, not shown here) — copy verbatim from those arrays, then delete the arrays' display-name halves if now unused.**

- [ ] **Step 3: Run full suite.** The Task 3 completeness test now covers all generated rows too (it iterates `UpgradeCatalog.All`). Spot-check byte-identity: add to `UpgradeCatalogI18nTests`:

```csharp
[Fact]
public void GeneratedRows_KeepByteIdenticalEnglish()
{
    Assert.Equal("Keep Copper Hoe", UpgradeCatalog.TryGet("keep_hoe_1")!.DisplayName);
    Assert.Equal("Start each loop with your Hoe at the Copper tier.", UpgradeCatalog.TryGet("keep_hoe_1")!.Description);
    Assert.Equal("Keep Farming Level 5", UpgradeCatalog.TryGet("keep_farming_level_5")!.DisplayName);
    Assert.EndsWith("Re-triggers the profession picker for Level 5.", UpgradeCatalog.TryGet("keep_farming_level_5")!.Description);
    Assert.Equal("Keep Mine Elevator Floor 120", UpgradeCatalog.TryGet("keep_mine_elevator_120")!.DisplayName);
}
```

- [ ] **Step 4: Bump to 0.11.49, build, commit** (`"v0.11.49: i18n — generator templates to upgrade-tpl.* keys with i18n: lazy tokens"`).

---

### Task 5: Theme / category / modifier display names

**Files:**
- Modify: `src/TheLongestYear.Core/ThemeModifiers.cs`
- Create: `src/TheLongestYear.Core/ThemeDisplay.cs`
- Modify: `src/TheLongestYear/i18n/default.json`
- Modify: every `.ToString()` display site of `Theme` / `UpgradeCategory` (find with `Grep`: `theme.ToString|Theme.ToString|Category.ToString|_theme}` over `src/` — known sites: `UI/JunimoShrineMenu.cs` tab labels, `UI/WeeklyHubMenu.cs` theme cards, `Loop/WeeklyThemeQuestService.cs:83` quest title)
- Test: `tests/TheLongestYear.Tests/ThemeDisplayTests.cs`

**Interfaces:**
- Produces: `ThemeDisplay.Name(Theme)` → `Strings.Get($"theme.{theme.ToString().ToLowerInvariant()}")`; `ThemeDisplay.CategoryName(UpgradeCategory)` → `Strings.Get($"upgrade-category.{category.ToString().ToLowerInvariant()}")`. All display sites call these; parsing/persistence keeps raw `.ToString()`.

- [ ] **Step 1: Failing test**

```csharp
// tests/TheLongestYear.Tests/ThemeDisplayTests.cs
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class ThemeDisplayTests
{
    public ThemeDisplayTests(I18nFixture _) { }

    [Fact]
    public void EveryTheme_HasDisplayName()
    {
        foreach (Theme t in System.Enum.GetValues<Theme>())
            Assert.False(ThemeDisplay.Name(t).StartsWith("theme."), $"missing theme key for {t}");
    }

    [Fact]
    public void EveryCategory_HasDisplayName()
    {
        foreach (UpgradeCategory c in System.Enum.GetValues<UpgradeCategory>())
            Assert.False(ThemeDisplay.CategoryName(c).StartsWith("upgrade-category."), $"missing category key for {c}");
    }

    [Fact]
    public void EveryModifierId_HasDisplayName()
    {
        foreach (Theme t in System.Enum.GetValues<Theme>())
        {
            var (bonus, liability) = ThemeModifiers.For(t);
            Assert.False(ThemeModifiers.DisplayNameFor(bonus).StartsWith("modifier."), bonus);
            Assert.False(ThemeModifiers.DisplayNameFor(liability).StartsWith("modifier."), liability);
        }
    }

    [Fact]
    public void KnownStrings_ByteIdentical()
    {
        Assert.Equal("Farming", ThemeDisplay.Name(Theme.Farming));
        Assert.Equal("Loadout", ThemeDisplay.CategoryName(UpgradeCategory.Loadout));
        Assert.Equal("Mine entrance closed all week", ThemeModifiers.DisplayNameFor("mines_closed"));
    }
}
```

- [ ] **Step 2: Implement.** `ThemeDisplay.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Localized display names for enums whose raw .ToString() is persisted/parsed —
/// display sites call these; storage keeps the raw enum name.</summary>
public static class ThemeDisplay
{
    public static string Name(Theme theme)
        => Strings.Get($"theme.{theme.ToString().ToLowerInvariant()}");

    public static string CategoryName(UpgradeCategory category)
        => Strings.Get($"upgrade-category.{category.ToString().ToLowerInvariant()}");
}
```

`ThemeModifiers.DisplayNameFor` (`ThemeModifiers.cs:41-66`) becomes a key lookup with id fallback (keeps the defensive raw-id behavior for unmapped ids):

```csharp
public static string DisplayNameFor(string modifierId)
{
    string result = Strings.Get($"modifier.{modifierId}");
    return result == $"modifier.{modifierId}" ? modifierId : result;
}
```

JSON — every switch value verbatim (`ThemeModifiers.cs:43-64`), all 13 ids incl. legacy, plus enums:

```jsonc
// -- themes / categories / modifiers ------------------------------------------
"theme.foraging": "Foraging", "theme.farming": "Farming", "theme.fishing": "Fishing",
"theme.mining": "Mining", "theme.mixed": "Mixed",
"upgrade-category.loadout": "Loadout", "upgrade-category.carryover": "Carryover",
"upgrade-category.efficiency": "Efficiency", "upgrade-category.obtainability": "Obtainability",
"upgrade-category.foresight": "Foresight", "upgrade-category.stash": "Stash",
"upgrade-category.buildings": "Buildings",
"modifier.forage_yield_up": "20% chance to find an extra foraged item",
"modifier.forage_off": "All foraging items removed",
"modifier.crop_growth_up": "20% chance per crop per day to grow an extra day",
"modifier.crop_growth_down": "20% chance per crop per day to grow nothing",
"modifier.fish_bite_up": "Fish bite 30% sooner",
"modifier.fish_bite_down": "Fish bite 30% slower",
"modifier.mine_drops_up": "20% chance for mined resources to drop +1",
"modifier.mines_closed": "Mine entrance closed all week",
"modifier.all_drops_up": "10% chance for any drop to be +1",
"modifier.all_sell_prices_down": "All sell prices cut in half",
"modifier.forage_drops_off": "Foraging disabled (legacy)",
"modifier.mine_drops_off": "Mine drops disabled (legacy)",
"modifier.shop_discount": "Shop prices 15% lower",
"modifier.stamina_drain_up": "Tools drain 30% more stamina"
```

**Verify the `UpgradeCategory` enum member list against `src/TheLongestYear.Core/UpgradeCategory.cs` before writing the JSON — the seven names above come from the survey; copy the enum's actual members.**

- [ ] **Step 3: Update display sites.** Grep as listed in Files; replace e.g. `theme.ToString()` → `ThemeDisplay.Name(theme)` ONLY where the value is drawn/shown, never where it's saved, compared, or parsed (check each site's downstream use before replacing).

- [ ] **Step 4: Full suite + build. Bump to 0.11.50, commit** (`"v0.11.50: i18n — theme/category/modifier display names via ThemeDisplay + modifier.* keys"`).

---

### Task 6: Shrine menus (`JunimoShrineMenu`, `ShrinePreviewMenu`)

**Files:**
- Modify: `src/TheLongestYear/UI/JunimoShrineMenu.cs` (~lines 315, 318, 369, 384)
- Modify: `src/TheLongestYear/UI/ShrinePreviewMenu.cs` (~lines 119, 379-397 + cart/weather headers + weekday names)
- Modify: `src/TheLongestYear/i18n/default.json`

**Interfaces:** Consumes `Strings.Get`. No new surface.

- [ ] **Step 1: Extract every drawn literal.** Pattern — `"Junimo Shrine"` → `Strings.Get("menu.shrine.title")`; tokened: `$"JP: {n}"` → `Strings.Get("menu.shrine.jp", new Dictionary<string, string> { ["jp"] = n.ToString("N0") })` (keep the exact current number formatting — check each site). JSON section (values verbatim from the two files; keys):

```jsonc
// -- shrine menus -------------------------------------------------------------
"menu.shrine.title": "Junimo Shrine",
"menu.shrine.jp": "JP: {{jp}}",
"menu.shrine.owned": "Owned",
"menu.shrine.cost": "Cost: {{cost}} JP",
"menu.shrine.insufficient": "  (insufficient)",
"menu.shrine-preview.title": "Junimo Shrine - Planning",
"menu.shrine-preview.banked": "Junimo Points banked: {{jp}}",
"menu.shrine-preview.planning-note": "Planning view — you spend JP when a loop resets or you win, not here.",
"menu.shrine-preview.nothing-new": "Nothing new to plan for yet — reach further this run!",
"menu.shrine-preview.weather-header": "Weather",
"menu.shrine-preview.cart-away": "Traveling Cart — not in town (next: {{day}})",
"menu.shrine-preview.cart-catalog-header": "Cart Catalog — bundle items today",
"menu.shrine-preview.cart-nothing": "Nothing here for a bundle today."
```

**Open both files and sweep top-to-bottom for EVERY drawn string literal — the survey's line numbers are the anchors, not the full census. Add a key for each; copy values verbatim. Weekday short-names (`ShortDayName`): replace the English array with the game's own localized day names via `StardewValley.Utility` / `Game1.content` lookups if a vanilla source exists at the call site; otherwise add keys `day.sun` … `day.sat` with the current values.** Cart headers with embedded values keep their exact dash/spacing.

- [ ] **Step 2: Build + suite** (menus aren't unit-tested; correctness = compile + Task 13 guard + final PC pass).

- [ ] **Step 3: Bump to 0.11.51, commit** (`"v0.11.51: i18n — shrine + shrine-preview menu labels to menu.shrine*.* keys"`).

---

### Task 7: Hub + goals menus (`WeeklyHubMenu`, `SeasonGoalsMenu`, `WeatherIcons`)

**Files:**
- Modify: `src/TheLongestYear/UI/WeeklyHubMenu.cs` (~463, 570-613, 680, 719 + hover composition ~476)
- Modify: `src/TheLongestYear/UI/SeasonGoalsMenu.cs` (~320-323, 398, 498, 509-513, 554-555)
- Modify: `src/TheLongestYear/UI/WeatherIcons.cs:29`
- Modify: `src/TheLongestYear/i18n/default.json`

- [ ] **Step 1: Extract.** Same sweep rule as Task 6. Known keys + verbatim values:

```jsonc
// -- weekly hub / season goals / weather ---------------------------------------
"menu.hub.pick-theme": "Pick a theme",
"menu.hub.banking-tip": "Banking items for a matching theme week pays 1.5x JP.",
"menu.hub.reroll": "Re-roll Themes",
"menu.hub.reroll-count": "Re-roll ({{count}})",
"menu.hub.no-offer": "(no offer)",
"menu.hub.bonus-week": "Bonus this week (1.5x):",
"menu.hub.weather-header": "Weather",
"menu.hub.day-label": "Day {{day}} - {{weather}}",
"menu.hub.cart-label": "Cart: {{label}}",
"menu.goals.title": "Season Goals — {{season}} (day {{day}})",
"menu.goals.badge-met": "checkpoint met",
"menu.goals.badge-needs-before": "needs {{count}} before {{season}} 1",
"menu.goals.badge-needs-end": "needs {{count}} by run end",
"menu.goals.more": "+{{count}} more",
"menu.goals.bus-repair": "Bus Repair",
"menu.goals.vault": "Vault",
"weather.green-rain": "Green Rain"
```

**Season names in `menu.goals.title` / `badge-needs-before`: pass the game's OWN localized season string as the `{{season}}` token (SDV localizes season names; find the vanilla helper used elsewhere in the file or use `StardewValley.Utility.getSeasonNameFromNumber`-equivalent for 1.6). Do NOT add our own season keys.** The vault hover strings at `SeasonGoalsMenu.cs:320-323` get keys `menu.goals.vault-hover-*` with verbatim values from the file.

- [ ] **Step 2: Build + suite. Bump to 0.11.52, commit** (`"v0.11.52: i18n — hub/goals/weather menu labels"`).

---

### Task 8: Book menus + victory (`CookbookMenu`, `CraftbookMenu`, `VictoryMenu`, `WinSummary`, `Day28CutsceneMenu` hint)

**Files:**
- Modify: `src/TheLongestYear/UI/CookbookMenu.cs` (~183, 204, 267), `src/TheLongestYear/UI/CraftbookMenu.cs` (same lines), `src/TheLongestYear/UI/VictoryMenu.cs:19-20`, `src/TheLongestYear/UI/Day28CutsceneMenu.cs` (`"(click or press A to continue)"`)
- Modify: `src/TheLongestYear.Core/WinSummary.cs:12-15`
- Modify: `src/TheLongestYear/i18n/default.json`
- Test: `tests/TheLongestYear.Tests/` — existing WinSummary tests get `[Collection("i18n")]`

- [ ] **Step 1: Extract.**

```jsonc
// -- book menus / victory / cutscene hint ---------------------------------------
"menu.cookbook.no-new": "No new recipes to add — learn more recipes first.",
"menu.cookbook.choose": "Choose a recipe to bank:",
"menu.cookbook.title": "Cookbook — {{used}} / {{total}} slots",
"menu.craftbook.title": "Craftbook — {{used}} / {{total}} slots",
"menu.cookbook.remove-confirm": "Remove \"{{recipe}}\" from the cookbook?\nThis recipe won't carry over next loop unless you re-add it.",
"menu.victory.restored": "You have restored the Community Center. The valley is saved!",
"menu.victory.continue-hint": "(press A or click to continue)",
"menu.cutscene.continue-hint": "(click or press A to continue)",
"win.loop-line.first": "You restored it on your very first loop!",
"win.loop-line.many": "It took {{count}} loops."
```

**`menu.cookbook.remove-confirm`: copy the actual second sentence verbatim from `CookbookMenu.cs:204` — the value above is from the survey and MUST be checked against source. The craftbook has its own copy of `no-new`/`choose`/`remove-confirm` — reuse the SAME `menu.cookbook.*` keys from both menus (dedupe), UNLESS the wording differs in source (then split into `menu.craftbook.*`).** `WinSummary.LoopLine` branches on `runNumber == 1` between the two `win.loop-line.*` keys.

- [ ] **Step 2: Full suite (WinSummary tests must pass byte-identically) + build. Bump to 0.11.53, commit** (`"v0.11.53: i18n — cookbook/craftbook/victory/win-summary strings"`).

---

### Task 9: Quests + composed objective + shared tags

**Files:**
- Create: `src/TheLongestYear.Core/QualityTags.cs`
- Modify: `src/TheLongestYear/Loop/WeeklyThemeQuestService.cs` (title :83, description, egg table :215-221, `DescribeSlot` :225-246, `RefreshObjective` :137-160)
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs:800-817` (+ the rewrite in `AddIntroQuest` :820+)
- Modify: `src/TheLongestYear/UI/SeasonGoalsMenu.cs:341` (quality switch → `QualityTags`)
- Modify: `src/TheLongestYear/i18n/default.json`
- Test: `tests/TheLongestYear.Tests/QualityTagsTests.cs`

**Interfaces:**
- Produces: `QualityTags.For(int quality)` → `""`/`" (silver)"`/`" (gold)"`/`" (iridium)"` via keys.

- [ ] **Step 1: TDD QualityTags**

```csharp
// tests/TheLongestYear.Tests/QualityTagsTests.cs
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class QualityTagsTests
{
    public QualityTagsTests(I18nFixture _) { }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, " (silver)")]
    [InlineData(2, " (gold)")]
    [InlineData(4, " (iridium)")]
    public void For_MatchesCurrentEnglish(int quality, string expected)
        => Assert.Equal(expected, QualityTags.For(quality));
}
```

```csharp
// src/TheLongestYear.Core/QualityTags.cs
namespace TheLongestYear.Core;

/// <summary>The " (gold)"-style quality suffix, previously duplicated as switch blocks in
/// WeeklyThemeQuestService and SeasonGoalsMenu. Leading space is part of the value so the
/// empty no-quality case composes cleanly.</summary>
public static class QualityTags
{
    public static string For(int quality) => quality switch
    {
        1 => Strings.Get("quality.silver"),
        2 => Strings.Get("quality.gold"),
        4 => Strings.Get("quality.iridium"),
        _ => ""
    };
}
```

- [ ] **Step 2: Extract quest strings.** JSON:

```jsonc
// -- quests / composed fragments / tags -----------------------------------------
"quest.weekly.title": "Weekly Theme: {{theme}}",
"quest.weekly.description": "Bonus: {{bonus}}\nDrawback: {{drawback}}",
"quest.weekly.progress": "Donated {{done}}/{{total}}:",
"quest.weekly.slot": "{{item}}{{color}}{{qty}}{{quality}} - {{bundle}}",
"quest.weekly.qty": " x{{count}}",
"quest.stash.title": "A gift from the Junimos",
"quest.shrine.title": "The Junimo Shrine",
"quality.silver": " (silver)", "quality.gold": " (gold)", "quality.iridium": " (iridium)",
"egg-color.white": " (White)", "egg-color.brown": " (Brown)"
```

**Open `WeeklyThemeQuestService.cs` and `WorldResetService.cs:800-817` and copy the ACTUAL current strings verbatim into these values — the title/description/tip/objective fragments and the stash/shrine quest descriptions (add `quest.stash.desc` / `quest.shrine.desc` with the real text). The `RefreshObjective` tip sentence(s) after the checklist get `quest.weekly.tip` with the verbatim value. The egg-color parentheticals at :215-221: verify exact casing/spacing.** Rewrites: `$"Weekly Theme: {theme}"` → `Strings.Get("quest.weekly.title", new Dictionary<string,string>{ ["theme"] = ThemeDisplay.Name(theme) })`; `DescribeSlot` composes via `quest.weekly.slot` tokens — `item` = `item.DisplayName` (vanilla-localized), `color` = egg-color key or `""`, `qty` = count > 1 ? `Strings.Get("quest.weekly.qty", …)` : `""`, `quality` = `QualityTags.For(q)`, `bundle` = slot.BundleName (already sourced from game data). `[X]`/`[ ]` markers and `string.Join("\n", …)` stay code-side.

- [ ] **Step 3: Full suite (any quest-text tests join the i18n collection) + build. Bump to 0.11.54, commit** (`"v0.11.54: i18n — weekly/stash/shrine quest text, QualityTags helper, egg-color keys"`).

---

### Task 10: HUD messages + question dialogues

**Files:**
- Modify: `src/TheLongestYear/Loop/WeeklyThemeQuestService.cs:200`, `src/TheLongestYear/Loop/RunController.cs:723` and `:246-253`, `src/TheLongestYear/Loop/FestivalTimeFlow.cs:59`, `src/TheLongestYear/Loop/JunimoStashCapPatch.cs:79-80`, `src/TheLongestYear/Loop/CaveChoicePrompt.cs:52-59`, `src/TheLongestYear/Loop/JojaMembershipBlock.cs:38`, `src/TheLongestYear/Loop/MineDropsPatch.cs:155,197`
- Modify: `src/TheLongestYear/i18n/default.json`

- [ ] **Step 1: Extract.** JSON (values verbatim from each site — the two MineDrops variants and the full Joja/win prompts MUST be copied from source):

```jsonc
// -- hud / dialogues -------------------------------------------------------------
"hud.theme-complete": "Weekly theme complete! +{{jp}} JP, drawback lifted.",
"hud.nothing-to-donate": "Nothing left to donate for this theme - drawback lifted.",
"hud.festival-over": "The festival is over.",
"hud.stash-full.one": "Junimo Stash is full! ({{cap}} slot maximum)",
"hud.stash-full.other": "Junimo Stash is full! ({{cap}} slots maximum)",
"dialog.cave.prompt": "The cave waits, familiar and patient. What should it nurture this year?",
"dialog.cave.mushrooms": "Mushrooms",
"dialog.cave.bats": "Fruit bats",
"dialog.cave.later": "(Decide later)",
"dialog.win.prompt": "The Junimos sing! The Community Center is restored.\n… Do you want to begin a new loop now, or keep playing this run?",
"dialog.win.new-loop": "Start a new loop",
"dialog.win.keep-playing": "Keep playing this run",
"dialog.joja": "The Junimos shake their heads. Something about \"a debt to the land\"…",
"dialog.mines.uneasy-1": "The mines feel uneasy this week…",
"dialog.mines.uneasy-2": "The mines feel uneasy this week…"
```

(`hud.festival-over` already exists from Task 2 — don't duplicate.) The stash plural: `Game1.showRedMessage(Strings.Get(cap == 1 ? "hud.stash-full.one" : "hud.stash-full.other", new Dictionary<string,string>{ ["cap"] = cap.ToString() }))` — the inline `slot{(cap==1?"":"s")}` dies. `Response` second args (the labels) translate; first-arg keys (`"tly_cave_mushrooms"`, `"newLoop"`…) do not.

- [ ] **Step 2: Build + suite. Bump to 0.11.55, commit** (`"v0.11.55: i18n — HUD messages and question dialogues (incl. explicit plural keys)"`).

---

### Task 11: Mail + furniture names + locale-change invalidation

**Files:**
- Modify: `src/TheLongestYear/Loop/OnboardingMailService.cs:35-46`
- Modify: `src/TheLongestYear/Integration/BookFurniture.cs:109-111`
- Modify: `src/TheLongestYear/UI/PlanningShrineService.cs:47`
- Modify: `src/TheLongestYear/ModEntry.cs` (subscribe `LocaleChanged`)
- Modify: `src/TheLongestYear/i18n/default.json`

- [ ] **Step 1: Extract.** Mail: the whole letter body (verbatim, `^` breaks and `[#]` separator INSIDE the value) → `"mail.intro.body"`; title → `"mail.intro.title": "The Longest Year"`. Furniture: only the display-name segment of each `Data/Furniture` format string becomes a key (`furniture.cookbook` = `"The Longest Year Cookbook"`, `furniture.craftbook`, `furniture.bundle-log`, `furniture.planning-shrine` — check `PlanningShrineService.cs:47` for whether "Planning Shrine" and "Junimo Planning Shrine" are two distinct strings → two keys). The mechanical id/rows around them stay hardcoded.

- [ ] **Step 2: LocaleChanged.** In `ModEntry.Entry` (next to the other event subscriptions):

```csharp
this.Helper.Events.Content.LocaleChanged += (_, _) =>
{
    this.Helper.GameContent.InvalidateCache("Data/Mail");
    this.Helper.GameContent.InvalidateCache("Data/Furniture");
};
```

- [ ] **Step 3: Build + suite. Bump to 0.11.56, commit** (`"v0.11.56: i18n — onboarding mail, furniture display names, LocaleChanged asset invalidation"`).

---

### Task 12: GMCM + intro event + Day-28 cutscene

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs:1070-1091` (GMCM lambdas)
- Modify: `src/TheLongestYear/Integration/IntroEventInjector.cs:85-151`
- Modify: `src/TheLongestYear.Core/Day28/Day28CutsceneContent.cs`
- Modify: `src/TheLongestYear/i18n/default.json`
- Test: existing Day28 script tests join `[Collection("i18n")]`

- [ ] **Step 1: GMCM.** The lambdas are already `Func<string>` — swap bodies: `name: () => "Enabled"` → `name: () => Strings.Get("gmcm.enabled.name")`. Keys/values (verbatim from `ModEntry.cs:1070-1091`):

```jsonc
// -- gmcm -------------------------------------------------------------------------
"gmcm.section": "The Longest Year",
"gmcm.master-blurb": "Master switch. When off, TLY skips all setup at save load and no effects fire. Toggling takes effect on the next save load.",
"gmcm.enabled.name": "Enabled",
"gmcm.jp-hud.name": "Show JP HUD",
"gmcm.jp-hud.tooltip": "Always-on corner counter showing banked JP and the current week's theme.",
"gmcm.auto-detect.name": "Auto-detect mod unlock cutscenes",
"gmcm.auto-detect.tooltip": "Re-fire any mod cutscene that grants a recipe / mail flag / quest each loop, so wiped mod unlocks (e.g. SVE's guild) can be regained. Off = only vanilla furnace/cave scenes replay. Takes effect on next save load."
```

(Concatenated multi-line literals join into one JSON value with the exact spaces they already produce.)

- [ ] **Step 2: Intro event.** In the command array, ONLY the `speak` payloads change. Pattern:

```csharp
// BEFORE (one element of the array)
"speak Lewis \"Ah, @! Good, you made it...\"",
// AFTER
$"speak Lewis \"{Strings.Get("event.intro.lewis-1")}\"",
```

Number the keys in array order: `event.intro.lewis-1` … `-8`, `event.intro.junimo-1` … `-13` (adjust counts to the actual file). Every other element (`warp`, `pause`, `viewport`, `addTemporaryActor`, `playSound`, `end`…) is untouched. **If any speak payload itself contains a `"` escape, keep it escaped in the JSON value.**

- [ ] **Step 3: Day-28.** `const` → lazy properties, callers unaffected:

```csharp
public static string FailDialogue => Strings.Get("cutscene.day28.fail");
public static string ContinueDialogue => Strings.Get("cutscene.day28.continue");
```

```jsonc
// -- day-1 intro event / day-28 cutscene (preserve @, #$b#, $h) --------------------
"cutscene.day28.fail": "At this pace we won't be able to restore the Community Center in time, @.#$b#So we will use our magic to rewind the year — but don't worry. We have enough power left over to give you a head-start this time.$h",
"cutscene.day28.continue": "Great job, @ — you're doing well!#$b#Keep this up and we'll save the valley together. We'll gain even more power from the work you do this season.$h"
```

- [ ] **Step 4: Full suite + build. Bump to 0.11.57, commit** (`"v0.11.57: i18n — GMCM, intro event speak lines, day-28 cutscene keys"`).

---

### Task 13: Guard tests

**Files:**
- Create: `tests/TheLongestYear.Tests/I18nGuardTests.cs`

- [ ] **Step 1: Write the guards**

```csharp
// tests/TheLongestYear.Tests/I18nGuardTests.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class I18nGuardTests
{
    private readonly I18nFixture _fixture;
    public I18nGuardTests(I18nFixture fixture) => _fixture = fixture;

    private static string SrcRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static readonly Regex LiteralKey = new(@"Strings\.Get\(\s*""(?<key>[a-z0-9.\-]+)""", RegexOptions.Compiled);
    private static readonly Regex I18nToken = new(@"""i18n:(?<key>[a-z0-9.\-]+)""", RegexOptions.Compiled);

    private static IEnumerable<string> AllSourceFiles()
        => Directory.EnumerateFiles(SrcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private HashSet<string> ReferencedKeys()
    {
        var keys = new HashSet<string>();
        foreach (string file in AllSourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (Match m in LiteralKey.Matches(text)) keys.Add(m.Groups["key"].Value);
            foreach (Match m in I18nToken.Matches(text)) keys.Add(m.Groups["key"].Value);
        }
        // Dynamically composed families — resolve from the live catalog/enums.
        foreach (var def in UpgradeCatalog.All) { _ = def.DisplayName; _ = def.Description; }
        foreach (var kv in _fixture.Map.Keys.Where(k =>
                 k.StartsWith("upgrade.") || k.StartsWith("upgrade-tpl.") || k.StartsWith("theme.") ||
                 k.StartsWith("upgrade-category.") || k.StartsWith("modifier.") || k.StartsWith("tier.") ||
                 k.StartsWith("tool.") || k.StartsWith("skill.") || k.StartsWith("event.intro.")))
            keys.Add(kv); // covered by iteration tests below / array-order numbering
        return keys;
    }

    [Fact]
    public void EveryLiteralKeyInSource_ExistsInDefaultJson()
    {
        var missing = new List<string>();
        foreach (string file in AllSourceFiles())
            foreach (Match m in LiteralKey.Matches(File.ReadAllText(file)))
                if (!_fixture.Map.ContainsKey(m.Groups["key"].Value))
                    missing.Add($"{m.Groups["key"].Value} ({Path.GetFileName(file)})");
        Assert.True(missing.Count == 0, "Keys referenced but missing from default.json:\n" + string.Join("\n", missing.Distinct()));
    }

    [Fact]
    public void EveryCatalogKey_ExistsInDefaultJson()
    {
        // Lazy resolution returns the key itself when missing — detect that.
        foreach (var def in UpgradeCatalog.All)
        {
            Assert.False(def.DisplayName.StartsWith("upgrade"), $"unresolved name for {def.Id}: {def.DisplayName}");
            Assert.False(def.Description.StartsWith("upgrade"), $"unresolved desc for {def.Id}: {def.Description}");
        }
        foreach (Theme t in Enum.GetValues<Theme>())
        {
            _ = ThemeDisplay.Name(t);
            var (b, l) = ThemeModifiers.For(t);
            Assert.False(ThemeModifiers.DisplayNameFor(b).StartsWith("modifier."));
            Assert.False(ThemeModifiers.DisplayNameFor(l).StartsWith("modifier."));
        }
    }

    [Fact]
    public void NoOrphanKeys_InDefaultJson()
    {
        var referenced = ReferencedKeys();
        var orphans = _fixture.Map.Keys.Where(k => !referenced.Contains(k)).ToList();
        Assert.True(orphans.Count == 0, "Orphan keys in default.json:\n" + string.Join("\n", orphans));
    }

    [Fact]
    public void EveryTokenInValues_LooksSane()
    {
        // {{token}} names must be lowercase word chars — catches typos like {{ count }} or {{Count}}.
        var bad = new List<string>();
        var token = new Regex(@"\{\{(?<n>[^}]*)\}\}");
        foreach (var kv in _fixture.Map)
            foreach (Match m in token.Matches(kv.Value))
                if (!Regex.IsMatch(m.Groups["n"].Value, @"^[a-z][a-z0-9]*$"))
                    bad.Add($"{kv.Key}: '{{{{{m.Groups["n"].Value}}}}}'");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }
}
```

**Note:** `NoOrphanKeys` whitelists the dynamic families rather than proving each member is reachable — the paired `EveryCatalogKey` test proves reachability for the big family; `event.intro.*` completeness is proven by the literal-key scan (each key IS a literal in `IntroEventInjector`), so remove `event.intro.` from the whitelist if the scan already covers it (it should — verify, and tighten the whitelist to the minimum that passes honestly).

- [ ] **Step 2: Run the guards.** Fix every miss they surface (this is the task that catches any string a Task 3-12 sweep dropped). Full suite green.

- [ ] **Step 3: No manifest bump needed IF this task changed only tests — but any source fixes the guards forced DO bump. If sources changed: bump to 0.11.58. Commit** (`"v0.11.58: i18n — guard tests (literal-key scan, catalog resolution, orphan + token checks)"` or `"tests: …"` if truly test-only).

---

### Task 14: Translator docs + final verification

**Files:**
- Create: `docs/TRANSLATING.md`
- Modify: `README.md` (add a Translations section stub — content-identical Nexus update happens at release time per house rules)

- [ ] **Step 1: Write docs/TRANSLATING.md**

```markdown
# Translating The Longest Year

All player-visible text lives in [`src/TheLongestYear/i18n/default.json`](../src/TheLongestYear/i18n/default.json)
(shipped in the mod folder as `i18n/default.json`). To add a language:

1. Copy `default.json` to `<locale>.json` in the same `i18n/` folder — e.g. `zh.json`
   (Chinese), `de.json`, `es.json`, `pt.json`, `fr.json`, `ja.json`, `ko.json`, `ru.json`.
2. Translate the **values only**. Never change the keys.
3. Preserve these EXACTLY as they appear:
   - `{{token}}` placeholders (e.g. `{{count}}`, `{{theme}}`) — the game substitutes
     numbers/names at runtime; translated text can reorder them freely.
   - `@` — replaced with the player's name.
   - `#$b#` — dialogue page break.
   - `$h`, `$s`, `$a` — portrait pose codes.
   - `^` and `[#]` inside the mail letter.
4. Drop the file into the installed mod folder (`Mods/TheLongestYear/i18n/`) and restart.
   SMAPI picks the file matching the game language; any key missing from your file
   falls back to English automatically, so partial translations work fine.

No DLL edits, no rebuilds — a JSON file is the whole translation. If you publish one,
tell us (Nexus DM or GitHub issue) and we'll link it from the mod page.
```

- [ ] **Step 2: Final PC verification pass** (user drives or command bridge `tly_commands.txt`; see memory `tly-unattended-verification-loop.md`): load the test save and eyeball each surface for pixel-identical English — Junimo Shrine, shrine preview, weekly hub, season goals, cookbook/craftbook, quest log (weekly quest objective formatting!), GMCM page, onboarding mail (fresh TLY save), Day-1 intro (fresh save), Day-28 via bridge, a stash-full red message, cave prompt. Any diff from current live text = bug (byte-identical rule).

- [ ] **Step 3: Commit docs** (`"docs: TRANSLATING.md + README translations section (no bump — docs only)"`). Update `TODO.md`: mark pre-0.12 item 4 done.

---

## Self-Review Notes (already applied)

- **Spec coverage:** all 8 rollout steps mapped (spec step 4 "one commit per menu" became Tasks 6-8 grouped by menu pair to keep commits reviewable but small; spec's "menus one commit per menu" relaxed to one commit per task — acceptable since each task is one focused change set. If the executor prefers strict one-menu-per-commit, split Tasks 6-8 commits accordingly.)
- **Placeholder scan:** survey-sourced values that MUST be re-verified against source are marked in **bold** inline ("copy verbatim from source") — they are instructions to copy exact existing content from a named file:line, not placeholders.
- **Type consistency:** `Strings.Get(string, IReadOnlyDictionary<string,string>)` used consistently; `I18nFixture.Map` exposed for guards; `i18n:`-prefixed lazy tokens defined in Task 4 and consumed by the Task 13 regex.
