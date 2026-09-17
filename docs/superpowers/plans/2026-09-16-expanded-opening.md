# Expanded Opening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mod's first-morning porch intro with the vanilla opening (deathbed, cubicle, bus) carrying our words, followed by one arrival event that runs bus stop, farmhouse, Community Center and farm tour, ending on the theme picker.

**Architecture:** The mod stops forcing vanilla's Skip intro. Two `AssetRequested` editors do the narrative work: one rewrites grandpa's deathbed lines and the letter in `Strings/StringsFromCSFiles`, the other replaces the vanilla arrival event `60367/u 0` in `Data/Events/BusStop` with a script assembled by a pure Core builder. The existing intro driver keeps only its "flag seen, open the picker" job. Nothing in this plan reads or changes the board, the darkness, or the obtainability model.

**Tech Stack:** C# (.NET 6), SMAPI 4 (`AssetRequested`, Harmony), vanilla event script language, xUnit, the headless bridge (`tools/bridge.ps1`, `tools/send-smapi-command.ps1`).

**Spec:** `docs/superpowers/specs/2026-09-16-expanded-opening-design.md` (approved 2026-09-16). Decompile for vanilla facts: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley` (`StardewValley.Minigames/GrandpaStory.cs`, `StardewValley.Minigames/Intro.cs`, `StardewValley.Menus/TitleMenu.cs` 1269-1286, `StardewValley/Event.cs` 4637 `beginGame`). Vanilla data as exported: `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\patch export\Data_Events_BusStop.json` and `Strings_StringsFromCSFiles.json`.

## Global Constraints

- Branch `story`. Commit after each task and push straight away. Do NOT touch the `Version` in `src/TheLongestYear/manifest.json` (master owns versions).
- Before every commit: `dotnet test tests/TheLongestYear.Tests` (2622 passing at the start) and `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` (0 errors).
- The test project references `TheLongestYear.Core` only. Anything that must be unit-tested lives in Core; the game project holds SMAPI and Harmony glue.
- No em dashes in code, comments, docs, strings or commit messages. Never `/sdcard/`.
- Every player-facing line goes through the `game-writing` skill and is shown to Jeff verbatim before it lands (Task 2). Spec section 3 spoiler rules: grandpa, the letter and Robin never say keeper, spirit, darkness, Junimo, loop or the hall's heart. Morris is courteous and never threatening. The Junimos never mention grandpa.
- `event.*` i18n values must contain no `"` and no `/` (I18nGuardTests). Every value that goes into a script passes through the `EventText` sanitiser at the call site.
- Live runs: my automated run only, throwaway farms made by `tly_newgame` (delete their save folders afterwards), per `docs/HEADLESS_DRIVING.md`. Label every launch.
- Master dependency: the tour's book lines say each book starts with four free slots (Cookbook and Craftbook rework on master's TODO, released first). Until master is merged in, the lines still say four; the plan does not gate on it.

---

## File map

| File | Responsibility |
|---|---|
| `src/TheLongestYear/Loop/SkipIntroChoicePatch.cs` (modify) | Record the checkbox, pass it through unchanged. |
| `src/TheLongestYear.Core/Intro/OpeningStrings.cs` (create) | The table of vanilla string keys the deathbed and letter editor replaces, each mapped to an i18n key, plus the placeholder check. |
| `src/TheLongestYear/Integration/OpeningStringsEditor.cs` (create) | `AssetRequested` editor for `Strings/StringsFromCSFiles`. |
| `src/TheLongestYear.Core/Intro/OpeningScript.cs` (create) | Pure builder of the arrival event script: tiles, actors, beats, `end beginGame`. Takes a text lookup so tests need no game. |
| `src/TheLongestYear/Integration/OpeningEventInjector.cs` (create) | `AssetRequested` editor for `Data/Events/BusStop`, supplies `EventText`. |
| `src/TheLongestYear/Integration/IntroEventInjector.cs` (modify) | Keep the flag bookkeeping and `ClearIntroState`; delete `BuildIntroEvent`. |
| `src/TheLongestYear.Core/Intro/IntroSequenceDecider.cs` (modify) | `StartIntro` becomes `WaitForOpening`; the driver never starts an event. |
| `src/TheLongestYear/Integration/IntroSequenceDriver.cs` (modify) | Drop the start branch. |
| `src/TheLongestYear/ModEntry.cs` (modify) | Register the two editors; `tly_newgame` gains the vanilla-chain path; `tly_replayintro` re-fires the arrival event in place. |
| `src/TheLongestYear/i18n/default.json` (modify) | New `opening.*` and `event.opening.*` keys; delete `event.intro.*`. |
| `tests/TheLongestYear.Tests/OpeningStringsTests.cs`, `OpeningScriptTests.cs` (create), `IntroSequenceDeciderTests.cs` (modify) | Unit coverage. |
| `tools/farmtype-intro.ps1` (modify) | Headless run of the whole opening, both checkbox states. |
| `docs/HEADLESS_DRIVING.md`, `TODO.md`, `STATUS.md` (modify) | Runbook and state. |

---

### Task 1: Stop forcing the vanilla skip; give `tly_newgame` the vanilla-chain path

**Files:**
- Modify: `src/TheLongestYear/Loop/SkipIntroChoicePatch.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdNewGame`, around lines 1108-1160)

**Interfaces:**
- Produces: `tly_newgame <type> [skipintro] [name]` unchanged in syntax; without `skipintro` it now starts the vanilla `GrandpaStory` minigame instead of the bed shortcut. `SkipIntroChoicePatch.Choice.Pending` still carries the checkbox to `OnSaveLoaded`.

- [ ] **Step 1: Pass the checkbox through.** Replace the prefix body:

```csharp
private static void Prefix(ref bool skipIntro)
{
    if (Enabled == null || !Enabled())
        return;

    Choice.Record(skipIntro);
    Monitor?.Log(
        skipIntro
            ? "SkipIntroChoice: player ticked Skip intro; vanilla skips to bed and the theme picker opens on Spring 1."
            : "SkipIntroChoice: Skip intro left off; the opening plays (deathbed, cubicle, bus, arrival).",
        LogLevel.Info);
    // The value is left as the player set it: the vanilla chain now carries our opening
    // (spec 2026-09-16-expanded-opening-design.md, section 2.1).
}
```

Update the class summary: "Records the character-creation Skip intro checkbox and lets vanilla act on it. Off: GrandpaStory, the bus and the arrival event (replaced by OpeningEventInjector) play. On: vanilla's skip path, which marks 60367 seen and wakes the player in bed; OnSaveLoaded plants the cc-seen flag so the driver opens the picker."

- [ ] **Step 2: `tly_newgame` without `skipintro` runs the vanilla chain.** In `CmdNewGame`, after the farm-type setup and `Loop.SkipIntroChoicePatch.Choice.Record(skipIntro);`, replace the tail with:

```csharp
if (Game1.activeClickableMenu is TitleMenu)
    TitleMenu.subMenu = null;
if (skipIntro)
{
    // Mirrors TitleMenu.createdNewCharacter(true): bed, Spring 1, arrival marked seen.
    Game1.game1.loadForNewGame();
    Game1.saveOnNewDay = true;
    Game1.player.eventsSeen.Add("60367");
    Game1.player.currentLocation = Utility.getHomeOfFarmer(Game1.player);
    Game1.player.Position = new Microsoft.Xna.Framework.Vector2(9f, 9f) * 64f;
    Game1.player.isInBed.Value = true;
    Game1.NewDay(0f);
    Game1.exitActiveMenu();
    Game1.setGameMode(3);
    return;
}
// Mirrors TitleMenu.update's transition for createdNewCharacter(false): the deathbed and
// cubicle minigame, which hands off to the bus ride, which calls loadForNewGame itself
// (GrandpaStory.cs 105/119, Intro.cs 416) and lands the player at the bus stop for 60367.
Game1.currentMinigame = new StardewValley.Minigames.GrandpaStory();
Game1.exitActiveMenu();
Game1.setGameMode(3);
```

- [ ] **Step 3: Build and run the tests.**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` and `dotnet test tests/TheLongestYear.Tests`
Expected: 0 errors; 2622 passing (no test change in this task; `IntroSkipChoiceTests` still cover `Record`/`Consume`).

- [ ] **Step 4: Commit.**

```bash
git add src/TheLongestYear/Loop/SkipIntroChoicePatch.cs src/TheLongestYear/ModEntry.cs
git commit -m "opening: let vanilla act on the Skip intro checkbox; tly_newgame can run the vanilla chain"
git push
```

---

### Task 2: The lines (game-writing skill, Jeff's approval)

**Files:**
- Modify: `src/TheLongestYear/i18n/default.json` (add keys; do not delete `event.intro.*` yet, Task 6 does)

**Interfaces:**
- Produces the i18n keys every later task reads, exactly these names:
  - Deathbed: `opening.grandpa-1-m`, `opening.grandpa-1-f` (the "grandson" / "granddaughter" openers), `opening.grandpa-2` .. `opening.grandpa-5`, `opening.grandpa-6-m`, `opening.grandpa-6-f`, `opening.grandpa-7`. Eight spoken boxes, matching vanilla's queue order 12026/12028, 12029, 12030, 12031, 12034, 12035, 12036/12038, 12040.
  - Letter: `opening.letter-m`, `opening.letter-f`. Must contain `{0}` (player name) and `{1}` (farm name) exactly once each and use `^` for line breaks and `^^` for a blank line, as vanilla does.
  - Arrival, all under `event.opening.`: `robin-1`, `robin-2`, `robin-3` (greeting, grandpa nostalgia, pointing Morris up the road), `morris-bus-1` (asks for the mayor), `robin-walk-1`, `robin-walk-2` (trimmed tour), `lewis-1` (welcome), `morris-farm-1` .. `morris-farm-4` (landmark, option closes Winter 28 unless restored, assigned manager, the file and the open door), `lewis-2` (angry, no money), `farmer-ask` (the player's question, delivered as a `message`), `lewis-3` (he takes you), `lewis-hall-1`, `lewis-hall-2` (beyond saving, the key), `junimo-1` .. `junimo-8` (who they are, the hall, the loop rules: season shares, the unwind, what carries over, the offer, "Spring is yours"), `tour-1` (stash), `tour-2` (statue), `tour-3` (Cookbook, four free slots), `tour-4` (Craftbook, four free slots), `tour-5` (Bundle Log), `tour-6` (each week the town needs something most).
- Consumes: the current `event.intro.junimo-*` lines as the base for `junimo-*` (trim, do not add lore).

- [ ] **Step 1: Invoke the `game-writing` skill** and draft every key above in Jeff's register, under spec section 3. Vanilla's own lines are the reference for shape (deathbed strings `GrandpaStory.cs.12026` to `12040`; letter `12051`; Robin and Lewis in `60367/u 0`).

- [ ] **Step 2: Show Jeff every line verbatim in chat, grouped by scene.** Wait for his yes or edits. Nothing lands without it.

- [ ] **Step 3: Add the approved lines to `default.json`** under a new comment header `// -- the expanded opening (deathbed, letter, arrival event) ------------------`, placed after the `event.intro.*` block.

- [ ] **Step 4: Run the tests.**

Run: `dotnet test tests/TheLongestYear.Tests`
Expected: 2622 passing. `I18nGuardTests` will flag the new keys as unused until Tasks 3 and 5 reference them: if `UnusedKeys` (or the equivalent guard) fails, add the keys to that test's allow list with the comment `// referenced by OpeningStrings / OpeningScript (Tasks 3 and 5 of the 2026-09-16 opening plan)` and remove the allowance in Task 6.

- [ ] **Step 5: Commit.**

```bash
git add src/TheLongestYear/i18n/default.json tests/TheLongestYear.Tests/I18nGuardTests.cs
git commit -m "opening: the lines (deathbed, letter, arrival), approved by Jeff"
git push
```

---

### Task 3: Grandpa's words: `OpeningStrings` (Core) and the string editor (glue)

**Files:**
- Create: `src/TheLongestYear.Core/Intro/OpeningStrings.cs`
- Create: `src/TheLongestYear/Integration/OpeningStringsEditor.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`Entry`, next to the `_onboardingMail` registration around line 212)
- Test: `tests/TheLongestYear.Tests/OpeningStringsTests.cs`

**Interfaces:**
- Produces: `OpeningStrings.Replacements` (`IReadOnlyDictionary<string, string>`, vanilla key to i18n key), `OpeningStrings.Apply(IDictionary<string, string> data, Func<string, string> text)` which overwrites each vanilla key whose i18n text is non-empty and returns how many it replaced, and `OpeningStrings.PlaceholdersMatch(string vanilla, string ours)` which is true when both contain the same set of `{n}` tokens.

- [ ] **Step 1: Write the failing tests.**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core.Intro;
using Xunit;

public class OpeningStringsTests
{
    [Fact]
    public void Every_vanilla_deathbed_and_letter_key_is_mapped()
    {
        string[] vanilla =
        {
            "GrandpaStory.cs.12026", "GrandpaStory.cs.12028", "GrandpaStory.cs.12029", "GrandpaStory.cs.12030",
            "GrandpaStory.cs.12031", "GrandpaStory.cs.12034", "GrandpaStory.cs.12035", "GrandpaStory.cs.12036",
            "GrandpaStory.cs.12038", "GrandpaStory.cs.12040", "GrandpaStory.cs.12051", "GrandpaStory.cs.12055",
        };
        foreach (string key in vanilla)
            Assert.True(OpeningStrings.Replacements.ContainsKey(key), key);
        Assert.Equal(vanilla.Length, OpeningStrings.Replacements.Count);
    }

    [Fact]
    public void Apply_overwrites_mapped_keys_and_leaves_empty_text_alone()
    {
        var data = new Dictionary<string, string>
        {
            ["GrandpaStory.cs.12029"] = "vanilla envelope line",
            ["GrandpaStory.cs.12051"] = "Dear {0}, {1} Farm",
            ["Game1.cs.3689"] = "Loading...",
        };
        int replaced = OpeningStrings.Apply(data, key => key == "opening.grandpa-2" ? "our envelope line" : "");
        Assert.Equal(1, replaced);
        Assert.Equal("our envelope line", data["GrandpaStory.cs.12029"]);
        Assert.Equal("Dear {0}, {1} Farm", data["GrandpaStory.cs.12051"]);   // empty text: untouched
        Assert.Equal("Loading...", data["Game1.cs.3689"]);
    }

    [Fact]
    public void Placeholders_must_match_vanilla()
    {
        Assert.True(OpeningStrings.PlaceholdersMatch("Dear {0}, welcome to {1} Farm", "{1} Farm is yours, {0}"));
        Assert.False(OpeningStrings.PlaceholdersMatch("Dear {0}, welcome to {1} Farm", "Dear {0}"));
        Assert.True(OpeningStrings.PlaceholdersMatch("no tokens", "still none"));
    }
}
```

- [ ] **Step 2: Run the tests to see them fail.**

Run: `dotnet test tests/TheLongestYear.Tests --filter OpeningStringsTests`
Expected: FAIL, `OpeningStrings` does not exist.

- [ ] **Step 3: Write `OpeningStrings`.**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core.Intro;

/// <summary>Grandpa's deathbed speech and the letter, replaced word for word in
/// Strings/StringsFromCSFiles while the vanilla pictures play (spec 2026-09-16-expanded-opening-design.md,
/// section 2.2). Keys are the vanilla string ids GrandpaStory.cs reads (lines 84-91 the speech queue,
/// 312 the letter); values are the mod's i18n keys. The two "-m" / "-f" pairs are the male and
/// female variants vanilla itself has.</summary>
public static class OpeningStrings
{
    private const string VanillaPrefix = "GrandpaStory.cs.";
    private static readonly Regex Placeholder = new(@"\{\d+\}", RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, string> Replacements = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [VanillaPrefix + "12026"] = "opening.grandpa-1-m",
        [VanillaPrefix + "12028"] = "opening.grandpa-1-f",
        [VanillaPrefix + "12029"] = "opening.grandpa-2",
        [VanillaPrefix + "12030"] = "opening.grandpa-3",
        [VanillaPrefix + "12031"] = "opening.grandpa-4",
        [VanillaPrefix + "12034"] = "opening.grandpa-5",
        [VanillaPrefix + "12035"] = "opening.grandpa-6",
        [VanillaPrefix + "12036"] = "opening.grandpa-7-m",
        [VanillaPrefix + "12038"] = "opening.grandpa-7-f",
        [VanillaPrefix + "12040"] = "opening.grandpa-8",
        [VanillaPrefix + "12051"] = "opening.letter-m",
        [VanillaPrefix + "12055"] = "opening.letter-f",
    };

    /// <summary>Overwrites each mapped vanilla key with the mod's text when that text is non-empty
    /// (an empty i18n value leaves vanilla's line in place rather than blanking the scene).</summary>
    public static int Apply(IDictionary<string, string> data, Func<string, string> text)
    {
        int replaced = 0;
        foreach ((string vanillaKey, string ourKey) in Replacements)
        {
            string value = text(ourKey);
            if (string.IsNullOrEmpty(value)) continue;
            data[vanillaKey] = value;
            replaced++;
        }
        return replaced;
    }

    /// <summary>True when both strings use the same set of {n} tokens (the letter needs {0} and {1}).</summary>
    public static bool PlaceholdersMatch(string vanilla, string ours)
    {
        var a = Placeholder.Matches(vanilla).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        var b = Placeholder.Matches(ours).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        return a.SetEquals(b);
    }
}
```

Note the key names: the spec table in Task 2 lists eight spoken boxes; the mapping above names them `grandpa-1` (two variants), `grandpa-2` .. `grandpa-6`, `grandpa-7` (two variants), `grandpa-8`. Task 2's writer uses these exact names (the Task 2 list is superseded by this table where they differ: `-6-m/-f` in Task 2 is `-7-m/-f` here and the last line is `-8`).

- [ ] **Step 4: Run the tests to see them pass.**

Run: `dotnet test tests/TheLongestYear.Tests --filter OpeningStringsTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Write the editor (glue).**

```csharp
using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Integration
{
    /// <summary>Rewrites grandpa's deathbed speech and the letter for the opening. Active whenever the
    /// mod is enabled: while it is, every new farm is a Longest Year farm, and these strings are only
    /// read by the new-game minigame (GrandpaStory.cs), so a loaded vanilla save never sees them.</summary>
    internal sealed class OpeningStringsEditor
    {
        private const string AssetName = "Strings/StringsFromCSFiles";
        private readonly IMonitor _monitor;
        private readonly Func<bool> _enabled;

        public OpeningStringsEditor(IMonitor monitor, Func<bool> enabled)
        {
            _monitor = monitor;
            _enabled = enabled;
        }

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!_enabled() || !e.NameWithoutLocale.IsEquivalentTo(AssetName)) return;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                int replaced = OpeningStrings.Apply(data, Strings.Get);
                _monitor.Log($"Opening: replaced {replaced} of {OpeningStrings.Replacements.Count} deathbed and letter strings.", LogLevel.Trace);
            }, AssetEditPriority.Default);
        }
    }
}
```

- [ ] **Step 6: Register it in `Entry`** next to the onboarding mail registration:

```csharp
// The opening's deathbed and letter text (spec 2026-09-16). Hooked at Entry so the very first
// Strings/StringsFromCSFiles load already carries it; the minigame reads it before any save exists.
_openingStrings = new OpeningStringsEditor(this.Monitor, () => _config.Enabled);
helper.Events.Content.AssetRequested += _openingStrings.OnAssetRequested;
```

with the field `private OpeningStringsEditor _openingStrings;` beside `_introInjector`.

- [ ] **Step 7: Add the letter placeholder guard to `OpeningStringsTests`.** The i18n map is available to tests through `I18nGuardTests`' fixture; add to that file's class (or a new fact in `OpeningStringsTests` that loads `src/TheLongestYear/i18n/default.json` the same way `I18nGuardTests` does):

```csharp
[Fact]
public void The_letter_keeps_vanillas_name_and_farm_tokens()
{
    foreach (string key in new[] { "opening.letter-m", "opening.letter-f" })
        Assert.True(OpeningStrings.PlaceholdersMatch("Dear {0}, {1} Farm", _fixture.Map[key]), key);
}
```

- [ ] **Step 8: Build, test, commit.**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` and `dotnet test tests/TheLongestYear.Tests`
Expected: 0 errors; 2626 passing.

```bash
git add src/TheLongestYear.Core/Intro/OpeningStrings.cs src/TheLongestYear/Integration/OpeningStringsEditor.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/OpeningStringsTests.cs tests/TheLongestYear.Tests/I18nGuardTests.cs
git commit -m "opening: grandpa's deathbed lines and the letter carry the mod's words"
git push
```

---

### Task 4: The arrival script: `OpeningScript` (Core)

**Files:**
- Create: `src/TheLongestYear.Core/Intro/OpeningScript.cs`
- Test: `tests/TheLongestYear.Tests/OpeningScriptTests.cs`

**Interfaces:**
- Produces: `OpeningScript.Build(Func<string, string> text, string ccSeenMail)` returning the `/`-joined event value (no key); `OpeningScript.VanillaKey = "60367/u 0"`; `OpeningScript.LocationOrder = { "BusStop", "Farm", "CommunityCenter", "Farm" }` for the test and the runbook.
- Consumes: `IntroEventKeys.CcSeenMail`; the `event.opening.*` keys from Task 2.

Tiles (all vanilla or already proven by the current intro and the ending):
- Bus stop: vanilla `60367` (farmer 22 10, Robin 22 13, viewport 23 10). Morris steps off two tiles behind the farmer: `addTemporaryActor Morris 16 32 22 8 2 true Character` after the bus door sound, then `move Morris 0 1 2`.
- Farm walk: vanilla (`warp Robin 78 17`, `warp farmer 79 17`, both `move -8 0 3`, then `-7 0 0`). Morris follows one tile behind: `warp Morris 80 17` then the same moves with a `pause 200` lead.
- Farmhouse door: vanilla `warp Lewis 64 15`, then his three moves. Standard-farm tiles; the game offsets Farm events by the farmhouse position (Farm.ResetForEvent), which is why the current intro's porch scene lands on Meadowlands.
- Morris leaves east along row 17: `move Morris 12 0 1 true`, then `warp Morris -100 -100` so he is off screen before the Community Center.
- Community Center: the current intro's tiles (`warp farmer 32 16 true`, Junimo at 32 11, `viewport 32 14 true`); Lewis at 30 16 for his two lines, then `warp Lewis -100 -100`.
- Farm tour: the current intro's porch tiles (`warp farmer 66 18 true`, `viewport 66 18 true`). The stash chest is placed at the farmhouse door plus (3, 2), which on the Standard farm is (67, 17), and the statue five tiles left of it at (62, 17) (JunimoStashService.PlaceChest, PlanningShrineService.Place). Two Junimo actors: `addTemporaryActor Junimo 16 16 67 18 0 false character Junimo` by the chest, `addTemporaryActor Junimo 16 16 62 18 0 false character Junimo2` by the statue. The speaker is `Junimo`; `Junimo2` only hops (`jump Junimo2`) when the statue is named.

- [ ] **Step 1: Write the failing tests.**

```csharp
using System;
using System.Linq;
using TheLongestYear.Core.Intro;
using Xunit;

public class OpeningScriptTests
{
    private static string Text(string key) => $"[{key}]";

    [Fact]
    public void The_script_visits_bus_stop_farm_hall_and_farm_in_that_order_and_ends_in_bed()
    {
        string[] commands = OpeningScript.Build(Text, "tly_intro_cc_seen").Split('/');
        var locations = commands.Where(c => c.StartsWith("changeLocation ", StringComparison.Ordinal))
            .Select(c => c.Substring("changeLocation ".Length)).ToArray();
        Assert.Equal(new[] { "Farm", "CommunityCenter", "Farm" }, locations);   // BusStop is where 60367 starts
        Assert.Equal("end beginGame", commands[^1]);
        Assert.Equal("addMailReceived tly_intro_cc_seen", commands[^2]);
    }

    [Fact]
    public void Every_line_key_is_spoken_once_and_wrapped_in_quotes()
    {
        string script = OpeningScript.Build(Text, "flag");
        foreach (string key in OpeningScript.LineKeys)
            Assert.Equal(1, CountOf(script, $"\"[{key}]\""));
    }

    [Fact]
    public void Morris_is_gone_before_the_hall_and_lewis_is_gone_before_the_tour()
    {
        string[] commands = OpeningScript.Build(Text, "flag").Split('/');
        int hall = Array.IndexOf(commands, "changeLocation CommunityCenter");
        int tour = Array.LastIndexOf(commands, "changeLocation Farm");
        Assert.Contains(commands.Take(hall), c => c == "warp Morris -100 -100");
        Assert.DoesNotContain(commands.Skip(hall), c => c.Contains("Morris"));
        Assert.Contains(commands.Skip(hall).Take(tour - hall), c => c == "warp Lewis -100 -100");
        Assert.DoesNotContain(commands.Skip(tour), c => c.Contains("Lewis"));
    }

    [Fact]
    public void The_script_never_names_the_dark_morris_sprite_and_is_not_skippable()
    {
        string script = OpeningScript.Build(Text, "flag");
        Assert.DoesNotContain("Morris_Dark", script);
        Assert.DoesNotContain("changeSprite", script);
        Assert.DoesNotContain("/skippable/", script);
    }

    private static int CountOf(string haystack, string needle)
        => (haystack.Length - haystack.Replace(needle, "").Length) / needle.Length;
}
```

- [ ] **Step 2: Run the tests to see them fail.**

Run: `dotnet test tests/TheLongestYear.Tests --filter OpeningScriptTests`
Expected: FAIL, `OpeningScript` does not exist.

- [ ] **Step 3: Write `OpeningScript`.**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Intro;

/// <summary>The arrival event (spec 2026-09-16-expanded-opening-design.md, sections 1 and 2.3): one
/// script under vanilla's own key that carries itself from the bus stop through the farmhouse, the
/// Community Center and back to the porch, and ends the way vanilla's arrival ends (end beginGame:
/// Event.cs 4637 puts the farmer to bed and starts Spring 1). Pure: <paramref name="text"/> supplies
/// every spoken line already sanitised for the script (no '"' or '/').</summary>
public static class OpeningScript
{
    public const string VanillaKey = "60367/u 0";
    public static readonly string[] LocationOrder = { "BusStop", "Farm", "CommunityCenter", "Farm" };
    private const string Off = "-100 -100";
    private const string Prefix = "event.opening.";

    /// <summary>Every line key the script speaks, in order.</summary>
    public static readonly string[] LineKeys =
    {
        Prefix + "robin-1", Prefix + "robin-2", Prefix + "morris-bus-1", Prefix + "robin-3",
        Prefix + "robin-walk-1", Prefix + "robin-walk-2",
        Prefix + "lewis-1", Prefix + "morris-farm-1", Prefix + "morris-farm-2", Prefix + "morris-farm-3",
        Prefix + "lewis-2", Prefix + "morris-farm-4", Prefix + "farmer-ask", Prefix + "lewis-3",
        Prefix + "lewis-hall-1", Prefix + "lewis-hall-2",
        Prefix + "junimo-1", Prefix + "junimo-2", Prefix + "junimo-3", Prefix + "junimo-4",
        Prefix + "junimo-5", Prefix + "junimo-6", Prefix + "junimo-7", Prefix + "junimo-8",
        Prefix + "tour-1", Prefix + "tour-2", Prefix + "tour-3", Prefix + "tour-4", Prefix + "tour-5", Prefix + "tour-6",
    };

    public static string Build(Func<string, string> text, string ccSeenMail)
    {
        string Say(string who, string key) => $"speak {who} \"{text(Prefix + key)}\"";
        string Note(string key) => $"message \"{text(Prefix + key)}\"";   // the farmer's own line, no portrait

        var s = new List<string>
        {
            // ---- Bus stop (vanilla 60367 opening, Morris added) ----
            "none",
            "-1000 -1000",
            "farmer 22 10 2 Robin 22 13 0 Lewis -100 -100 2",
            "pause 500",
            "playSound busDoorOpen",
            "pause 5000",
            "viewport 23 10 clamp true",
            "move farmer 0 2 2",
            "playMusic SettlingIn",
            Say("Robin", "robin-1"),
            "pause 300",
            Say("Robin", "robin-2"),
            "pause 400",
            "playSound busDoorOpen",
            "addTemporaryActor Morris 16 32 22 8 2 true Character",
            "move Morris 0 1 2",
            "pause 400",
            "faceDirection Robin 0",
            Say("Morris", "morris-bus-1"),
            "pause 300",
            "faceDirection Robin 2",
            Say("Robin", "robin-3"),
            "pause 400",
            "viewport move 0 2 800",
            "move Robin 0 5 2 true",
            "pause 800",
            "move farmer 0 4 2 true",
            "fade",
            "speed farmer 2",
            "viewport -200 -200",

            // ---- Farm: the walk (vanilla tiles, Morris one tile behind) ----
            "changeLocation Farm",
            "halt",
            "warp Robin 78 17",
            "faceDirection Robin 3",
            "warp farmer 79 17",
            "faceDirection farmer 3",
            "warp Morris 80 17",
            "faceDirection Morris 3",
            "viewport 70 16 clamp",
            "viewport move -1 0 4000",
            "move Robin -8 0 3 farmer -8 0 3 Morris -8 0 3",
            "pause 700",
            "faceDirection Robin 2",
            Say("Robin", "robin-walk-1"),
            "pause 500",
            "move Robin -7 0 0 farmer -7 0 0 Morris -7 0 0",
            "pause 400",
            "faceDirection Robin 0",
            Say("Robin", "robin-walk-2"),
            "pause 300",
            "faceDirection farmer 0",
            "pause 500",

            // ---- Farmhouse door: Lewis, then Morris's business ----
            "playSound doorClose",
            "warp Lewis 64 15",
            "pause 1500",
            "move Lewis 0 1 2",
            "move Lewis 1 0 2",
            "move Lewis 0 1 3",
            "faceDirection farmer 1",
            "faceDirection Robin 1",
            "pause 600",
            Say("Lewis", "lewis-1"),
            "pause 400",
            "faceDirection Lewis 1",
            "faceDirection Morris 3",
            Say("Morris", "morris-farm-1"),
            "pause 200",
            Say("Morris", "morris-farm-2"),
            "pause 200",
            Say("Morris", "morris-farm-3"),
            "pause 400",
            "jump Lewis",
            Say("Lewis", "lewis-2"),
            "pause 500",
            "faceDirection Morris 2",
            "faceDirection farmer 1",
            Say("Morris", "morris-farm-4"),
            "pause 400",
            "move Morris 12 0 1 true",
            "pause 1500",
            $"warp Morris {Off}",
            "faceDirection farmer 3",
            "faceDirection Lewis 1",
            "pause 600",
            Note("farmer-ask"),
            "pause 300",
            Say("Lewis", "lewis-3"),
            "pause 600",
            "globalFade",
            $"viewport {Off}",

            // ---- Community Center: Lewis's piece, then the Junimos ----
            "changeLocation CommunityCenter",
            "warp farmer 32 16 true",
            "warp Lewis 30 16",
            "faceDirection Lewis 1",
            "faceDirection farmer 3",
            "viewport 32 14 true",
            "pause 800",
            Say("Lewis", "lewis-hall-1"),
            "playSound coin",
            "pause 300",
            Say("Lewis", "lewis-hall-2"),
            "pause 600",
            "playSound doorClose",
            $"warp Lewis {Off}",
            "faceDirection farmer 0",
            "pause 800",
            "addTemporaryActor Junimo 16 16 32 11 2 false character Junimo",
            "playSound junimoMeep1",
            "pause 400",
            Say("Junimo", "junimo-1"),
            "pause 200",
            Say("Junimo", "junimo-2"),
            "pause 200",
            Say("Junimo", "junimo-3"),
            "pause 300",
            Say("Junimo", "junimo-4"),
            "pause 300",
            Say("Junimo", "junimo-5"),
            "pause 300",
            Say("Junimo", "junimo-6"),
            "pause 300",
            Say("Junimo", "junimo-7"),
            "pause 600",
            Say("Junimo", "junimo-8"),
            "pause 600",
            "playSound junimoMeep1",
            "globalFade",
            $"viewport {Off}",

            // ---- Farm again: the tour on the porch (Standard-farm tiles, offset per farm type) ----
            "changeLocation Farm",
            "warp farmer 66 18 true",
            "faceDirection farmer 1",
            "addTemporaryActor Junimo 16 16 67 18 0 false character Junimo",
            "addTemporaryActor Junimo 16 16 62 18 0 false character Junimo2",
            "viewport 66 18 true",
            "pause 800",
            "playSound junimoMeep1",
            "jump Junimo",
            Say("Junimo", "tour-1"),
            "pause 300",
            "faceDirection farmer 3",
            "jump Junimo2",
            Say("Junimo", "tour-2"),
            "pause 300",
            "faceDirection farmer 1",
            "playSound coin",
            Say("Junimo", "tour-3"),
            "pause 200",
            "playSound coin",
            Say("Junimo", "tour-4"),
            "pause 200",
            "playSound coin",
            Say("Junimo", "tour-5"),
            "pause 400",
            Say("Junimo", "tour-6"),
            "pause 600",
            "playSound junimoMeep1",
            "pause 800",
            "globalFade",
            $"viewport {Off}",
            "playMusic none",
            "pause 1500",
            "playSound rooster",
            "pause 800",
            $"addMailReceived {ccSeenMail}",
            "end beginGame",
        };
        return string.Join("/", s);
    }
}
```

Two things the implementer must keep: `move A dx dy dir B dx dy dir` moves several actors at once (vanilla uses it for Robin and the farmer), and `message` is the vanilla command for a line with no speaker portrait, used here for the farmer's own question.

- [ ] **Step 4: Run the tests to see them pass.**

Run: `dotnet test tests/TheLongestYear.Tests --filter OpeningScriptTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit.**

```bash
git add src/TheLongestYear.Core/Intro/OpeningScript.cs tests/TheLongestYear.Tests/OpeningScriptTests.cs
git commit -m "opening: the arrival script, bus stop to farm tour, ends in bed"
git push
```

---

### Task 5: The event editor (glue) and the driver that only opens the picker

**Files:**
- Create: `src/TheLongestYear/Integration/OpeningEventInjector.cs`
- Modify: `src/TheLongestYear.Core/Intro/IntroSequenceDecider.cs`
- Modify: `src/TheLongestYear/Integration/IntroSequenceDriver.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`Entry`; `CmdReplayIntro`)
- Modify: `src/TheLongestYear/Integration/IntroEventInjector.cs` (`ClearIntroState`)
- Test: `tests/TheLongestYear.Tests/IntroSequenceDeciderTests.cs`

**Interfaces:**
- Produces: `IntroAction.WaitForOpening` (replaces `StartIntro`); `OpeningEventInjector.OnAssetRequested`; `IntroEventInjector.ClearIntroState()` now also removes `60367` from `eventsSeen`.
- Consumes: `OpeningScript.Build`, `OpeningScript.VanillaKey`, `IntroEventKeys.CcSeenMail`.

- [ ] **Step 1: Update the decider tests.** Replace `Fresh_no_flags_starts_intro`:

```csharp
[Fact]
public void Fresh_no_flags_waits_for_the_opening_to_plant_the_flag()
    => Assert.Equal(IntroAction.WaitForOpening, IntroSequenceDecider.Next(Fresh()));
```

- [ ] **Step 2: Run the tests to see them fail.**

Run: `dotnet test tests/TheLongestYear.Tests --filter IntroSequenceDeciderTests`
Expected: FAIL, `WaitForOpening` not defined.

- [ ] **Step 3: Change the decider.**

```csharp
public enum IntroAction
{
    None,            // not a fresh-intro context
    Waiting,         // an event is playing (or just ended)
    WaitForOpening,  // fresh morning, flag not planted yet: the arrival event (or the skip) will plant it
    OpenPicker       // flag present: open the theme picker
}
```

and in `Next`: `if (!s.CcSeen) return IntroAction.WaitForOpening;`. Update the class summary: "The opening is vanilla's arrival event, replaced by OpeningEventInjector; it ends by adding the cc-seen flag, as does the Skip intro path. The driver never starts an event; it waits for the flag and opens the picker."

- [ ] **Step 4: Run the tests to see them pass.**

Run: `dotnet test tests/TheLongestYear.Tests --filter IntroSequenceDeciderTests`
Expected: PASS.

- [ ] **Step 5: Shrink the driver.** In `IntroSequenceDriver.OnUpdateTicked`, delete the whole `case IntroAction.StartIntro:` block and the `_introStartedThisMorning` field and its reset; add `case IntroAction.WaitForOpening:` to the no-op group. Replace the class summary with: "Opens the theme picker on the first morning of a fresh run once the cc-seen flag is present. The flag is planted by the arrival event's end (OpeningEventInjector) or by OnSaveLoaded when Skip intro was ticked. This class never starts an event."

- [ ] **Step 6: Write the event editor.**

```csharp
using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Integration
{
    /// <summary>Replaces vanilla's arrival event (60367 in Data/Events/BusStop) with the opening's
    /// script while the mod is enabled. Keeping vanilla's key keeps every vanilla behaviour around it:
    /// Skip intro marks it seen, FarmerReset marks it seen after a rewind, and "end beginGame" puts the
    /// farmer to bed for Spring 1 exactly as vanilla does.</summary>
    internal sealed class OpeningEventInjector
    {
        internal const string AssetName = "Data/Events/BusStop";
        private readonly IMonitor _monitor;
        private readonly Func<bool> _enabled;

        public OpeningEventInjector(IMonitor monitor, Func<bool> enabled)
        {
            _monitor = monitor;
            _enabled = enabled;
        }

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!_enabled() || !e.NameWithoutLocale.IsEquivalentTo(AssetName)) return;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                if (!data.ContainsKey(OpeningScript.VanillaKey))
                    _monitor.Log($"Opening: {AssetName} has no '{OpeningScript.VanillaKey}' entry (another mod replaced it?); adding ours.", LogLevel.Warn);
                data[OpeningScript.VanillaKey] = OpeningScript.Build(EventText, IntroEventKeys.CcSeenMail);
            }, AssetEditPriority.Late);
        }

        /// <summary>Same sanitiser as the ending: a translated '"' or '/' would break the script.</summary>
        private static string EventText(string key)
        {
            string value = Strings.Get(key);
            return string.IsNullOrEmpty(value) ? value : value.Replace('"', '\'').Replace('/', ',');
        }
    }
}
```

`I18nGuardTests` finds keys through `EventText("literal")` call sites; `OpeningScript` builds its keys from `Prefix + "robin-1"`, so add a regex to that test beside `SeasonTurnKey`:

```csharp
/// <summary>OpeningScript.LineKeys builds its keys as <c>Prefix + "robin-1"</c>; the prefix is "event.opening.".</summary>
private static readonly Regex OpeningKey = new(@"Prefix\s*\+\s*""(?<key>[a-z0-9\-]+)""", RegexOptions.Compiled);
```

and include its matches (prefixed with `event.opening.`) wherever `SeasonTurnKey` matches are collected. Then remove the Task 2 allow-list entries for `event.opening.*`.

- [ ] **Step 7: Register the editor in `Entry`** beside the strings editor:

```csharp
_openingEvent = new OpeningEventInjector(this.Monitor, () => _config.Enabled);
helper.Events.Content.AssetRequested += _openingEvent.OnAssetRequested;
```

- [ ] **Step 8: `tly_replayintro` re-fires the arrival in place.** `IntroEventInjector.ClearIntroState` also removes the vanilla id:

```csharp
Game1.player.eventsSeen.Remove("60367");
```

and `CmdReplayIntro` warps to the bus stop so the event's own precondition fires it:

```csharp
private void CmdReplayIntro(string command, string[] args)
{
    if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
    _introInjector?.ClearIntroState();
    this.Helper.GameContent.InvalidateCache(Integration.OpeningEventInjector.AssetName);
    Game1.warpFarmer("BusStop", 22, 11, false);
    this.Monitor.Log("tly_replayintro: flags cleared, warping to the bus stop; the opening's arrival event fires on arrival.", LogLevel.Info);
}
```

Update the console command's description string to "Replay the opening: clears the intro flags and warps to the bus stop so the arrival event fires again."

- [ ] **Step 9: Build, test, commit.**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` and `dotnet test tests/TheLongestYear.Tests`
Expected: 0 errors; all passing.

```bash
git add src/TheLongestYear/Integration/OpeningEventInjector.cs src/TheLongestYear.Core/Intro/IntroSequenceDecider.cs src/TheLongestYear/Integration/IntroSequenceDriver.cs src/TheLongestYear/Integration/IntroEventInjector.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/IntroSequenceDeciderTests.cs tests/TheLongestYear.Tests/I18nGuardTests.cs
git commit -m "opening: the arrival event replaces vanilla 60367; the driver only opens the picker"
git push
```

---

### Task 6: Retire the old porch intro

**Files:**
- Modify: `src/TheLongestYear/Integration/IntroEventInjector.cs` (delete `BuildIntroEvent` and the `Strings` using if unused)
- Modify: `src/TheLongestYear/i18n/default.json` (delete every `event.intro.*` key and the `intro.skip-notice` stays)
- Modify: `src/TheLongestYear.Core/Intro/IntroEventKeys.cs` (`IntroEventId` stays for `ClearIntroState`; update the summary)
- Modify: `tests/TheLongestYear.Tests/I18nGuardTests.cs` (drop any `event.intro.` special case; the `NoEventKeyValue_ContainsAScriptBreakingCharacter` summary no longer mentions `IntroEventInjector`)

- [ ] **Step 1: Delete `BuildIntroEvent`** and the `// ---- Event script ----` section from `IntroEventInjector`. Rewrite the class summary: "Cross-run bookkeeping for the opening: promotes the cc-seen flag to MetaState.HasSeenIntro at first save, plants the legacy done flag on later loops, and clears everything for tly_replayintro. The opening itself is vanilla's chain with OpeningStringsEditor and OpeningEventInjector."

- [ ] **Step 2: Delete the `event.intro.*` keys** from `default.json` (keep `intro.skip-notice` and `mail.intro.*`). Check `i18n/` for other locale files and delete the same keys there.

- [ ] **Step 3: Build and test.** Expected: 0 errors, all passing. If `I18nGuardTests` complains about a key referenced nowhere, it is one you missed in Step 2.

- [ ] **Step 4: Commit.**

```bash
git add src/TheLongestYear/Integration/IntroEventInjector.cs src/TheLongestYear/i18n src/TheLongestYear.Core/Intro/IntroEventKeys.cs tests/TheLongestYear.Tests/I18nGuardTests.cs
git commit -m "opening: retire the first-morning porch intro"
git push
```

---

### Task 7: Headless run of the whole opening, both checkbox states

**Files:**
- Modify: `tools/farmtype-intro.ps1`
- Modify: `docs/HEADLESS_DRIVING.md` (the "Farm-type runs" section)

**Interfaces:**
- Produces: `tools/farmtype-intro.ps1 -FarmType <type> [-SkipIntro]` printing one result table.

- [ ] **Step 1: Rewrite the script body** (the helper functions at the top stay):

```powershell
param([Parameter(Mandatory)][string]$FarmType, [switch]$SkipIntro)
# ... helpers unchanged ...
$r = [ordered]@{ Farm = $FarmType; Skip = [bool]$SkipIntro }
$n = Count
if ($SkipIntro) {
    Send "tly_newgame $FarmType skipintro"
    $r.Skipped = WaitLog 'Intro: skipped by the character-creation checkbox' $n 150
    $r.Hub = WaitLog 'Opened planning hub \(week 1' $n 120
    $r.NoEvent = if ((Tail $n) -match 'Event started: 60367|busDoorOpen') { 'FAIL: arrival played' } else { 'ok' }
} else {
    Send "tly_newgame $FarmType"
    # The deathbed and cubicle are minigames with no log lines of their own; the first thing the
    # log shows is the save load during the bus ride, then the stash placement, then the event.
    $r.SaveLoaded = WaitLog 'Run \d+ ready' $n 240
    $r.StashPlaced = WaitLog 'JunimoStashService: placed|Junimo Stash anchored|PlanningShrine' $n 60
    $r.EventStart = WaitLog 'Event started: 60367|Event \(60367\)' $n 240
    # Step the event: each tly_eventstep clicks the open speech box on and logs the command index.
    $deadline = (Get-Date).AddMinutes(8)
    do {
        Start-Sleep -Seconds 3
        $s = Count
        Send 'tly_eventstep'
        $line = (WaitLog 'tly_eventstep' $s 10)
        if ($line -match 'Morris' -and -not $r.MorrisSeen) { $r.MorrisSeen = $line }
        if ($line -match 'CommunityCenter' -and -not $r.HallReached) { $r.HallReached = $line }
    } while ((Get-Date) -lt $deadline -and -not ((Tail $n) -match 'Opened planning hub \(week 1'))
    $r.Hub = WaitLog 'Opened planning hub \(week 1' $n 30
    $r.MorrisGoneBeforeHall = if ($r.HallReached -and $r.MorrisSeen) { 'checked by OpeningScriptTests' } else { 'n/a' }
}
$r.Errors = ((Tail $n) | Where-Object { $_ -match '\bERROR\b' } | Select-Object -First 3) -join ' || '
$t = Count
Send 'tly_totitle'
WaitLog 'tly_totitle: exiting' $t 30 | Out-Null
Start-Sleep -Seconds 12
$r.GetEnumerator() | ForEach-Object { "{0,-20} {1}" -f $_.Key, $_.Value }
```

If `tly_eventstep` reports "no event" for two polls in a row before the hub opens, the log's last `tly_eventstep` line (command index and text) is the hang point; that is the failure to report.

- [ ] **Step 2: Check the log line names the script waits for.** `grep -n "placed\|anchored" src/TheLongestYear/Loop/JunimoStashService.cs src/TheLongestYear/UI/PlanningShrineService.cs` and `grep -rn "Event started" src/TheLongestYear --include=*.cs`. If the stash placement logs at Trace only, add one Info line `JunimoStashService: placed the Junimo Stash at (x, y).` in `PlaceChest` where `_placedTile` is set, and use its exact text in the script. If no "Event started" line exists, add to `IntroSequenceDriver.OnUpdateTicked` (in the `Waiting` case, once per event) `this._monitor.Log($"Opening: arrival event running ({Game1.CurrentEvent?.id}).", LogLevel.Info)` guarded by a `_loggedEventId` field so it logs once.

- [ ] **Step 3: Deploy and run.** `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`, wait for the bridge line, then:

```
pwsh -NoProfile -File tools/farmtype-intro.ps1 -FarmType standard
pwsh -NoProfile -File tools/farmtype-intro.ps1 -FarmType meadowlands
pwsh -NoProfile -File tools/farmtype-intro.ps1 -FarmType standard -SkipIntro
```

Expected: the first two reach `Opened planning hub (week 1` with `StashPlaced` before `EventStart`, no ERROR lines; the third reaches the hub with `NoEvent = ok`. Then, on the standard farm still loaded from a fourth run, `tly_replayintro` and confirm the event fires at the bus stop, and `tly_reset` afterwards shows no intro (log: no `arrival event running` after the reset, hub opens straight away).

- [ ] **Step 4: Delete the throwaway save folders** the runs made (`%APPDATA%\StardewValley\Saves\standard_*`, `meadowlands_*`), and `git checkout -- test-output/log-archive`.

- [ ] **Step 5: Update `docs/HEADLESS_DRIVING.md`** "Farm-type runs": `tools/farmtype-intro.ps1` plays the whole opening (deathbed and cubicle are minigames and log nothing; the bus ride loads the save; the arrival event is vanilla's 60367 replaced; step it with `tly_eventstep`; `-SkipIntro` checks the bed and picker path); `tly_replayintro` warps to the bus stop to re-fire it.

- [ ] **Step 6: Commit.**

```bash
git add tools/farmtype-intro.ps1 docs/HEADLESS_DRIVING.md src/TheLongestYear/Loop/JunimoStashService.cs src/TheLongestYear/Integration/IntroSequenceDriver.cs
git commit -m "opening: headless run of the full opening on two farm types and the skip path"
git push
```

Report the three result tables to Jeff verbatim.

---

### Task 8: Docs and state

**Files:**
- Modify: `TODO.md` (the "NEXT ON STORY (remote-safe): the expanded opening" entry), `STATUS.md`
- Modify: `docs/superpowers/specs/2026-09-16-expanded-opening-design.md` (status line: built, headless-checked, awaiting Jeff's own pass)

- [ ] **Step 1: TODO.** Retitle the entry "BUILT on story (date): the expanded opening" and record: commits, the three headless results, that Jeff's own pass on a real new farm (male and female farmer for the letter variants) is still owed before the story release, and the master dependency (book slots) for the tour's "four free slots" lines.

- [ ] **Step 2: STATUS.** Header line, tests count, deployed HEAD, and a dated section with the same facts.

- [ ] **Step 3: Commit.**

```bash
git add TODO.md STATUS.md docs/superpowers/specs/2026-09-16-expanded-opening-design.md
git commit -m "docs: expanded opening built and headless-checked"
git push
```

---

## Self-review notes

- Spec 2.1 (let vanilla play): Task 1. 2.2 (grandpa's words): Tasks 2, 3. 2.3 (one arrival event): Tasks 4, 5. 2.4 (driver): Task 5. 2.5 (placement before the tour): Task 7 checks the order in the log. 2.6 (master dependency): Global Constraints and Task 8. 2.7 out of scope: nothing here touches them. Section 3 writing rules: Task 2. Section 4 testing: Tasks 3, 4, 5 (unit), 7 (headless); Jeff's own pass recorded in Task 8.
- Key-name consistency: Task 3's table is the source of truth for `opening.grandpa-*` names (eight boxes: 1-m/1-f, 2, 3, 4, 5, 6, 7-m/7-f, 8); Task 2's writer follows it. `OpeningScript.LineKeys` (Task 4) is the source of truth for `event.opening.*`; Task 2 lists the same names.
- `IntroAction.StartIntro` is removed in Task 5 and referenced nowhere after it. `IntroEventKeys.IntroEventId` stays only because `ClearIntroState` removes it from old saves' `eventsSeen`.
- Vanilla `60367` is also hard-wired in `Event.cs` 10514 (`case "60367": endBehaviors("End", "beginGame")`), so even if a script ended without `beginGame` the game would still put the farmer to bed; the script ends with it explicitly so the behaviour is visible and tested.
