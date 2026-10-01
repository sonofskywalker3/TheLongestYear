# Stash Nesting, Keep Farm Decor, Keep Worn Gear Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A container in the Junimo Stash keeps its hats, clothes, furniture and wallpaper (anything else is refused at deposit), stashed items keep their dye, tailoring, inner rings and trinket rolls, and two new upgrades keep the farm's decor and the farmer's worn gear across the loop.

**Architecture:** Pure rules go in `TheLongestYear.Core` with xUnit tests: the record shape (`StashItemRecord` grows nullable fields), the cosmetic classifier and legacy trim (`StashNesting`), the legacy record rescue (`StashLegacyRescue`), the decor classifier and debris decision table (`FarmDecorKeep`, `FarmDecorPlanner`). Game-bound glue goes in `src/TheLongestYear`: the item codec (`StashItemCodec`, split out of the 584-line `JunimoStashService`), a `Chest.addItem` refusal prefix, a decor snapshot taken before `loadForNewGame` and restored after the stash chest is placed, and one `if` in `FarmerReset`.

**Tech Stack:** C# (.NET 6), SMAPI 4 mod with Harmony, xUnit. Build and test: `dotnet test TheLongestYear.sln -c Release` from `TheLongestYear/`. Game decompile for API checks: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley`.

**Spec:** `docs/superpowers/specs/2026-10-01-stash-nesting-farm-decor-worn-gear-design.md`

## Global Constraints

- Branch: `master` (the release line). Every commit bumps the PATCH of `src/TheLongestYear/manifest.json` `Version`. 0.18.118 is current; Task 1 makes 0.18.119, Task 10 makes 0.18.128. Push after every commit (`git push`). No release, no `gh release`, no Nexus step in this plan.
- Commit message trailer (every commit):
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01A76LU5aV5QATceZsfAGyKo
  ```
- No em dashes in any code comment, string, doc, changelog or commit message (Jeff's rule). Use a colon, a comma or a new sentence.
- Every player-facing string goes through `Strings.Get("<key>")` with the key in `src/TheLongestYear/i18n/default.json`. `I18nGuardTests` fails on a missing key and on an orphan key, so add the key in the same commit as the code that reads it. Final wording of every new player-facing string goes through the `game-writing` skill (the implementer of that task invokes it before writing the line).
- Files are CRLF. Edit with the Edit tool, never `sed -i` / `perl -pi` on multi-line patterns.
- Split any file approaching 400 lines. `JunimoStashService.cs` (584), `WorldResetService.cs` (1416) and `ModEntry.cs` (5465) are already over: add only call sites and registrations to them; new logic goes in new files.
- Never `/sdcard/` (use `/storage/emulated/0/`). Never Playwright.
- Spec values, verbatim: Keep Farm Decor **500 JP, one level, category Buildings**. Keep Worn Gear **1,000 JP, one level**, category **Loadout** (it sits beside Keep Golden Scythe: the farmer starts the loop wearing it; Buildings is for things placed on the farm). Large debris: **copper axe: stump; steel axe: hollow log; steel pickaxe: boulder**. Stumps and logs drop their vanilla **hardwood** (2 for a stump, 8 for a hollow log, `ResourceClump.destroy` without the Lumberjack bonus); boulders drop **nothing**.
- Cosmetic, verbatim from the spec: **Hats; shirts and pants (Clothing), dyed or not; Furniture; wallpaper and flooring (the Wallpaper item class)**. Everything else nested is refused. Tool attachments (rod bait and tackle) stay supported.
- Refusal HUD draft (final wording through `game-writing`): "This container holds things that can't be stored in the stash. Take out everything except hats, shirts, pants, furniture, wallpaper and flooring, then try again."

## Review Focus

1. **A dresser already in the stash from 0.18.118 or earlier with a ring inside.** The player expects the ring back, never deleted: in a free stash slot, or on the ground one tile south of the stash chest. Pinned in Task 3 (`Trim` tests), Task 5 (rescue + eject code) and Task 10 (live steps L1, L2).
2. **The stash is full when displaced decor or ejected items arrive.** They must land on the ground next to the chest, not vanish, and stackable items (30 Wood Path) must stack into an existing slot first. Pinned in Task 5 (`TryDeposit` stacks before using a slot, `DropNearStash` never deletes) and Task 10 (live steps L2, D2, D3).
3. **A kept path where the fresh farm has grass, a sapling, a tree or a bush, not only weeds.** The path must come back. `Flooring` and grass share the `terrainFeatures` key, so leaving the grass makes the add throw. Pinned in Task 8 (planner: `SmallDebris` is cleared under placed pieces) and Task 9 (`Survey` maps every non-Flooring terrain feature and every large terrain feature to `SmallDebris`).
4. **A worn trinket after the rewind with Keep Worn Gear.** The `trinketSlots` stat is wiped by `StatResetRules`, so the trinket would be worn but invisible and impossible to take off. Pinned in Task 7 (re-set `trinketSlots` when a trinket was kept) and Task 10 (live step G2).
5. **Top-level items that are not containers must still go in.** A Combined Ring (its inner rings), a fishing rod (its bait and tackle), a ring, a sword: none of these is "nested" and none may be refused. Pinned in Task 3 (`InnerRings` and `Attachments` are not traversed) and Task 10 (live step S5).

---

### Task 1: Split JunimoStashService before it grows

No behaviour change. `JunimoStashService.cs` is 584 lines and every later stash task adds to it.

**Files:**
- Create: `src/TheLongestYear/Loop/StashItemCodec.cs`
- Create: `src/TheLongestYear/Loop/JunimoStashService.Placement.cs`
- Modify: `src/TheLongestYear/Loop/JunimoStashService.cs` (make the class `partial`; delete the moved members; `PopulateFromMeta` calls the codec)
- Modify: `src/TheLongestYear/manifest.json` (0.18.119)

**Interfaces:**
- Produces: `internal static class StashItemCodec` with `internal static StashItemRecord ToRecord(Item item)` and `internal static Item CreateFromRecord(StashItemRecord record, IMonitor monitor)`. `CreateFromRecord` now also restores tool attachments and enchantments (moved out of `PopulateFromMeta`), so every caller gets a complete item.
- Produces: `internal sealed partial class JunimoStashService` across `JunimoStashService.cs` and `JunimoStashService.Placement.cs`.

- [ ] **Step 1: Create `StashItemCodec.cs`.** Move `ToRecord`, `CreateFromRecord` and `RestoreEnchantments` from `JunimoStashService.cs` (lines 396-489) verbatim, then fold the attachment loop (lines 365-385 of `PopulateFromMeta`) into `CreateFromRecord`:

```csharp
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Item to <see cref="StashItemRecord"/> and back. The stash does not carry item instances across
    /// the loop: <see cref="JunimoStashService.BankToMeta"/> writes a record per item and
    /// <see cref="JunimoStashService.PopulateFromMeta"/> rebuilds a fresh item from it, so anything an
    /// item holds on its instance has to be written here and put back here.
    /// </summary>
    internal static class StashItemCodec
    {
        // (doc comment of the old ToRecord, moved verbatim)
        internal static StashItemRecord ToRecord(Item item)
        {
            // (body moved verbatim from JunimoStashService.ToRecord)
        }

        /// <summary>Recreate one banked item from its record: registry lookup by id/stack/quality,
        /// the flavored-good identity, then a tool's attachment slots and enchantments. Null when the
        /// id is unknown to this game (mod item from a removed mod, typo).</summary>
        internal static Item CreateFromRecord(StashItemRecord record, IMonitor monitor)
        {
            Item item = ItemRegistry.Create(record.ItemId, record.Quantity, record.Quality, allowNull: true);
            if (item == null)
                return null;

            // (the preserve-field block moved verbatim from the old CreateFromRecord)

            // A stashed tool's slots (rod bait/tackle) are instance state the registry cannot
            // rebuild, same class as the kept-tier rod transplant in FarmerReset. Clamped to the
            // recreated tool's slot count so a stale record can never overflow.
            if (item is Tool tool && record.Attachments != null)
            {
                int slots = System.Math.Min(record.Attachments.Count, tool.attachments.Count);
                for (int i = 0; i < slots; i++)
                {
                    StashItemRecord slotRecord = record.Attachments[i];
                    if (slotRecord == null) continue;
                    if (CreateFromRecord(slotRecord, monitor) is StardewValley.Object attachment)
                        tool.attachments[i] = attachment;
                    else
                        monitor?.Log(
                            $"StashItemCodec: could not recreate attachment '{slotRecord.ItemId}' on '{record.ItemId}', slot left empty.",
                            LogLevel.Warn);
                }
            }

            if (item is Tool enchanted && record.Enchantments != null)
                RestoreEnchantments(enchanted, record, monitor);

            return item;
        }

        // (RestoreEnchantments moved verbatim, made static, with an IMonitor monitor parameter
        //  replacing the _monitor field; log prefix "StashItemCodec:")
    }
}
```

- [ ] **Step 2: Create `JunimoStashService.Placement.cs`** holding, verbatim and unchanged, `ResolveTile`, `AutoTile`, `AutoCandidates`, `TryGetFarmHouseEntry`, `IsTilePlaceable`, `DescribeBlocker` (lines 196-341 with their doc comments), inside `internal sealed partial class JunimoStashService` in namespace `TheLongestYear.Loop`, with the `using` lines they need (`System.Collections.Generic`, `Microsoft.Xna.Framework`, `StardewValley`).

- [ ] **Step 3: Trim `JunimoStashService.cs`.** Change `internal sealed class JunimoStashService` to `internal sealed partial class JunimoStashService`. Delete the moved members. Replace the loop body of `PopulateFromMeta` with:

```csharp
            foreach (StashItemRecord record in _meta.StashItems)
            {
                Item item = StashItemCodec.CreateFromRecord(record, _monitor);
                if (item == null)
                {
                    _monitor.Log(
                        $"JunimoStashService: could not recreate item '{record.ItemId}' (unknown id), skipping.",
                        LogLevel.Warn);
                    continue;
                }
                chest.Items.Add(item);
                restored++;
            }
```

and in `BankToMeta` call `StashItemCodec.ToRecord(item)`. Remove `using StardewValley.Enchantments;` if now unused.

- [ ] **Step 4: Build and run every test.** `dotnet test TheLongestYear.sln -c Release`. Expected: build succeeds, all tests pass (no test count change). `wc -l src/TheLongestYear/Loop/JunimoStashService.cs` should be about 345.

- [ ] **Step 5: Commit.** Bump `manifest.json` to 0.18.119.

```bash
git add src/TheLongestYear/Loop/StashItemCodec.cs src/TheLongestYear/Loop/JunimoStashService.Placement.cs src/TheLongestYear/Loop/JunimoStashService.cs src/TheLongestYear/manifest.json
git commit -m "Stash: split the item codec and tile placement out of JunimoStashService (0.18.119)"
git push
```

---

### Task 2: StashItemRecord grows contents and identity fields

**Files:**
- Modify: `src/TheLongestYear.Core/StashItemRecord.cs`
- Create: `src/TheLongestYear.Core/StashIdentityRecords.cs`
- Test: `tests/TheLongestYear.Tests/StashItemRecordTests.cs` (new test classes at the end)
- Modify: `src/TheLongestYear/manifest.json` (0.18.120)

**Interfaces:**
- Produces (Core, all nullable, default null, appended after `Enchantments` so every existing positional call still compiles):
  - `List<StashItemRecord>? Contents`: a container's held items (StorageFurniture `heldItems`, a Chest item's `Items`, a mod bag's item list), non-null entries only.
  - `StashItemRecord? HeldObject`: `Object.heldObject` (an item on a table, a lamp on a table).
  - `StashClothingRecord? Clothing`: `public sealed record StashClothingRecord(uint Color, bool Dyeable);` `Color` is `Microsoft.Xna.Framework.Color.PackedValue`.
  - `StashBootsRecord? Boots`: `public sealed record StashBootsRecord(string? AppliedBootSheetIndex, int ColorIndex, int Defense, int Immunity);` mirrors vanilla `Boots.GetOneCopyFrom` (appliedBootSheetIndex, indexInColorSheet, defenseBonus, immunityBonus).
  - `List<StashItemRecord>? InnerRings`: a Combined Ring's `combinedRings`. Deliberately NOT `Contents`: inner rings are part of the ring, not something it holds, and must not trip the nesting rule.
  - `int? TrinketSeed`: `Trinket.generationSeed`; `new Trinket(itemId, seed)` re-rolls identical stats.

- [ ] **Step 1: Write the failing tests** (append to `StashItemRecordTests.cs`):

```csharp
public class StashItemRecordContentsTests
{
    [Fact]
    public void New_fields_default_to_null()
    {
        var r = new StashItemRecord("(F)704", 1, 0);
        Assert.Null(r.Contents);
        Assert.Null(r.HeldObject);
        Assert.Null(r.Clothing);
        Assert.Null(r.Boots);
        Assert.Null(r.InnerRings);
        Assert.Null(r.TrinketSeed);
    }

    [Fact]
    public void Dresser_with_a_dyed_shirt_and_a_hat_round_trips_through_json()
    {
        var shirt = new StashItemRecord("(S)1000", 1, 0, Clothing: new StashClothingRecord(0xFF2828C8u, true));
        var hat = new StashItemRecord("(H)0", 1, 0);
        var dresser = new StashItemRecord("(F)704", 1, 0,
            Contents: new System.Collections.Generic.List<StashItemRecord> { shirt, hat });

        StashItemRecord back = JsonSerializer.Deserialize<StashItemRecord>(JsonSerializer.Serialize(dresser))!;

        Assert.Equal(2, back.Contents!.Count);
        Assert.Equal("(S)1000", back.Contents[0].ItemId);
        Assert.Equal(0xFF2828C8u, back.Contents[0].Clothing!.Color);
        Assert.True(back.Contents[0].Clothing!.Dyeable);
        Assert.Equal("(H)0", back.Contents[1].ItemId);
    }

    [Fact]
    public void Nested_depth_two_round_trips()
    {
        var hat = new StashItemRecord("(H)2", 1, 0);
        var inner = new StashItemRecord("(F)709", 1, 0, Contents: new() { hat });
        var outer = new StashItemRecord("(F)704", 1, 0, Contents: new() { inner });

        StashItemRecord back = JsonSerializer.Deserialize<StashItemRecord>(JsonSerializer.Serialize(outer))!;

        Assert.Equal("(H)2", back.Contents![0].Contents![0].ItemId);
    }

    [Fact]
    public void Held_object_round_trips()
    {
        var table = new StashItemRecord("(F)1120", 1, 0, HeldObject: new StashItemRecord("(F)1376", 1, 0));
        StashItemRecord back = JsonSerializer.Deserialize<StashItemRecord>(JsonSerializer.Serialize(table))!;
        Assert.Equal("(F)1376", back.HeldObject!.ItemId);
    }

    [Fact]
    public void Tailored_boots_round_trip()
    {
        var boots = new StashItemRecord("(B)504", 1, 0, Boots: new StashBootsRecord("514", 7, 4, 4));
        StashItemRecord back = JsonSerializer.Deserialize<StashItemRecord>(JsonSerializer.Serialize(boots))!;
        Assert.Equal(new StashBootsRecord("514", 7, 4, 4), back.Boots);
    }

    [Fact]
    public void Combined_ring_and_trinket_round_trip()
    {
        var ring = new StashItemRecord("(O)880", 1, 0, InnerRings: new()
        {
            new StashItemRecord("(O)529", 1, 0),
            new StashItemRecord("(O)530", 1, 0),
        });
        var trinket = new StashItemRecord("(TR)ParrotEgg", 1, 0, TrinketSeed: 1234567);

        StashItemRecord ringBack = JsonSerializer.Deserialize<StashItemRecord>(JsonSerializer.Serialize(ring))!;
        StashItemRecord trinketBack = JsonSerializer.Deserialize<StashItemRecord>(JsonSerializer.Serialize(trinket))!;

        Assert.Equal(new[] { "(O)529", "(O)530" }, ringBack.InnerRings!.Select(r => r.ItemId));
        Assert.Equal(1234567, trinketBack.TrinketSeed);
    }

    [Fact]
    public void Json_from_0_18_118_still_loads()
    {
        // A record as 0.18.118 wrote it: rod with bait and an enchantment, none of the new fields.
        const string legacyJson =
            "{\"ItemId\":\"(T)IridiumRod\",\"Quantity\":1,\"Quality\":0,\"Attachments\":[null,null]," +
            "\"Enchantments\":[{\"Type\":\"StardewValley.Enchantments.AutoHookEnchantment\",\"Level\":1}]}";
        StashItemRecord back = JsonSerializer.Deserialize<StashItemRecord>(legacyJson)!;
        Assert.Null(back.Contents);
        Assert.Null(back.Clothing);
        Assert.Null(back.TrinketSeed);
        Assert.Single(back.Enchantments!);
    }
}
```

Add `using System.Linq;` at the top of the file if missing.

- [ ] **Step 2: Run, expect FAIL** (compile errors: unknown named arguments). `dotnet test TheLongestYear.sln -c Release --filter StashItemRecordContentsTests`

- [ ] **Step 3: Implement.** Create `StashIdentityRecords.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>A stashed shirt's or pants' colour. Dyeing writes <c>Clothing.clothesColor</c> on the
/// instance, so a shirt rebuilt by id comes back undyed without this.</summary>
/// <param name="Color">The colour's packed RGBA value (<c>Color.PackedValue</c>).</param>
/// <param name="Dyeable">The instance's <c>dyeable</c> flag.</param>
public sealed record StashClothingRecord(uint Color, bool Dyeable);

/// <summary>Stashed boots' tailoring, the four fields vanilla <c>Boots.GetOneCopyFrom</c> copies.
/// Tailoring boots copies another pair's stats onto the instance.</summary>
public sealed record StashBootsRecord(string? AppliedBootSheetIndex, int ColorIndex, int Defense, int Immunity);
```

In `StashItemRecord.cs` append the six parameters after `Enchantments`, each with a `<param>` doc line (no em dashes), and extend the summary with one paragraph: "Contents, HeldObject, Clothing, Boots, InnerRings and TrinketSeed (spec 2026-10-01, sarahwinchester97's dresser) carry what lives on the instance: a container's contents, an item set on a table, dye, tailoring, a Combined Ring's two rings and a trinket's rolled stats. All null by default so 0.18.118 saves load."

```csharp
public sealed record StashItemRecord(
    string ItemId,
    int Quantity,
    int Quality,
    string? PreservedParentSheetIndex = null,
    int? Preserve = null,
    int? Price = null,
    List<StashItemRecord?>? Attachments = null,
    List<StashEnchantmentRecord>? Enchantments = null,
    List<StashItemRecord>? Contents = null,
    StashItemRecord? HeldObject = null,
    StashClothingRecord? Clothing = null,
    StashBootsRecord? Boots = null,
    List<StashItemRecord>? InnerRings = null,
    int? TrinketSeed = null);
```

- [ ] **Step 4: Run, expect PASS.** `dotnet test TheLongestYear.sln -c Release` (whole suite: the game project must still build against the wider record).

- [ ] **Step 5: Commit.** Bump to 0.18.120.

```bash
git add src/TheLongestYear.Core/StashItemRecord.cs src/TheLongestYear.Core/StashIdentityRecords.cs tests/TheLongestYear.Tests/StashItemRecordTests.cs src/TheLongestYear/manifest.json
git commit -m "Stash records: contents, held object, dye, tailoring, inner rings, trinket seed (0.18.120)"
git push
```

---

### Task 3: Cosmetic classifier, legacy trim and legacy rescue (Core)

**Files:**
- Create: `src/TheLongestYear.Core/StashNesting.cs`
- Create: `src/TheLongestYear.Core/StashLegacyRescue.cs`
- Test: `tests/TheLongestYear.Tests/StashNestingTests.cs`
- Modify: `src/TheLongestYear/manifest.json` (0.18.121)

**Interfaces:**
- Consumes: `StashItemRecord` (Task 2).
- Produces:
  - `public static bool StashNesting.IsCosmetic(string qualifiedItemId)`: true for `(H)`, `(S)`, `(P)`, `(F)`, `(WP)`, `(FL)` ids, false for anything else and for TLY's own items (`sonofskywalker3.TheLongestYear_` after the type prefix: the books and the planning shrine are re-granted every loop, so a stashed one would duplicate).
  - `public static IReadOnlyList<string> StashNesting.NonCosmeticNested(StashItemRecord record)`: every non-cosmetic id at depth 1 or more, walking `Contents` and `HeldObject` only (never `Attachments`, never `InnerRings`). Empty means the record may go in the stash.
  - `public static bool StashNesting.CanStash(StashItemRecord record)`.
  - `public static StashItemRecord StashNesting.Trim(StashItemRecord record, List<StashItemRecord> ejected)`: the record with every non-cosmetic nested item removed; each removed item is itself trimmed and appended to `ejected` (its own non-cosmetic descendants are appended too). `Contents` stays null when it was null.
  - `public static bool StashLegacyRescue.ShouldReplace(StashItemRecord stored, StashItemRecord live)`: true when the ids match and `live` carries any of Contents, HeldObject, Clothing, Boots, InnerRings, TrinketSeed that `stored` lacks.

- [ ] **Step 1: Write the failing tests** (`tests/TheLongestYear.Tests/StashNestingTests.cs`):

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StashNestingTests
{
    private static StashItemRecord R(string id, List<StashItemRecord>? contents = null, StashItemRecord? held = null)
        => new(id, 1, 0, Contents: contents, HeldObject: held);

    [Theory]
    [InlineData("(H)0")]                 // hat
    [InlineData("(S)1000")]              // shirt
    [InlineData("(P)0")]                 // pants
    [InlineData("(F)704")]               // furniture (a dresser)
    [InlineData("(F)Mod.TallBeechDresser")]
    [InlineData("(WP)12")]               // wallpaper
    [InlineData("(FL)3")]                // flooring
    public void Cosmetic_types_are_cosmetic(string id) => Assert.True(StashNesting.IsCosmetic(id));

    [Theory]
    [InlineData("(B)504")]               // boots
    [InlineData("(O)529")]               // ring (rings are objects)
    [InlineData("(O)880")]               // combined ring
    [InlineData("(TR)ParrotEgg")]        // trinket
    [InlineData("(O)128")]               // fish
    [InlineData("(O)405")]               // wood path item (craftable decoration)
    [InlineData("(O)93")]                // torch
    [InlineData("(BC)130")]              // chest
    [InlineData("(BC)152")]              // lamp-post
    [InlineData("(T)IridiumRod")]        // tool
    [InlineData("(W)4")]                 // weapon
    [InlineData("(M)Mannequin")]         // mannequin
    [InlineData("(F)sonofskywalker3.TheLongestYear_PlanningShrine")]   // TLY's own furniture
    [InlineData("")]
    public void Everything_else_is_not_cosmetic(string id) => Assert.False(StashNesting.IsCosmetic(id));

    [Fact]
    public void Empty_dresser_can_be_stashed() => Assert.True(StashNesting.CanStash(R("(F)704")));

    [Fact]
    public void Dresser_of_hats_and_clothes_can_be_stashed()
        => Assert.True(StashNesting.CanStash(R("(F)704", new() { R("(H)0"), R("(S)1000"), R("(P)0"), R("(WP)12") })));

    [Fact]
    public void Dresser_with_a_ring_is_refused_and_names_it()
    {
        var dresser = R("(F)704", new() { R("(H)0"), R("(O)529") });
        Assert.False(StashNesting.CanStash(dresser));
        Assert.Equal(new[] { "(O)529" }, StashNesting.NonCosmeticNested(dresser));
    }

    [Fact]
    public void Depth_two_is_checked()
    {
        var inner = R("(F)709", new() { R("(B)504") });
        Assert.Equal(new[] { "(B)504" }, StashNesting.NonCosmeticNested(R("(F)704", new() { R("(H)0"), inner })));
        Assert.True(StashNesting.CanStash(R("(F)704", new() { R("(F)709", new() { R("(H)0") }) })));
    }

    [Fact]
    public void Held_object_is_checked()
    {
        Assert.False(StashNesting.CanStash(R("(F)1120", held: R("(O)24"))));
        Assert.True(StashNesting.CanStash(R("(F)1120", held: R("(F)1376"))));
    }

    [Fact]
    public void Rod_attachments_are_exempt()
    {
        var rod = new StashItemRecord("(T)IridiumRod", 1, 0,
            Attachments: new List<StashItemRecord?> { new("(O)685", 20, 0), new("(O)686", 1, 0) });
        Assert.True(StashNesting.CanStash(rod));
    }

    [Fact]
    public void Combined_ring_inner_rings_are_exempt()
    {
        var ring = new StashItemRecord("(O)880", 1, 0,
            InnerRings: new List<StashItemRecord> { new("(O)529", 1, 0), new("(O)530", 1, 0) });
        Assert.True(StashNesting.CanStash(ring));
    }

    [Fact]
    public void A_non_container_top_level_item_is_never_refused()
    {
        Assert.True(StashNesting.CanStash(R("(O)529")));
        Assert.True(StashNesting.CanStash(R("(B)504")));
        Assert.True(StashNesting.CanStash(R("(BC)130")));
    }

    [Fact]
    public void Trim_keeps_cosmetics_and_ejects_the_rest()
    {
        var ejected = new List<StashItemRecord>();
        StashItemRecord trimmed = StashNesting.Trim(R("(F)704", new() { R("(H)0"), R("(O)529"), R("(S)1000") }), ejected);

        Assert.Equal(new[] { "(H)0", "(S)1000" }, trimmed.Contents!.Select(c => c.ItemId));
        Assert.Equal(new[] { "(O)529" }, ejected.Select(e => e.ItemId));
    }

    [Fact]
    public void Trim_recurses_into_ejected_and_kept_children()
    {
        // Dresser > [ inner dresser > [hat, boots], chest item > [hat, fish] ]
        var inner = R("(F)709", new() { R("(H)0"), R("(B)504") });
        var chest = R("(BC)130", new() { R("(H)2"), R("(O)128") });
        var ejected = new List<StashItemRecord>();

        StashItemRecord trimmed = StashNesting.Trim(R("(F)704", new() { inner, chest }), ejected);

        Assert.Single(trimmed.Contents!);                                       // only the inner dresser stays
        Assert.Equal(new[] { "(H)0" }, trimmed.Contents![0].Contents!.Select(c => c.ItemId));
        Assert.Equal(new[] { "(B)504", "(O)128", "(BC)130" }.OrderBy(x => x), ejected.Select(e => e.ItemId).OrderBy(x => x));
        StashItemRecord ejectedChest = ejected.Single(e => e.ItemId == "(BC)130");
        Assert.Equal(new[] { "(H)2" }, ejectedChest.Contents!.Select(c => c.ItemId));   // the chest keeps its hat
        Assert.True(StashNesting.CanStash(trimmed));
        Assert.All(ejected, e => Assert.True(StashNesting.CanStash(e)));
    }

    [Fact]
    public void Trim_ejects_a_non_cosmetic_held_object()
    {
        var ejected = new List<StashItemRecord>();
        StashItemRecord trimmed = StashNesting.Trim(R("(F)1120", held: R("(O)24")), ejected);
        Assert.Null(trimmed.HeldObject);
        Assert.Equal("(O)24", ejected.Single().ItemId);
    }

    [Fact]
    public void Trim_leaves_a_plain_record_untouched()
    {
        var ejected = new List<StashItemRecord>();
        StashItemRecord rod = new("(T)IridiumRod", 1, 0, Attachments: new List<StashItemRecord?> { null, null });
        StashItemRecord trimmed = StashNesting.Trim(rod, ejected);
        Assert.Null(trimmed.Contents);
        Assert.Equal(2, trimmed.Attachments!.Count);
        Assert.Empty(ejected);
    }
}

public class StashLegacyRescueTests
{
    [Fact]
    public void Replaces_a_legacy_record_whose_live_item_has_contents()
    {
        var stored = new StashItemRecord("(F)704", 1, 0);
        var live = new StashItemRecord("(F)704", 1, 0, Contents: new() { new("(H)0", 1, 0) });
        Assert.True(StashLegacyRescue.ShouldReplace(stored, live));
    }

    [Fact]
    public void Replaces_a_legacy_record_missing_dye_or_trinket_seed()
    {
        Assert.True(StashLegacyRescue.ShouldReplace(new("(S)1000", 1, 0),
            new("(S)1000", 1, 0, Clothing: new StashClothingRecord(1u, true))));
        Assert.True(StashLegacyRescue.ShouldReplace(new("(TR)ParrotEgg", 1, 0),
            new("(TR)ParrotEgg", 1, 0, TrinketSeed: 5)));
    }

    [Fact]
    public void Leaves_a_different_item_or_a_complete_record_alone()
    {
        var live = new StashItemRecord("(F)704", 1, 0, Contents: new() { new("(H)0", 1, 0) });
        Assert.False(StashLegacyRescue.ShouldReplace(new("(F)709", 1, 0), live));
        Assert.False(StashLegacyRescue.ShouldReplace(live, live));
        Assert.False(StashLegacyRescue.ShouldReplace(new("(O)388", 50, 0), new("(O)388", 50, 0)));
    }
}
```

- [ ] **Step 2: Run, expect FAIL** (types missing). `dotnet test TheLongestYear.sln -c Release --filter "StashNestingTests|StashLegacyRescueTests"`

- [ ] **Step 3: Implement `StashNesting.cs`:**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// The stash's nesting rule (spec 2026-10-01, Jeff ruling option C): a container may go into the
/// Junimo Stash only if everything inside it, at any depth, is cosmetic. Cosmetic is decided by the
/// item's type prefix alone, so the rule is the same for vanilla and mod items: hats, shirts, pants,
/// furniture, wallpaper and flooring. Everything else (boots, rings, trinkets, objects, big
/// craftables, tools, weapons, mannequins) may still go in as a top-level item, one slot each.
///
/// "Inside" means <see cref="StashItemRecord.Contents"/> and <see cref="StashItemRecord.HeldObject"/>.
/// A tool's <see cref="StashItemRecord.Attachments"/> (rod bait and tackle) and a Combined Ring's
/// <see cref="StashItemRecord.InnerRings"/> are part of the item, not held by it, and are never walked.
/// </summary>
public static class StashNesting
{
    /// <summary>Item-id prefix of The Longest Year's own items (books, planning shrine). They are
    /// re-granted every loop, so a nested copy would duplicate them.</summary>
    public const string ModItemPrefix = "sonofskywalker3.TheLongestYear_";

    // ItemRegistry type prefixes: hat, shirt, pants, furniture, wallpaper, flooring.
    private static readonly string[] CosmeticTypePrefixes = { "(H)", "(S)", "(P)", "(F)", "(WP)", "(FL)" };

    public static bool IsCosmetic(string qualifiedItemId)
    {
        if (string.IsNullOrEmpty(qualifiedItemId))
            return false;
        foreach (string prefix in CosmeticTypePrefixes)
        {
            if (!qualifiedItemId.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            return !qualifiedItemId.AsSpan(prefix.Length).StartsWith(ModItemPrefix, StringComparison.Ordinal);
        }
        return false;
    }

    public static IReadOnlyList<string> NonCosmeticNested(StashItemRecord record)
    {
        var found = new List<string>();
        foreach (StashItemRecord child in Children(record))
            Collect(child, found);
        return found;
    }

    public static bool CanStash(StashItemRecord record) => NonCosmeticNested(record).Count == 0;

    public static StashItemRecord Trim(StashItemRecord record, List<StashItemRecord> ejected)
    {
        List<StashItemRecord>? contents = null;
        if (record.Contents != null)
        {
            contents = new List<StashItemRecord>(record.Contents.Count);
            foreach (StashItemRecord child in record.Contents)
            {
                if (child == null) continue;
                StashItemRecord trimmedChild = Trim(child, ejected);
                if (IsCosmetic(child.ItemId))
                    contents.Add(trimmedChild);
                else
                    ejected.Add(trimmedChild);
            }
        }

        StashItemRecord? held = null;
        if (record.HeldObject != null)
        {
            StashItemRecord trimmedHeld = Trim(record.HeldObject, ejected);
            if (IsCosmetic(record.HeldObject.ItemId))
                held = trimmedHeld;
            else
                ejected.Add(trimmedHeld);
        }

        return record with { Contents = contents, HeldObject = held };
    }

    private static void Collect(StashItemRecord record, List<string> found)
    {
        if (!IsCosmetic(record.ItemId))
            found.Add(record.ItemId);
        foreach (StashItemRecord child in Children(record))
            Collect(child, found);
    }

    private static IEnumerable<StashItemRecord> Children(StashItemRecord record)
    {
        if (record.Contents != null)
            foreach (StashItemRecord child in record.Contents)
                if (child != null)
                    yield return child;
        if (record.HeldObject != null)
            yield return record.HeldObject;
    }
}
```

`StashLegacyRescue.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// Saves written by 0.18.118 or earlier banked a stashed dresser as a bare id, but the game's own
/// save still holds the real stash chest with the dresser's contents. On load, before the stale
/// chest is swept, each banked record whose live item carries instance data the record lacks is
/// replaced by a fresh record of the live item. Both are written by the same Saving event, so the
/// live item is the same item with more detail, never a different one.
/// </summary>
public static class StashLegacyRescue
{
    public static bool ShouldReplace(StashItemRecord stored, StashItemRecord live)
    {
        if (stored.ItemId != live.ItemId)
            return false;
        return (stored.Contents == null && live.Contents != null)
            || (stored.HeldObject == null && live.HeldObject != null)
            || (stored.Clothing == null && live.Clothing != null)
            || (stored.Boots == null && live.Boots != null)
            || (stored.InnerRings == null && live.InnerRings != null)
            || (stored.TrinketSeed == null && live.TrinketSeed != null);
    }
}
```

- [ ] **Step 4: Run, expect PASS.** `dotnet test TheLongestYear.sln -c Release`

- [ ] **Step 5: Commit.** Bump to 0.18.121.

```bash
git add src/TheLongestYear.Core/StashNesting.cs src/TheLongestYear.Core/StashLegacyRescue.cs tests/TheLongestYear.Tests/StashNestingTests.cs src/TheLongestYear/manifest.json
git commit -m "Stash nesting rule: cosmetic classifier, legacy trim and record rescue (0.18.121)"
git push
```

---

### Task 4: Codec keeps dye, tailoring, Combined Ring rings and trinket rolls

**Files:**
- Modify: `src/TheLongestYear/Loop/StashItemCodec.cs`
- Modify: `src/TheLongestYear/manifest.json` (0.18.122)

**Interfaces:**
- Consumes: `StashClothingRecord`, `StashBootsRecord`, `InnerRings`, `TrinketSeed` (Task 2).
- Produces: `ToRecord` fills the four identity fields; `CreateFromRecord` applies them. Field names verified in the PC decompile: `Clothing.clothesColor` (NetColor), `Clothing.dyeable` (NetBool); `Boots.appliedBootSheetIndex` (NetString), `indexInColorSheet`, `defenseBonus`, `immunityBonus` (NetInt); `CombinedRing.combinedRings` (NetList of Ring); `Trinket.generationSeed` (NetInt) and the ctor `Trinket(string itemId, int generationSeed)` which calls `GenerateRandomStats` (Trinket.cs:45). `ItemRegistry.Create("(O)880")` returns a `CombinedRing` (ObjectDataDefinition.cs).

This task is game-bound (no Core logic left); its proof is the build plus live steps S2 and S3 in Task 10.

- [ ] **Step 1: Capture in `ToRecord`.** Add `using System.Linq;`, `using StardewValley.Objects;`. Before the `return new StashItemRecord(...)`:

```csharp
            StashClothingRecord clothing = item is Clothing shirt
                ? new StashClothingRecord(shirt.clothesColor.Value.PackedValue, shirt.dyeable.Value)
                : null;
            StashBootsRecord boots = item is Boots pair
                ? new StashBootsRecord(pair.appliedBootSheetIndex.Value, pair.indexInColorSheet.Value,
                    pair.defenseBonus.Value, pair.immunityBonus.Value)
                : null;
            List<StashItemRecord> innerRings = item is CombinedRing combined && combined.combinedRings.Count > 0
                ? combined.combinedRings.Where(r => r != null).Select(ToRecord).ToList()
                : null;
            int? trinketSeed = item is Trinket trinket ? trinket.generationSeed.Value : null;
```

and pass them by name: `..., attachments, enchantments, Clothing: clothing, Boots: boots, InnerRings: innerRings, TrinketSeed: trinketSeed);`

- [ ] **Step 2: Apply in `CreateFromRecord`**, right after the registry call and its null check (the trinket must be swapped before anything else touches `item`):

```csharp
            // A trinket's stats are rolled from its seed; the registry rolls a new random seed.
            if (record.TrinketSeed.HasValue && item is Trinket rolled)
                item = new Trinket(rolled.ItemId, record.TrinketSeed.Value);

            if (record.Clothing != null && item is Clothing clothes)
            {
                clothes.clothesColor.Value = new Microsoft.Xna.Framework.Color(record.Clothing.Color);
                clothes.dyeable.Value = record.Clothing.Dyeable;
            }

            // Tailored boots: the same four fields vanilla Boots.GetOneCopyFrom copies.
            if (record.Boots != null && item is Boots boots)
            {
                boots.appliedBootSheetIndex.Value = record.Boots.AppliedBootSheetIndex;
                boots.indexInColorSheet.Value = record.Boots.ColorIndex;
                boots.defenseBonus.Value = record.Boots.Defense;
                boots.immunityBonus.Value = record.Boots.Immunity;
            }

            if (record.InnerRings != null && item is CombinedRing combined)
            {
                combined.combinedRings.Clear();
                foreach (StashItemRecord inner in record.InnerRings)
                {
                    if (CreateFromRecord(inner, monitor) is Ring ring)
                        combined.combinedRings.Add(ring);
                    else
                        monitor?.Log($"StashItemCodec: could not recreate inner ring '{inner?.ItemId}' of a Combined Ring.", LogLevel.Warn);
                }
            }
```

- [ ] **Step 3: Build and test.** `dotnet test TheLongestYear.sln -c Release`. Expected: build OK, all tests pass. Check in the decompile that `Microsoft.Xna.Framework.Color` has a `Color(uint packedValue)` constructor (MonoGame does); if the build says otherwise, use `new Color { PackedValue = record.Clothing.Color }`.

- [ ] **Step 4: Commit.** Bump to 0.18.122.

```bash
git add src/TheLongestYear/Loop/StashItemCodec.cs src/TheLongestYear/manifest.json
git commit -m "Stash keeps dye, boot tailoring, Combined Ring rings and trinket rolls (0.18.122)"
git push
```

---

### Task 5: Codec keeps container contents; legacy rescue, eject and overflow

**Files:**
- Modify: `src/TheLongestYear/Loop/StashItemCodec.cs` (contents, held object, mod item lists, `StripNonCosmetic`)
- Create: `src/TheLongestYear/Loop/JunimoStashService.Overflow.cs` (`TryDeposit`, `DropNearStash`, `RescueLegacyRecords`)
- Modify: `src/TheLongestYear/Loop/JunimoStashService.cs` (`PlaceChest` sweep calls the rescue; `PopulateFromMeta` trims and ejects)
- Modify: `src/TheLongestYear/manifest.json` (0.18.123)

**Interfaces:**
- Consumes: `StashNesting.Trim`, `StashNesting.IsCosmetic`, `StashLegacyRescue.ShouldReplace` (Task 3).
- Produces:
  - `internal static IList<Item> StashItemCodec.ContainerItems(Item item)`: `StorageFurniture.heldItems` (dressers, fish tanks, mod storage furniture), `Chest.Items` (a chest item a mod lets you carry), else for a type outside the game assembly the first public instance field or readable property whose value is an `IList<Item>`; null otherwise.
  - `internal static Item StashItemCodec.CreateFromRecord(StashItemRecord record, IMonitor monitor, List<Item> orphans)`: new overload. Nested items that have nowhere to go (the container type no longer holds items) are added to `orphans` instead of being lost. The Task 1 two-argument overload stays and forwards with a throwaway list it logs about.
  - `internal static void StashItemCodec.StripNonCosmetic(Item container)`: removes, on the live instance, every non-cosmetic nested item and held object (recursively). Used by Task 9 for outdoor storage furniture, which the farm wipe would otherwise have emptied anyway.
  - `internal Item JunimoStashService.TryDeposit(Item item)`: stacks onto matching stash stacks first, then uses a free slot under `MetaState.StashSlotCount`; returns the leftover (null when all of it went in). Writes `chest.Items` directly, never `Chest.addItem`, so no HUD message fires during a reset.
  - `internal void JunimoStashService.DropNearStash(Item item)`: `Game1.createItemDebris` one tile south of the stash chest (falls back to the farmhouse entry when no chest is placed).

Field names verified in the PC decompile: `StorageFurniture.heldItems` is `NetObjectList<Item>` (an `IList<Item>`); `FishTankFurniture : StorageFurniture`; `Object.heldObject` is `NetRef<Object>`; `Chest.Items` is an `Inventory` (`IList<Item>`).

- [ ] **Step 1: Contents in `ToRecord`.** Add before the `return`:

```csharp
            List<StashItemRecord> contents = null;
            IList<Item> held = ContainerItems(item);
            if (held != null && held.Any(i => i != null))
                contents = held.Where(i => i != null).Select(ToRecord).ToList();
            StashItemRecord heldObject = item is StardewValley.Object holder && holder.heldObject.Value != null
                ? ToRecord(holder.heldObject.Value)
                : null;
```

and pass `Contents: contents, HeldObject: heldObject`.

- [ ] **Step 2: `ContainerItems` and the mod-list lookup:**

```csharp
        // Mod item types that expose their own item list, resolved once per type.
        private static readonly Dictionary<System.Type, System.Reflection.MemberInfo> ModItemLists = new();

        internal static IList<Item> ContainerItems(Item item)
        {
            switch (item)
            {
                case StardewValley.Objects.StorageFurniture storage: return storage.heldItems;
                case StardewValley.Objects.Chest chest: return chest.Items;
            }
            return ModItemList(item);
        }

        // A mod bag: any public instance field or readable property holding an IList<Item>, on a type
        // the game assembly does not define. Game types are covered by the switch above.
        private static IList<Item> ModItemList(Item item)
        {
            System.Type type = item?.GetType();
            if (type == null || type.Assembly == typeof(Item).Assembly)
                return null;
            if (!ModItemLists.TryGetValue(type, out System.Reflection.MemberInfo member))
            {
                const System.Reflection.BindingFlags Public = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                member = (System.Reflection.MemberInfo)type.GetFields(Public).FirstOrDefault(f => typeof(IList<Item>).IsAssignableFrom(f.FieldType))
                    ?? type.GetProperties(Public).FirstOrDefault(p => p.CanRead && p.GetIndexParameters().Length == 0 && typeof(IList<Item>).IsAssignableFrom(p.PropertyType));
                ModItemLists[type] = member;
            }
            return member switch
            {
                System.Reflection.FieldInfo f => f.GetValue(item) as IList<Item>,
                System.Reflection.PropertyInfo p => p.GetValue(item) as IList<Item>,
                _ => null,
            };
        }
```

- [ ] **Step 3: Restore in `CreateFromRecord`.** Add the `orphans` overload; the two-argument form becomes:

```csharp
        internal static Item CreateFromRecord(StashItemRecord record, IMonitor monitor)
        {
            var orphans = new List<Item>();
            Item item = CreateFromRecord(record, monitor, orphans);
            if (orphans.Count > 0)
                monitor?.Log($"StashItemCodec: {orphans.Count} nested item(s) of '{record.ItemId}' had nowhere to go.", LogLevel.Warn);
            return item;
        }
```

The three-argument form is the Task 1/4 body, with nested calls passing `orphans` through, plus at the end before `return item;`:

```csharp
            if (record.Contents != null)
            {
                IList<Item> target = ContainerItems(item);
                foreach (StashItemRecord childRecord in record.Contents)
                {
                    Item child = CreateFromRecord(childRecord, monitor, orphans);
                    if (child == null)
                        monitor?.Log($"StashItemCodec: could not recreate '{childRecord?.ItemId}' inside '{record.ItemId}' (unknown id).", LogLevel.Warn);
                    else if (target != null)
                        target.Add(child);
                    else
                        orphans.Add(child);
                }
            }
            if (record.HeldObject != null)
            {
                Item heldItem = CreateFromRecord(record.HeldObject, monitor, orphans);
                if (item is StardewValley.Object holder && heldItem is StardewValley.Object heldObj)
                    holder.heldObject.Value = heldObj;
                else if (heldItem != null)
                    orphans.Add(heldItem);
            }
```

- [ ] **Step 4: `StripNonCosmetic`:**

```csharp
        /// <summary>Remove, on the live item, everything nested that is not cosmetic, at any depth.
        /// For decor kept on the farm (Keep Farm Decor): an outdoor dresser keeps its hats; its ring
        /// is wiped with the rest of the farm, exactly as a chest's contents are.</summary>
        internal static void StripNonCosmetic(Item container)
        {
            IList<Item> items = ContainerItems(container);
            if (items != null)
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    Item child = items[i];
                    if (child == null) continue;
                    if (StashNesting.IsCosmetic(child.QualifiedItemId))
                        StripNonCosmetic(child);
                    else
                        items.RemoveAt(i);
                }
            if (container is StardewValley.Object holder && holder.heldObject.Value is StardewValley.Object held)
            {
                if (StashNesting.IsCosmetic(held.QualifiedItemId))
                    StripNonCosmetic(held);
                else
                    holder.heldObject.Value = null;
            }
        }
```

- [ ] **Step 5: Create `JunimoStashService.Overflow.cs`:**

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    internal sealed partial class JunimoStashService
    {
        /// <summary>Put an item in the stash the way a player deposit would land, without the HUD:
        /// stack onto matching stacks first, then take a free slot under the slot cap. Returns what
        /// did not fit, null when everything went in.</summary>
        internal Item TryDeposit(Item item)
        {
            Chest chest = FindStashChest();
            if (chest == null || item == null)
                return item;

            foreach (Item slot in chest.Items)
            {
                if (slot == null || !slot.canStackWith(item)) continue;
                item.Stack = slot.addToStack(item);
                if (item.Stack <= 0)
                    return null;
            }

            if (chest.Items.Count(i => i != null) >= _meta.StashSlotCount)
                return item;
            int empty = chest.Items.IndexOf(null);
            if (empty >= 0)
                chest.Items[empty] = item;
            else
                chest.Items.Add(item);
            return null;
        }

        /// <summary>Drop an item one tile south of the stash chest (the spec's "next to the stash
        /// chest"), or at the farmhouse door if no chest is placed. Never deleted.</summary>
        internal void DropNearStash(Item item)
        {
            Farm farm = Game1.getFarm();
            if (farm == null || item == null)
                return;
            Vector2 tile = _placedTile ?? (TryGetFarmHouseEntry(farm) is Point door ? new Vector2(door.X, door.Y) : Vector2.Zero);
            Vector2 pixel = (tile + new Vector2(0f, 1f)) * 64f + new Vector2(32f, 32f);
            Game1.createItemDebris(item, pixel, -1, farm);
            _monitor.Log($"JunimoStashService: stash full, dropped '{item.QualifiedItemId}' x{item.Stack} by the stash at ({tile.X}, {tile.Y + 1}).", LogLevel.Info);
        }

        /// <summary>Before the stale chest from the save is swept: fill in banked records that a
        /// 0.18.118-or-earlier save wrote without contents or identity (see StashLegacyRescue). The
        /// save's chest and MetaState.StashItems are written by the same Saving event, so they list
        /// the same items in the same order; on any mismatch nothing is touched.</summary>
        private void RescueLegacyRecords(Chest stale)
        {
            List<Item> live = stale.Items.Where(i => i != null).ToList();
            if (live.Count != _meta.StashItems.Count)
            {
                _monitor.Log($"JunimoStashService: stale stash holds {live.Count} items, MetaState {_meta.StashItems.Count}; no legacy rescue.", LogLevel.Trace);
                return;
            }
            int rescued = 0;
            for (int i = 0; i < live.Count; i++)
            {
                StashItemRecord fresh = StashItemCodec.ToRecord(live[i]);
                if (!StashLegacyRescue.ShouldReplace(_meta.StashItems[i], fresh)) continue;
                _meta.StashItems[i] = fresh;
                rescued++;
            }
            if (rescued > 0)
                _monitor.Log($"JunimoStashService: filled in {rescued} stash record(s) written by an older version.", LogLevel.Info);
        }
    }
}
```

- [ ] **Step 6: Wire `PlaceChest`.** In the sweep loop of `PlaceChest`, next to `CollectCarriedModData(existing, carried);`, add `RescueLegacyRecords(existing);`. On a reset the fresh farm has no stale chest, so this only runs on save load and `tly_setstash`.

- [ ] **Step 7: Trim and eject in `PopulateFromMeta`.** Replace the loop from Task 1 with:

```csharp
            var overflow = new List<Item>();
            int ejectedCount = 0;
            foreach (StashItemRecord stored in _meta.StashItems)
            {
                // Saves from 0.18.118 or earlier can hold a container with non-cosmetic contents
                // (the deposit check is new). Never delete: keep the cosmetic contents nested and
                // take everything else out as its own stash entry, or onto the ground if full.
                var ejected = new List<StashItemRecord>();
                StashItemRecord record = StashNesting.Trim(stored, ejected);

                Item item = StashItemCodec.CreateFromRecord(record, _monitor, overflow);
                if (item == null)
                {
                    _monitor.Log(
                        $"JunimoStashService: could not recreate item '{record.ItemId}' (unknown id), skipping.",
                        LogLevel.Warn);
                    continue;
                }
                chest.Items.Add(item);
                restored++;

                foreach (StashItemRecord e in ejected)
                    if (StashItemCodec.CreateFromRecord(e, _monitor, overflow) is Item loose)
                    {
                        overflow.Add(loose);
                        ejectedCount++;
                    }
            }
            foreach (Item extra in overflow)
                if (TryDeposit(extra) is Item left)
                    DropNearStash(left);
            if (ejectedCount > 0)
                _monitor.Log($"JunimoStashService: took {ejectedCount} non-cosmetic item(s) out of stashed containers.", LogLevel.Info);
```

- [ ] **Step 8: Build and test.** `dotnet test TheLongestYear.sln -c Release`. All pass. Check `JunimoStashService.cs` stays under 400 lines.

- [ ] **Step 9: Commit.** Bump to 0.18.123.

```bash
git add src/TheLongestYear/Loop/StashItemCodec.cs src/TheLongestYear/Loop/JunimoStashService.Overflow.cs src/TheLongestYear/Loop/JunimoStashService.cs src/TheLongestYear/manifest.json
git commit -m "Stash keeps container contents; older stashed containers rescued, extras ejected (0.18.123)"
git push
```

---

### Task 6: Refuse a non-cosmetic container at deposit, plus the stash debug command

**Files:**
- Create: `src/TheLongestYear/Loop/JunimoStashNestingPatch.cs`
- Create: `src/TheLongestYear/Loop/StashNestingDebug.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (register `tly_stashnest` next to `tly_stashrod`, line ~383; add the bridge `case` next to `tly_additem`, line ~2541)
- Modify: `src/TheLongestYear/i18n/default.json` (`hud.stash-nesting-refused`, next to `hud.stash-full.other`)
- Modify: `src/TheLongestYear/manifest.json` (0.18.124)

**Interfaces:**
- Consumes: `StashItemCodec.ToRecord`, `StashNesting.NonCosmeticNested`, `JunimoStashService.FindStashChest`.
- Produces: `tly_stashnest <hats|ring|gear|legacy|fill|wear|worn|check>`, used by the Task 10 live checklist.

Insertion path verified in the PC decompile: every player deposit runs `ItemGrabMenu.receiveLeftClick/receiveRightClick` then `behaviorFunction` = `Chest.grabItemFromInventory` (Chest.cs:941), which calls `Chest.addItem` and, when that returns the item, puts it back with `who.addItemToInventory`. Fill Stacks only tops up existing stacks (furniture never stacks). So a prefix on `Chest.addItem` is the single choke point. When the prefix sets `__result = item` and skips the original, Harmony still runs `JunimoStashCapPatch`'s postfix, which returns at once because `__result != null`.

- [ ] **Step 1: Write the HUD string with the `game-writing` skill.** Invoke the skill. Brief it: a red HUD line shown when the player tries to put a dresser (or any container) into the Junimo Stash and something inside is not a hat, shirt, pants, furniture, wallpaper or flooring; the item stays in the inventory; one short line, no em dashes. Starting draft from the spec: "This container holds things that can't be stored in the stash. Take out everything except hats, shirts, pants, furniture, wallpaper and flooring, then try again." Add the result to `default.json`:

```json
    "hud.stash-nesting-refused": "<game-writing result>",
```

- [ ] **Step 2: Create `JunimoStashNestingPatch.cs`:**

```csharp
using System.Collections.Generic;
using HarmonyLib;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// The stash nesting rule at the moment of deposit (spec 2026-10-01): a container whose contents,
    /// at any depth, include anything that is not a hat, shirt, pants, furniture, wallpaper or
    /// flooring is handed back unchanged with a HUD line. Nothing is trimmed or deleted later.
    /// Prefix on Chest.addItem, the call every deposit path ends in (Chest.grabItemFromInventory).
    /// Returning the item as the result makes vanilla put it back in the inventory.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.addItem))]
    internal static class JunimoStashNestingPatch
    {
        // ReSharper disable once InconsistentNaming: Harmony convention.
        private static bool Prefix(Chest __instance, Item item, ref Item __result)
        {
            if (item == null || !__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return true;

            IReadOnlyList<string> blocked = StashNesting.NonCosmeticNested(StashItemCodec.ToRecord(item));
            if (blocked.Count == 0)
                return true;

            __result = item;
            Game1.showRedMessage(Strings.Get("hud.stash-nesting-refused"));
            PatchLog.Trace($"JunimoStashNestingPatch: refused '{item.QualifiedItemId}', holds {string.Join(", ", blocked)}.");
            return false;
        }
    }
}
```

- [ ] **Step 3: Create `StashNestingDebug.cs`** (static, called from ModEntry with its monitor and stash service). Modes:
  - `hats`: a dresser holding a hat, a dyed shirt and pants, deposited through `chest.addItem`; logs `accepted` or `refused` (a refused item goes back to the farmer's inventory).
  - `ring`: the same dresser plus `(O)529`; expect `refused`.
  - `gear`: deposits dyed pants, tailored boots, a Combined Ring and a trinket through `chest.addItem`.
  - `legacy`: puts a dresser holding a hat, shirt, pants and `(O)529` straight into `chest.Items` (bypassing the deposit check), standing in for a 0.18.118 save.
  - `fill`: fills free stash slots with hats through `TryDeposit` until the stash is full.
  - `wear`: equips tailored boots, a Combined Ring (left), an Amethyst Ring (right) and a trinket on the farmer (sets `trinketSlots` to 1 first, as the Combat mastery claim does).
  - `worn`: logs what the farmer wears, one identity line each.
  - `check` (default): one identity line per stash item.
  The dresser is found at runtime (first `(F)` item that `is StorageFurniture and not FishTankFurniture`), so no furniture id is hard-coded. If any other id below does not exist in 1.6 (`ItemRegistry.Exists`), swap in another of the same type and note it in the commit.

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace TheLongestYear.Loop
{
    /// <summary>tly_stashnest: drive the stash nesting rule and the identity round trip from the
    /// debug bridge (spec 2026-10-01 live checks). Developer-only.</summary>
    internal static class StashNestingDebug
    {
        private static readonly Color DyeRed = new(200, 40, 40);
        private const int FillHatCount = 60;
        private const string TrinketSlotsStat = "trinketSlots";

        public static void Run(string[] args, IMonitor monitor, JunimoStashService stash)
        {
            Chest chest = stash?.FindStashChest();
            if (chest == null) { monitor.Log("tly_stashnest: no stash chest. Own stash_1 and reload first.", LogLevel.Warn); return; }
            string mode = args.Length > 0 ? args[0] : "check";
            switch (mode)
            {
                case "hats": Deposit(chest, Dresser(monitor, withRing: false), monitor); break;
                case "ring": Deposit(chest, Dresser(monitor, withRing: true), monitor); break;
                case "gear": foreach (Item i in Gear()) Deposit(chest, i, monitor); break;
                case "legacy":
                    chest.Items.Add(Dresser(monitor, withRing: true));
                    monitor.Log("tly_stashnest: legacy dresser (hat, shirt, pants, ring) placed straight in the stash.", LogLevel.Info);
                    break;
                case "fill": Fill(stash, monitor); break;
                case "wear": Wear(monitor); break;
                case "worn": LogWorn(monitor); break;
                default:
                    foreach (Item i in chest.Items.Where(i => i != null))
                        monitor.Log("tly_stashnest: " + Describe(i), LogLevel.Info);
                    break;
            }
        }

        private static void Deposit(Chest chest, Item item, IMonitor monitor)
        {
            if (item == null) return;
            Item back = chest.addItem(item);
            if (back != null) Game1.player.addItemToInventory(back);
            monitor.Log($"tly_stashnest: {(back == null ? "accepted" : "refused")} {Describe(item)}", LogLevel.Info);
        }

        private static Item Dresser(IMonitor monitor, bool withRing)
        {
            StorageFurniture dresser = null;
            foreach (string key in DataLoader.Furniture(Game1.content).Keys)
                if (ItemRegistry.Create("(F)" + key, allowNull: true) is StorageFurniture sf && sf is not FishTankFurniture)
                {
                    dresser = sf;
                    break;
                }
            if (dresser == null) { monitor.Log("tly_stashnest: no dresser in Data/Furniture.", LogLevel.Warn); return null; }
            monitor.Log($"tly_stashnest: using dresser {dresser.QualifiedItemId}.", LogLevel.Info);
            dresser.heldItems.Add(ItemRegistry.Create("(H)0"));
            var shirt = ItemRegistry.Create<Clothing>("(S)1000");
            shirt.clothesColor.Value = DyeRed;
            dresser.heldItems.Add(shirt);
            dresser.heldItems.Add(ItemRegistry.Create("(P)0"));
            if (withRing) dresser.heldItems.Add(ItemRegistry.Create("(O)529"));
            return dresser;
        }

        private static List<Item> Gear()
        {
            var pants = ItemRegistry.Create<Clothing>("(P)0");
            pants.clothesColor.Value = DyeRed;
            var boots = ItemRegistry.Create<Boots>("(B)504");               // Sneakers...
            boots.applyStats(ItemRegistry.Create<Boots>("(B)514"));       // ...tailored with Space Boots
            var ring = ItemRegistry.Create<CombinedRing>("(O)880");
            ring.combinedRings.Add(ItemRegistry.Create<Ring>("(O)529"));
            ring.combinedRings.Add(ItemRegistry.Create<Ring>("(O)530"));
            var trinket = ItemRegistry.Create<Trinket>("(TR)ParrotEgg");
            return new List<Item> { pants, boots, ring, trinket };
        }

        private static void Fill(JunimoStashService stash, IMonitor monitor)
        {
            int added = 0;
            for (int i = 0; i < FillHatCount; i++)
            {
                Item hat = ItemRegistry.Create("(H)" + i, allowNull: true);
                if (hat == null) continue;
                if (stash.TryDeposit(hat) != null) break;
                added++;
            }
            monitor.Log($"tly_stashnest: filled {added} slot(s); the stash is full.", LogLevel.Info);
        }

        private static void Wear(IMonitor monitor)
        {
            Farmer p = Game1.player;
            List<Item> gear = Gear();
            p.Equip((Boots)gear[1], p.boots);
            p.Equip((Ring)gear[2], p.leftRing);
            p.Equip(ItemRegistry.Create<Ring>("(O)529"), p.rightRing);
            p.stats.Set(TrinketSlotsStat, 1);
            p.trinketItems.Add((Trinket)gear[3]);
            LogWorn(monitor);
        }

        private static void LogWorn(IMonitor monitor)
        {
            Farmer p = Game1.player;
            var worn = new List<Item> { p.boots.Value, p.leftRing.Value, p.rightRing.Value };
            worn.AddRange(p.trinketItems);
            monitor.Log($"tly_stashnest: worn {string.Join(" | ", worn.Select(Describe))}; trinketSlots={p.stats.Get(TrinketSlotsStat)}.", LogLevel.Info);
        }

        private static string Describe(Item item)
        {
            if (item == null) return "none";
            var parts = new List<string> { item.QualifiedItemId };
            if (item is Clothing c) parts.Add($"colour={c.clothesColor.Value.PackedValue}");
            if (item is Boots b) parts.Add($"boots={b.appliedBootSheetIndex.Value}/{b.defenseBonus.Value}/{b.immunityBonus.Value}");
            if (item is CombinedRing r) parts.Add($"rings=[{string.Join(",", r.combinedRings.Select(x => x.QualifiedItemId))}]");
            if (item is Trinket t) parts.Add($"seed={t.generationSeed.Value}");
            if (StashItemCodec.ContainerItems(item) is IList<Item> inside && inside.Any(i => i != null))
                parts.Add($"contents=[{string.Join(", ", inside.Where(i => i != null).Select(Describe))}]");
            return string.Join(" ", parts);
        }
    }
}
```

- [ ] **Step 4: Register in `ModEntry.cs`.** After the `tly_stashrod` registration:

```csharp
            helper.ConsoleCommands.Add("tly_stashnest",
                "Debug: exercise the stash nesting rule and item identity. Usage: tly_stashnest <hats|ring|gear|legacy|fill|wear|worn|check>",
                (cmd, a) => { if (Context.IsWorldReady) StashNestingDebug.Run(a, this.Monitor, _stashService); });
```

and in the bridge switch: `case "tly_stashnest": if (Context.IsWorldReady) StashNestingDebug.Run(args, this.Monitor, _stashService); break;`

- [ ] **Step 5: Build and test.** `dotnet test TheLongestYear.sln -c Release`. `I18nGuardTests` must pass (the new key is reachable from the literal `Strings.Get`).

- [ ] **Step 6: Commit.** Bump to 0.18.124.

```bash
git add src/TheLongestYear/Loop/JunimoStashNestingPatch.cs src/TheLongestYear/Loop/StashNestingDebug.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/manifest.json
git commit -m "Stash refuses a container holding anything but hats, clothes, furniture or wallpaper (0.18.124)"
git push
```

---

### Task 7: Keep Worn Gear

**Files:**
- Create: `src/TheLongestYear.Core/WornGearKeep.cs`
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs` (row after `keep_golden_scythe`, line ~89)
- Modify: `src/TheLongestYear.Core/RunBaseline.cs` (`KeepWornGear`)
- Modify: `src/TheLongestYear.Core/RunBaselineBuilder.cs` (set it, line ~202)
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs` (lines 72-88 and the stat wipe at ~103)
- Modify: `src/TheLongestYear/i18n/default.json` (name + desc after `upgrade.keep_golden_scythe.desc`)
- Test: `tests/TheLongestYear.Tests/WornGearKeepTests.cs`
- Modify: `src/TheLongestYear/manifest.json` (0.18.125)

**Interfaces:**
- Produces: `WornGearKeep.UpgradeId = "keep_worn_gear"`, `WornGearKeep.Cost = 1000`, `WornGearKeep.TrinketSlotsStat = "trinketSlots"`; `RunBaseline.KeepWornGear` (bool, init).

- [ ] **Step 1: Write the failing tests:**

```csharp
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class WornGearKeepTests
{
    [Fact]
    public void Catalog_row_is_one_level_1000_jp_in_loadout()
    {
        UpgradeDefinition row = UpgradeCatalog.All.Single(u => u.Id == WornGearKeep.UpgradeId);
        Assert.Equal(1000L, row.Cost);
        Assert.Equal(UpgradeCategory.Loadout, row.Category);
        Assert.Null(row.PrerequisiteId);
        Assert.Null(row.RunReachRequirement);
        Assert.Equal(1, UpgradeCatalog.All.Count(u => u.Id.StartsWith("keep_worn_gear")));
    }

    [Fact]
    public void Baseline_keeps_worn_gear_only_when_owned()
    {
        var owned = new MetaState { OwnedUpgrades = { WornGearKeep.UpgradeId } };
        Assert.True(RunBaselineBuilder.Build(owned, new RunState(), PlayerSnapshot.Empty, 500).KeepWornGear);
        Assert.False(RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500).KeepWornGear);
    }
}
```

- [ ] **Step 2: Run, expect FAIL** (`WornGearKeep` missing). `dotnet test TheLongestYear.sln -c Release --filter WornGearKeepTests`

- [ ] **Step 3: Write the name and description with the `game-writing` skill.** Brief: Junimo Upgrades row, Loadout tab, beside "Keep Golden Scythe" ("Start each loop with the Golden Scythe instead of the basic scythe."). Effect: the boots, both rings and the trinket you are wearing when the year rewinds stay on you, exactly as they were (a Combined Ring keeps both rings, tailored boots keep their stats, a trinket keeps its rolled stats). Spare rings and boots still need stash slots. Name stays "Keep Worn Gear" (spec). Draft desc: "The boots, rings and trinket you're wearing when the year rewinds stay on you." Add both keys to `default.json` after `upgrade.keep_golden_scythe.desc`.

- [ ] **Step 4: Implement Core.** `WornGearKeep.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Keep Worn Gear (spec 2026-10-01, promised to sarahwinchester97): the boots, both rings
/// and the trinket(s) worn at the rewind stay worn. The Farmer object survives the in-place reset,
/// so FarmerReset just leaves the slots alone and the very instances (Combined Rings, tailored boots,
/// trinket stats) carry over. Only what is worn: spare rings and boots still need stash slots.</summary>
public static class WornGearKeep
{
    public const string UpgradeId = "keep_worn_gear";
    public const long Cost = 1000;

    /// <summary>The stat that shows the trinket slot (vanilla sets it to 1 at the Combat mastery
    /// claim). StatResetRules wipes it each loop; a kept trinket needs it back or it is worn but
    /// invisible and cannot be taken off.</summary>
    public const string TrinketSlotsStat = "trinketSlots";
}
```

`UpgradeCatalog.cs`, after the `keep_golden_scythe` row:

```csharp
        // Keep Worn Gear (spec 2026-10-01, sarahwinchester97): worn boots, rings and trinkets stay
        // on through the rewind. Priced well above Keep Golden Scythe: rings and trinkets are power.
        new UpgradeDefinition(WornGearKeep.UpgradeId, UpgradeCategory.Loadout, WornGearKeep.Cost),
```

`RunBaseline.cs` (after `GrantGoldenScythe`):

```csharp
    /// <summary>Keep Worn Gear owned: FarmerReset leaves boots, rings and trinkets equipped.</summary>
    public bool KeepWornGear { get; init; }
```

`RunBaselineBuilder.cs`, in the object initializer after `GrantGoldenScythe = ...`: `KeepWornGear = meta.HasUpgrade(WornGearKeep.UpgradeId),`

- [ ] **Step 5: Implement FarmerReset.** Wrap lines 81-88 (the three `p.Equip` calls and the trinket loop + Clear) so they run only without the upgrade, and remember the trinket slot:

```csharp
            // Keep Worn Gear (spec 2026-10-01): the owner keeps the worn instances as they are.
            bool keptTrinket = baseline.KeepWornGear && p.trinketItems.Any(t => t != null);
            if (!baseline.KeepWornGear)
            {
                p.Equip<StardewValley.Objects.Boots>(null, p.boots);
                p.Equip<StardewValley.Objects.Ring>(null, p.leftRing);
                p.Equip<StardewValley.Objects.Ring>(null, p.rightRing);
                // Trinkets unequip by index assignment: that fires OnTrinketChange then
                // Trinket.Unapply, the same path the inventory page uses; then the list is cleared.
                for (int i = 0; i < p.trinketItems.Count; i++)
                    p.trinketItems[i] = null;
                p.trinketItems.Clear();
            }
            uint keptTrinketSlots = keptTrinket ? p.stats.Get(WornGearKeep.TrinketSlotsStat) : 0;
```

Keep the existing explanatory comment above the block, and add one sentence to it: "Keep Worn Gear skips this block." Then right after the `StatResetRules.SelectRunScoped` removal loop:

```csharp
            // A kept trinket needs its slot visible, or it is worn and cannot be taken off.
            if (keptTrinketSlots > 0)
                p.stats.Set(WornGearKeep.TrinketSlotsStat, keptTrinketSlots);
```

- [ ] **Step 6: Run everything, expect PASS.** `dotnet test TheLongestYear.sln -c Release` (`UpgradeCatalogI18nTests.EveryCatalogRow_ResolvesNameAndDescription` and `I18nGuardTests` cover the new keys).

- [ ] **Step 7: Commit.** Bump to 0.18.125.

```bash
git add src/TheLongestYear.Core/WornGearKeep.cs src/TheLongestYear.Core/UpgradeCatalog.cs src/TheLongestYear.Core/RunBaseline.cs src/TheLongestYear.Core/RunBaselineBuilder.cs src/TheLongestYear/Loop/FarmerReset.cs src/TheLongestYear/i18n/default.json tests/TheLongestYear.Tests/WornGearKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "Keep Worn Gear: worn boots, rings and trinket stay on through the rewind (0.18.125)"
git push
```

---

### Task 8: Farm decor classifier and debris decision table (Core)

**Files:**
- Create: `src/TheLongestYear.Core/FarmDecorTypes.cs`
- Create: `src/TheLongestYear.Core/FarmDecorKeep.cs`
- Create: `src/TheLongestYear.Core/FarmDecorPlanner.cs`
- Test: `tests/TheLongestYear.Tests/FarmDecorKeepTests.cs`
- Modify: `src/TheLongestYear/manifest.json` (0.18.126)

**Interfaces:**
- Produces (Core):
  - `enum FarmThingKind { Flooring, Fence, Torch, Sign, Furniture, BigCraftable, Object }`
  - `enum DecorLayer { Ground, Object }`: Ground = a path or floor (terrain feature); Object = objects and furniture.
  - `[Flags] enum TileBlock { None = 0, OffMap = 1, Building = 2, OtherObject = 4, SmallDebris = 8 }`
  - `enum DecorTool { Axe, Pickaxe }`
  - `readonly record struct DecorTile(int X, int Y)`
  - `sealed record DecorPiece(int Id, DecorLayer Layer, IReadOnlyList<DecorTile> Tiles)`
  - `sealed record DecorClump(int Id, int Index, IReadOnlyList<DecorTile> Tiles)`
  - `sealed record ClumpRule(DecorTool Tool, int MinTier, int HardwoodDrop)`
  - `sealed record ClearedClump(int ClumpId, int HardwoodDrop)`
  - `sealed record DecorPlan(IReadOnlyList<int> Placed, IReadOnlyList<int> Displaced, IReadOnlyList<ClearedClump> ClearedClumps, IReadOnlyList<DecorTile> DebrisTilesToClear)`
  - `FarmDecorKeep.UpgradeId = "keep_farm_decor"`, `FarmDecorKeep.Cost = 500`, `FarmDecorKeep.HardwoodId = "(O)709"`
  - `bool FarmDecorKeep.IsKeptDecor(FarmThingKind kind, string qualifiedItemId)`
  - `ClumpRule? FarmDecorKeep.RuleFor(int clumpIndex)`, `bool FarmDecorKeep.CanBreak(int clumpIndex, int axeTier, int pickaxeTier)`
  - `DecorPlan FarmDecorPlanner.Plan(IReadOnlyList<DecorPiece> pieces, IReadOnlyList<DecorClump> clumps, Func<int, int, TileBlock> blockAt, int axeTier, int pickaxeTier)`

Rules from the spec and the decompile (`ResourceClump.performToolAction`, ResourceClump.cs:130-200; `destroy`, :236-290):

| Clump | Index | Breaks with | Drops on clear |
|---|---|---|---|
| Stump | 600 | axe tier 1 (copper) | 2 Hardwood |
| Hollow log | 602 | axe tier 2 (steel) | 8 Hardwood |
| Boulder | 672 | pickaxe tier 2 (steel) | nothing (Jeff: no stone) |
| Meteorite, quarry boulder | 622, 148 | pickaxe tier 3 | nothing |
| Mine rocks | 752, 754, 756, 758 | any pickaxe | nothing |
| anything else (giant crops, mod clumps) | | never | |

Decor classifier: Flooring, Fence (incl. gates), Torch (the `Torch` class: torches, spirit torches, every brazier and campfire) and Sign (wood/stone/dark signs) are always decor; Furniture is decor unless it is one of TLY's own (`StashNesting.ModItemPrefix`); a BigCraftable is decor only if it is in `DecorBigCraftableIds` below; a plain Object is never decor. Machines, sprinklers, scarecrows and rarecrows, chests, the Garden Pot and functional statues are absent from the table on purpose.

- [ ] **Step 1: Write the failing tests** (`tests/TheLongestYear.Tests/FarmDecorKeepTests.cs`):

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FarmDecorKeepTests
{
    [Theory]
    [InlineData(FarmThingKind.Flooring, "(FL)ignored")]
    [InlineData(FarmThingKind.Fence, "(O)322")]
    [InlineData(FarmThingKind.Fence, "(O)325")]          // gate
    [InlineData(FarmThingKind.Torch, "(O)93")]
    [InlineData(FarmThingKind.Torch, "(BC)146")]         // campfire
    [InlineData(FarmThingKind.Sign, "(BC)37")]
    [InlineData(FarmThingKind.Furniture, "(F)1120")]
    [InlineData(FarmThingKind.BigCraftable, "(BC)152")]  // wood lamp-post
    [InlineData(FarmThingKind.BigCraftable, "(BC)153")]  // iron lamp-post
    [InlineData(FarmThingKind.BigCraftable, "(BC)108")]  // tub o' flowers
    [InlineData(FarmThingKind.BigCraftable, "(BC)TextSign")]
    public void Spec_decor_is_kept(FarmThingKind kind, string id) => Assert.True(FarmDecorKeep.IsKeptDecor(kind, id));

    [Theory]
    [InlineData(FarmThingKind.BigCraftable, "(BC)12")]   // keg (machine)
    [InlineData(FarmThingKind.BigCraftable, "(BC)8")]    // scarecrow
    [InlineData(FarmThingKind.BigCraftable, "(BC)110")]  // rarecrow
    [InlineData(FarmThingKind.BigCraftable, "(BC)130")]  // chest
    [InlineData(FarmThingKind.BigCraftable, "(BC)62")]   // garden pot (grows crops)
    [InlineData(FarmThingKind.BigCraftable, "(BC)127")]  // statue of endless fortune (produces)
    [InlineData(FarmThingKind.Object, "(O)599")]         // sprinkler
    [InlineData(FarmThingKind.Object, "(O)24")]          // a crop item left on the ground
    [InlineData(FarmThingKind.Furniture, "(F)sonofskywalker3.TheLongestYear_PlanningShrine")]
    public void Machines_crops_sprinklers_scarecrows_chests_are_never_kept(FarmThingKind kind, string id)
        => Assert.False(FarmDecorKeep.IsKeptDecor(kind, id));

    // ----- clump rules: tool tier x debris type -----

    [Theory]
    [InlineData(600, 0, 0, false)]
    [InlineData(600, 1, 0, true)]     // copper axe: stump
    [InlineData(602, 1, 4, false)]
    [InlineData(602, 2, 0, true)]     // steel axe: hollow log
    [InlineData(672, 4, 1, false)]
    [InlineData(672, 0, 2, true)]     // steel pickaxe: boulder
    [InlineData(622, 0, 2, false)]
    [InlineData(622, 0, 3, true)]
    [InlineData(752, 0, 0, true)]
    [InlineData(44, 4, 4, false)]     // unknown clump: never broken
    public void CanBreak_follows_the_kept_tool_tier(int index, int axe, int pick, bool expected)
        => Assert.Equal(expected, FarmDecorKeep.CanBreak(index, axe, pick));

    [Theory]
    [InlineData(600, 2)]
    [InlineData(602, 8)]
    [InlineData(672, 0)]
    public void Hardwood_drops_match_vanilla(int index, int hardwood)
        => Assert.Equal(hardwood, FarmDecorKeep.RuleFor(index)!.HardwoodDrop);

    // ----- planner -----

    private static IReadOnlyList<DecorTile> T(params (int x, int y)[] tiles) => tiles.Select(t => new DecorTile(t.x, t.y)).ToList();
    private static DecorClump Clump(int id, int index, int x, int y) => new(id, index, T((x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)));
    private static TileBlock Open(int x, int y) => TileBlock.None;

    [Fact]
    public void Path_under_a_stump_with_a_copper_axe_clears_it_for_two_hardwood()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((10, 10)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 10, 10) }, Open, axeTier: 1, pickaxeTier: 0);
        Assert.Equal(new[] { 1 }, plan.Placed);
        Assert.Equal(new[] { new ClearedClump(7, 2) }, plan.ClearedClumps);
    }

    [Fact]
    public void Path_under_a_stump_with_the_basic_axe_goes_to_the_stash_and_the_stump_stays()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((10, 10)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 10, 10) }, Open, axeTier: 0, pickaxeTier: 0);
        Assert.Empty(plan.Placed);
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.ClearedClumps);
    }

    [Fact]
    public void Boulder_cleared_with_a_steel_pickaxe_drops_nothing()
    {
        var fence = new DecorPiece(1, DecorLayer.Object, T((5, 6)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { fence }, new[] { Clump(3, 672, 5, 5) }, Open, 0, 2);
        Assert.Equal(new[] { new ClearedClump(3, 0) }, plan.ClearedClumps);
    }

    [Fact]
    public void Clumps_away_from_decor_are_left_alone()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((0, 0)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 20, 20) }, Open, 4, 4);
        Assert.Empty(plan.ClearedClumps);
        Assert.Equal(new[] { 1 }, plan.Placed);
    }

    [Fact]
    public void One_clump_under_two_pieces_is_cleared_once()
    {
        var a = new DecorPiece(1, DecorLayer.Ground, T((10, 10)));
        var b = new DecorPiece(2, DecorLayer.Ground, T((11, 11)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { a, b }, new[] { Clump(7, 600, 10, 10) }, Open, 1, 0);
        Assert.Single(plan.ClearedClumps);
        Assert.Equal(new[] { 1, 2 }, plan.Placed);
    }

    [Fact]
    public void A_breakable_clump_is_not_cleared_for_a_piece_the_stash_takes_anyway()
    {
        // A 2x1 bench over a stump (breakable) and a hollow log (not breakable with a copper axe).
        var bench = new DecorPiece(1, DecorLayer.Object, T((10, 10), (12, 10)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { bench },
            new[] { Clump(7, 600, 10, 10), Clump(8, 602, 12, 10) }, Open, axeTier: 1, pickaxeTier: 0);
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.ClearedClumps);
    }

    [Fact]
    public void A_kept_building_wins_over_decor()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((3, 3)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new DecorClump[0],
            (x, y) => x == 3 && y == 3 ? TileBlock.Building : TileBlock.None, 0, 0);
        Assert.Equal(new[] { 1 }, plan.Displaced);
    }

    [Fact]
    public void Off_map_tiles_send_decor_to_the_stash()
    {
        var torch = new DecorPiece(1, DecorLayer.Object, T((-1, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { torch }, new DecorClump[0],
            (x, y) => x < 0 ? TileBlock.OffMap : TileBlock.None, 0, 0);
        Assert.Equal(new[] { 1 }, plan.Displaced);
    }

    [Fact]
    public void Another_object_blocks_an_object_but_not_a_path_under_it()
    {
        // e.g. the stash chest or planning shrine now stands on that tile.
        var path = new DecorPiece(1, DecorLayer.Ground, T((4, 4)));
        var sign = new DecorPiece(2, DecorLayer.Object, T((4, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path, sign }, new DecorClump[0],
            (x, y) => TileBlock.OtherObject, 0, 0);
        Assert.Equal(new[] { 1 }, plan.Placed);
        Assert.Equal(new[] { 2 }, plan.Displaced);
    }

    [Fact]
    public void Small_debris_under_kept_decor_is_listed_for_clearing_once()
    {
        // Weeds, twigs, stones, grass, saplings, trees and bushes all report SmallDebris.
        var path = new DecorPiece(1, DecorLayer.Ground, T((4, 4)));
        var fence = new DecorPiece(2, DecorLayer.Object, T((4, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path, fence }, new DecorClump[0],
            (x, y) => TileBlock.SmallDebris, 0, 0);
        Assert.Equal(new[] { 1, 2 }, plan.Placed);
        Assert.Equal(new[] { new DecorTile(4, 4) }, plan.DebrisTilesToClear);
    }

    [Fact]
    public void Debris_under_displaced_decor_is_left_alone()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((4, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new DecorClump[0],
            (x, y) => TileBlock.SmallDebris | TileBlock.Building, 0, 0);
        Assert.Empty(plan.DebrisTilesToClear);
    }
}
```

- [ ] **Step 2: Run, expect FAIL.** `dotnet test TheLongestYear.sln -c Release --filter FarmDecorKeepTests`

- [ ] **Step 3: Implement.** `FarmDecorTypes.cs` holds the enums and records from the Interfaces block, each with a one-line doc comment. `FarmDecorKeep.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Keep Farm Decor (spec 2026-10-01): paths and flooring, fences and gates, lamp-posts, torches,
/// braziers, signs, outdoor furniture and placed decorations come back on the same farm tiles.
/// Never kept: machines, crops, sprinklers, scarecrows, chests. The glue is
/// FarmDecorCarryoverService; the conflict rules are FarmDecorPlanner.
/// </summary>
public static class FarmDecorKeep
{
    public const string UpgradeId = "keep_farm_decor";
    public const long Cost = 500;
    public const string HardwoodId = "(O)709";

    // Vanilla ResourceClump indices (ResourceClump.cs constants).
    public const int StumpIndex = 600;
    public const int HollowLogIndex = 602;
    public const int MeteoriteIndex = 622;
    public const int BoulderIndex = 672;
    public const int QuarryBoulderIndex = 148;

    // Tool tiers as Tool.UpgradeLevel (RunBaseline.ToolTiers): 1 copper, 2 steel, 3 gold.
    private const int Copper = 1, Steel = 2, Gold = 3, Basic = 0;

    // Hardwood from ResourceClump.destroy without the Lumberjack bonus; boulders give no stone (Jeff).
    private const int StumpHardwood = 2, HollowLogHardwood = 8, NoDrop = 0;

    private static readonly IReadOnlyDictionary<int, ClumpRule> ClumpRules = new Dictionary<int, ClumpRule>
    {
        [StumpIndex] = new(DecorTool.Axe, Copper, StumpHardwood),
        [HollowLogIndex] = new(DecorTool.Axe, Steel, HollowLogHardwood),
        [BoulderIndex] = new(DecorTool.Pickaxe, Steel, NoDrop),
        [MeteoriteIndex] = new(DecorTool.Pickaxe, Gold, NoDrop),
        [QuarryBoulderIndex] = new(DecorTool.Pickaxe, Gold, NoDrop),
        [752] = new(DecorTool.Pickaxe, Basic, NoDrop),
        [754] = new(DecorTool.Pickaxe, Basic, NoDrop),
        [756] = new(DecorTool.Pickaxe, Basic, NoDrop),
        [758] = new(DecorTool.Pickaxe, Basic, NoDrop),
    };

    /// <summary>Decorative big craftables (Data/BigCraftables, 1.6, checked against the game's
    /// patch export): no machine data, no crop, no scarecrow, no storage, no warp or shop.</summary>
    private static readonly HashSet<string> DecorBigCraftableIds = new(StringComparer.Ordinal)
    {
        "(BC)0", "(BC)1", "(BC)2", "(BC)3", "(BC)4", "(BC)5", "(BC)6", "(BC)7",   // house plants
        "(BC)22", "(BC)23", "(BC)26", "(BC)27", "(BC)28", "(BC)29", "(BC)31", "(BC)32", "(BC)33",
        "(BC)34", "(BC)35", "(BC)36", "(BC)40", "(BC)41", "(BC)42", "(BC)43", "(BC)44", "(BC)45",
        "(BC)46", "(BC)47", "(BC)48", "(BC)52", "(BC)53", "(BC)54", "(BC)55", "(BC)56",
        "(BC)64", "(BC)65", "(BC)66", "(BC)67", "(BC)68", "(BC)69", "(BC)70", "(BC)72", "(BC)73",
        "(BC)74", "(BC)75", "(BC)76", "(BC)78", "(BC)79", "(BC)80", "(BC)83", "(BC)84", "(BC)85",
        "(BC)86", "(BC)87", "(BC)88", "(BC)89", "(BC)94", "(BC)95", "(BC)98", "(BC)107", "(BC)108",
        "(BC)111", "(BC)112", "(BC)116", "(BC)117", "(BC)118", "(BC)119", "(BC)120", "(BC)121",
        "(BC)122", "(BC)123", "(BC)124", "(BC)125", "(BC)141", "(BC)152", "(BC)153", "(BC)155",
        "(BC)159", "(BC)161", "(BC)162", "(BC)164", "(BC)174", "(BC)175", "(BC)184", "(BC)188",
        "(BC)192", "(BC)196", "(BC)200", "(BC)204", "(BC)219", "(BC)262", "(BC)263", "(BC)TextSign",
    };

    public static ClumpRule? RuleFor(int clumpIndex)
        => ClumpRules.TryGetValue(clumpIndex, out ClumpRule? rule) ? rule : null;

    public static bool CanBreak(int clumpIndex, int axeTier, int pickaxeTier)
    {
        ClumpRule? rule = RuleFor(clumpIndex);
        if (rule == null)
            return false;
        int tier = rule.Tool == DecorTool.Axe ? axeTier : pickaxeTier;
        return tier >= rule.MinTier;
    }

    public static bool IsKeptDecor(FarmThingKind kind, string qualifiedItemId) => kind switch
    {
        FarmThingKind.Flooring or FarmThingKind.Fence or FarmThingKind.Torch or FarmThingKind.Sign => true,
        FarmThingKind.Furniture => !IsOwnItem(qualifiedItemId),
        FarmThingKind.BigCraftable => DecorBigCraftableIds.Contains(qualifiedItemId ?? ""),
        _ => false,
    };

    private static bool IsOwnItem(string? qualifiedItemId)
        => qualifiedItemId != null && qualifiedItemId.Contains(StashNesting.ModItemPrefix, StringComparison.Ordinal);
}
```

`FarmDecorPlanner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Where kept decor goes on the fresh farm (spec 2026-10-01, "Conflicts on the fresh farm").
/// A piece goes to the stash when a tile is off the map or under a building (kept buildings are
/// placed first and win), or, for an object-layer piece, when another object or furniture now
/// stands there (a path may run under an object). Large debris touching a still-placed piece is
/// cleared if the KEPT tool tier breaks it, else every piece it touches goes to the stash.
/// Unbreakable clumps are resolved first so a breakable clump is never cleared for a piece the
/// stash takes anyway. Large debris elsewhere is never touched. Small debris under placed pieces
/// is listed for clearing (no drops).
/// </summary>
public static class FarmDecorPlanner
{
    private const TileBlock AlwaysBlocks = TileBlock.OffMap | TileBlock.Building;
    private const TileBlock ObjectLayerBlocks = AlwaysBlocks | TileBlock.OtherObject;

    public static DecorPlan Plan(IReadOnlyList<DecorPiece> pieces, IReadOnlyList<DecorClump> clumps,
        Func<int, int, TileBlock> blockAt, int axeTier, int pickaxeTier)
    {
        var displaced = new HashSet<int>();
        foreach (DecorPiece piece in pieces)
        {
            TileBlock blocking = piece.Layer == DecorLayer.Object ? ObjectLayerBlocks : AlwaysBlocks;
            if (piece.Tiles.Any(t => (blockAt(t.X, t.Y) & blocking) != 0))
                displaced.Add(piece.Id);
        }

        foreach (DecorClump clump in clumps.Where(c => !FarmDecorKeep.CanBreak(c.Index, axeTier, pickaxeTier)))
            foreach (DecorPiece piece in pieces)
                if (!displaced.Contains(piece.Id) && Overlaps(piece, clump))
                    displaced.Add(piece.Id);

        var cleared = new List<ClearedClump>();
        foreach (DecorClump clump in clumps.Where(c => FarmDecorKeep.CanBreak(c.Index, axeTier, pickaxeTier)))
            if (pieces.Any(p => !displaced.Contains(p.Id) && Overlaps(p, clump)))
                cleared.Add(new ClearedClump(clump.Id, FarmDecorKeep.RuleFor(clump.Index)!.HardwoodDrop));

        List<DecorPiece> placed = pieces.Where(p => !displaced.Contains(p.Id)).ToList();
        List<DecorTile> debris = placed.SelectMany(p => p.Tiles)
            .Where(t => (blockAt(t.X, t.Y) & TileBlock.SmallDebris) != 0)
            .Distinct()
            .ToList();

        return new DecorPlan(
            placed.Select(p => p.Id).ToList(),
            pieces.Where(p => displaced.Contains(p.Id)).Select(p => p.Id).ToList(),
            cleared,
            debris);
    }

    private static bool Overlaps(DecorPiece piece, DecorClump clump)
        => piece.Tiles.Any(t => clump.Tiles.Contains(t));
}
```

- [ ] **Step 4: Run, expect PASS.** `dotnet test TheLongestYear.sln -c Release`

- [ ] **Step 5: Commit.** Bump to 0.18.126.

```bash
git add src/TheLongestYear.Core/FarmDecorTypes.cs src/TheLongestYear.Core/FarmDecorKeep.cs src/TheLongestYear.Core/FarmDecorPlanner.cs tests/TheLongestYear.Tests/FarmDecorKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "Keep Farm Decor rules: decor classifier and debris decision table (0.18.126)"
git push
```

---

### Task 9: Keep Farm Decor in the rewind (snapshot, restore, catalog row)

**Files:**
- Create: `src/TheLongestYear/Loop/FarmDecorSnapshot.cs` (capture before `loadForNewGame`)
- Create: `src/TheLongestYear/Loop/FarmDecorCarryoverService.cs` (survey, plan, apply)
- Create: `src/TheLongestYear/Loop/FarmDecorDebug.cs` (`tly_decor`)
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (step 0g snapshot after 0f, line ~288; step 13a restore after `_planningShrine?.Place`, line ~702)
- Modify: `src/TheLongestYear/Loop/JunimoStashService.Placement.cs` (`IsTilePlaceable`: a path under the chest is fine)
- Modify: `src/TheLongestYear/ModEntry.cs` (register `tly_decor` + bridge case)
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs` (row after `keep_pet`, line ~297)
- Modify: `src/TheLongestYear/i18n/default.json` (name + desc after `upgrade.keep_pet.desc`)
- Test: `tests/TheLongestYear.Tests/FarmDecorKeepTests.cs` (catalog row test)
- Modify: `src/TheLongestYear/manifest.json` (0.18.127)

**Interfaces:**
- Consumes: Task 8 Core types; `StashItemCodec.StripNonCosmetic` (Task 5); `JunimoStashService.TryDeposit`, `DropNearStash` (Task 5); `RunBaseline.ToolTiers` (keys `"axe"`, `"pickaxe"`, values = `Tool.UpgradeLevel`, absent = 0).
- Produces: `FarmDecorSnapshot FarmDecorSnapshot.Capture(Farm farm, IMonitor monitor)`; `void FarmDecorCarryoverService.Restore(FarmDecorSnapshot snapshot, IReadOnlyDictionary<string, int> keptToolTiers, JunimoStashService stash, IMonitor monitor)`; `tly_decor <clumps|path x y|check x y>`.

Design: the reset is one method, so the decor travels as live instances held in a local variable (the `DisplayOptionsCarryover.Snapshot` precedent at step 0e), not through MetaState. Each instance is removed from the old farm at capture (the old farm is discarded by `loadForNewGame`; removing first keeps Netcode from seeing one object in two collections) and added to the fresh farm at restore, so every field survives: gate state, sign text and display item, furniture rotation, a torch on a fence (`Fence.heldObject`), dresser contents (trimmed to cosmetic). Restore runs at 13a, after kept buildings (step 8, 9, 9a), the stash chest and the planning shrine, so all of them count as blockers and displaced decor can go straight into the stash.

API checks done in the PC decompile: `Flooring.whichFloor`; `Flooring.GetFloorPathItemLookup()` maps an unqualified object id to a floor id (Object.cs:6051); `Fence.isGate`, `Fence.heldObject`; `Torch` is `StardewValley.Torch`, `Sign` is `StardewValley.Objects.Sign`, `Fence` is `StardewValley.Fence`; `GameLocation.furniture.OnValueAdded` calls `f.OnAdded` (light sources); `OnObjectAdded` sets `Location` and `TileLocation`; `ResourceClump.Tile`, `width`, `height`, `parentSheetIndex`; `Game1.createMultipleItemDebris(Item, Vector2 pixelOrigin, int direction, GameLocation)`; `Object.IsWeeds/IsTwig/IsBreakableStone/IsSpawnedObject`; `Object.boundingBox` (NetRectangle, pixels).

- [ ] **Step 1: Catalog row test (failing).** Append to `FarmDecorKeepTests.cs` (and add `[Collection("i18n")]` to a new class so it shares the i18n fixture):

```csharp
[Collection("i18n")]
public class FarmDecorKeepCatalogTests
{
    [Fact]
    public void Catalog_row_is_one_level_500_jp_in_buildings()
    {
        UpgradeDefinition row = UpgradeCatalog.All.Single(u => u.Id == FarmDecorKeep.UpgradeId);
        Assert.Equal(500L, row.Cost);
        Assert.Equal(UpgradeCategory.Buildings, row.Category);
        Assert.Null(row.PrerequisiteId);
        Assert.Null(row.RunReachRequirement);
    }
}
```

Run `dotnet test TheLongestYear.sln -c Release --filter FarmDecorKeepCatalogTests`, expect FAIL.

- [ ] **Step 2: Name and description with the `game-writing` skill.** Brief: Buildings tab row next to Keep Pet ("Your pet returns at the start of every loop with its name and friendship hearts intact.") and Keep Fish Pond. Effect: paths and floors, fences and gates, lamp-posts, torches, braziers, signs, outdoor furniture and decorations come back on the same tiles; never machines, crops, sprinklers, scarecrows or chests. A stump or log in the way is cleared if your kept axe could break it (you get its hardwood), a boulder if your kept pickaxe could; otherwise that piece waits in the stash. Name stays "Keep Farm Decor". Draft desc: "Paths, fences, lights, signs and outdoor furniture come back where you left them each loop." Add `upgrade.keep_farm_decor.name` / `.desc` after `upgrade.keep_pet.desc`.

- [ ] **Step 3: Catalog row.** After the `keep_pet` row in `UpgradeCatalog.cs`:

```csharp
        // Keep Farm Decor (spec 2026-10-01, Jeff): paths, fences, lights, signs, outdoor furniture
        // back on the same tiles. Above Keep Pet: paths give a speed boost and the layout saves clearing.
        new UpgradeDefinition(FarmDecorKeep.UpgradeId, UpgradeCategory.Buildings, FarmDecorKeep.Cost),
```

Run the catalog and i18n tests: PASS.

- [ ] **Step 4: `FarmDecorSnapshot.cs`:**

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The Farm's kept decor, lifted off the old farm just before loadForNewGame (step 0g)
    /// and held in memory until FarmDecorCarryoverService.Restore (step 13a).</summary>
    internal sealed class FarmDecorSnapshot
    {
        internal sealed class Entry
        {
            public int Id;
            public DecorLayer Layer;
            public Vector2 Tile;
            public Flooring Floor;               // Ground layer
            public StardewValley.Object Obj;     // fence, torch, sign, decor big craftable
            public Furniture Furniture;          // outdoor furniture
            public List<DecorTile> Tiles;
        }

        public readonly List<Entry> Entries = new();

        public static FarmDecorSnapshot Capture(Farm farm, IMonitor monitor)
        {
            var snap = new FarmDecorSnapshot();
            if (farm == null) return snap;

            foreach (var pair in farm.terrainFeatures.Pairs.ToList())
            {
                if (pair.Value is not Flooring floor) continue;
                farm.terrainFeatures.Remove(pair.Key);
                snap.Add(DecorLayer.Ground, pair.Key, OneTile(pair.Key), floor: floor);
            }

            foreach (var pair in farm.objects.Pairs.ToList())
            {
                StardewValley.Object obj = pair.Value;
                if (!FarmDecorKeep.IsKeptDecor(KindOf(obj), obj.QualifiedItemId)) continue;
                farm.objects.Remove(pair.Key);
                snap.Add(DecorLayer.Object, pair.Key, OneTile(pair.Key), obj: obj);
            }

            foreach (Furniture f in farm.furniture.ToList())
            {
                if (!FarmDecorKeep.IsKeptDecor(FarmThingKind.Furniture, f.QualifiedItemId)) continue;
                StashItemCodec.StripNonCosmetic(f);
                farm.furniture.Remove(f);
                snap.Add(DecorLayer.Object, f.TileLocation, TilesOf(f.boundingBox.Value), furniture: f);
            }

            monitor.Log($"Keep Farm Decor: lifted {snap.Entries.Count} piece(s) off the farm before the rewind.", LogLevel.Info);
            return snap;
        }

        internal static FarmThingKind KindOf(StardewValley.Object obj) => obj switch
        {
            Chest => FarmThingKind.Object,          // chests are never kept (the stash included)
            Fence => FarmThingKind.Fence,
            Torch => FarmThingKind.Torch,
            Sign => FarmThingKind.Sign,
            _ when obj.bigCraftable.Value => FarmThingKind.BigCraftable,
            _ => FarmThingKind.Object,
        };

        internal static List<DecorTile> OneTile(Vector2 tile) => new() { new DecorTile((int)tile.X, (int)tile.Y) };

        internal static List<DecorTile> TilesOf(Rectangle pixels)
        {
            var tiles = new List<DecorTile>();
            for (int x = pixels.X / 64; x <= (pixels.Right - 1) / 64; x++)
                for (int y = pixels.Y / 64; y <= (pixels.Bottom - 1) / 64; y++)
                    tiles.Add(new DecorTile(x, y));
            return tiles;
        }

        private void Add(DecorLayer layer, Vector2 tile, List<DecorTile> tiles,
            Flooring floor = null, StardewValley.Object obj = null, Furniture furniture = null)
            => Entries.Add(new Entry { Id = Entries.Count, Layer = layer, Tile = tile, Tiles = tiles, Floor = floor, Obj = obj, Furniture = furniture });
    }
}
```

- [ ] **Step 5: `FarmDecorCarryoverService.cs`:**

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Puts kept decor back on the fresh farm (step 13a). Kept buildings, the stash chest and
    /// the planning shrine are already placed, so they count as blockers. FarmDecorPlanner decides;
    /// this applies: clear the large debris the kept tools can break (hardwood on the ground where it
    /// stood, boulders drop nothing), clear small debris under kept tiles (no drops), place the rest,
    /// and send displaced pieces to the stash, or next to it when the stash is full.</summary>
    internal static class FarmDecorCarryoverService
    {
        private const string AxeKey = "axe";
        private const string PickaxeKey = "pickaxe";

        public static void Restore(FarmDecorSnapshot snapshot, IReadOnlyDictionary<string, int> keptToolTiers,
            JunimoStashService stash, IMonitor monitor)
        {
            if (snapshot == null || snapshot.Entries.Count == 0) return;
            Farm farm = Game1.getFarm();
            if (farm == null) return;

            int axe = keptToolTiers != null && keptToolTiers.TryGetValue(AxeKey, out int a) ? a : 0;
            int pick = keptToolTiers != null && keptToolTiers.TryGetValue(PickaxeKey, out int p) ? p : 0;

            List<ResourceClump> clumpRefs = farm.resourceClumps.ToList();
            List<DecorClump> clumps = clumpRefs
                .Select((c, i) => new DecorClump(i, c.parentSheetIndex.Value, ClumpTiles(c)))
                .ToList();
            List<DecorPiece> pieces = snapshot.Entries.Select(e => new DecorPiece(e.Id, e.Layer, e.Tiles)).ToList();

            DecorPlan plan = FarmDecorPlanner.Plan(pieces, clumps, (x, y) => Survey(farm, x, y), axe, pick);

            foreach (ClearedClump cleared in plan.ClearedClumps)
            {
                ResourceClump clump = clumpRefs[cleared.ClumpId];
                farm.resourceClumps.Remove(clump);
                if (cleared.HardwoodDrop > 0)
                    Game1.createMultipleItemDebris(ItemRegistry.Create(FarmDecorKeep.HardwoodId, cleared.HardwoodDrop),
                        clump.Tile * 64f + new Vector2(64f, 64f), -1, farm);
            }
            foreach (DecorTile tile in plan.DebrisTilesToClear)
                ClearSmallDebris(farm, tile);

            Dictionary<int, FarmDecorSnapshot.Entry> byId = snapshot.Entries.ToDictionary(e => e.Id);
            foreach (int id in plan.Placed)
                Place(farm, byId[id]);
            int dropped = 0;
            foreach (int id in plan.Displaced)
                foreach (Item item in ToItems(byId[id]))
                {
                    Item left = stash != null ? stash.TryDeposit(item) : item;
                    if (left == null) continue;
                    dropped++;
                    if (stash != null)
                        stash.DropNearStash(left);
                    else   // no stash service: still never delete, drop it where the piece stood
                        Game1.createItemDebris(left, byId[id].Tile * 64f + new Vector2(32f, 32f), -1, farm);
                }

            monitor.Log($"Keep Farm Decor: placed {plan.Placed.Count}, to the stash {plan.Displaced.Count} ({dropped} dropped beside it, stash full), " +
                        $"cleared {plan.ClearedClumps.Count} large debris and {plan.DebrisTilesToClear.Count} debris tile(s).", LogLevel.Info);
        }

        // Fresh-farm state of one tile for the planner. Every terrain feature (grass, saplings, trees)
        // and every bush is small debris: it shares or blocks the tile and a kept path must win.
        private static TileBlock Survey(Farm farm, int x, int y)
        {
            if (!farm.isTileOnMap(x, y)) return TileBlock.OffMap;
            var tile = new Vector2(x, y);
            var rect = new Rectangle(x * 64, y * 64, 64, 64);
            TileBlock block = TileBlock.None;
            if (farm.getBuildingAt(tile) != null) block |= TileBlock.Building;
            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj))
                block |= IsSmallDebris(obj) ? TileBlock.SmallDebris : TileBlock.OtherObject;
            if (farm.furniture.Any(f => f.boundingBox.Value.Intersects(rect))) block |= TileBlock.OtherObject;
            if (farm.terrainFeatures.ContainsKey(tile)) block |= TileBlock.SmallDebris;
            if (farm.largeTerrainFeatures.Any(l => l.getBoundingBox().Intersects(rect))) block |= TileBlock.SmallDebris;
            return block;
        }

        private static bool IsSmallDebris(StardewValley.Object obj)
            => obj.IsWeeds() || obj.IsTwig() || obj.IsBreakableStone() || obj.IsSpawnedObject;

        private static void ClearSmallDebris(Farm farm, DecorTile t)
        {
            var tile = new Vector2(t.X, t.Y);
            var rect = new Rectangle(t.X * 64, t.Y * 64, 64, 64);
            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj) && IsSmallDebris(obj))
                farm.objects.Remove(tile);
            farm.terrainFeatures.Remove(tile);
            for (int i = farm.largeTerrainFeatures.Count - 1; i >= 0; i--)
                if (farm.largeTerrainFeatures[i].getBoundingBox().Intersects(rect))
                    farm.largeTerrainFeatures.RemoveAt(i);
        }

        private static void Place(Farm farm, FarmDecorSnapshot.Entry e)
        {
            if (e.Floor != null)
                farm.terrainFeatures[e.Tile] = e.Floor;
            else if (e.Furniture != null)
                farm.furniture.Add(e.Furniture);
            else if (e.Obj != null)
            {
                farm.objects[e.Tile] = e.Obj;
                e.Obj.initializeLightSource(e.Tile);
            }
        }

        // A displaced piece as stash items: the path's item, a fresh fence/torch/sign/decoration
        // (plus a torch that sat on a fence), or the furniture itself (its contents are already
        // trimmed to cosmetic, so the stash rule holds).
        private static IEnumerable<Item> ToItems(FarmDecorSnapshot.Entry e)
        {
            if (e.Floor != null)
            {
                string objectId = Flooring.GetFloorPathItemLookup().FirstOrDefault(kv => kv.Value == e.Floor.whichFloor.Value).Key;
                if (objectId != null)
                    yield return ItemRegistry.Create("(O)" + objectId);
            }
            else if (e.Furniture != null)
                yield return e.Furniture;
            else if (e.Obj != null)
            {
                yield return ItemRegistry.Create(e.Obj.QualifiedItemId);
                if (e.Obj.heldObject.Value is StardewValley.Object held)
                    yield return ItemRegistry.Create(held.QualifiedItemId);
            }
        }

        private static List<DecorTile> ClumpTiles(ResourceClump c)
        {
            var tiles = new List<DecorTile>();
            for (int dx = 0; dx < c.width.Value; dx++)
                for (int dy = 0; dy < c.height.Value; dy++)
                    tiles.Add(new DecorTile((int)c.Tile.X + dx, (int)c.Tile.Y + dy));
            return tiles;
        }
    }
}
```

- [ ] **Step 6: Wire `WorldResetService.PerformReset`.** After step 0f (`HerdBookService.RefreshBeforeReset(_meta, _monitor);`), before step 1:

```csharp
            // 0g. Keep Farm Decor: lift paths, fences, lights, signs and outdoor furniture off the
            // farm before loadForNewGame discards it; they go back at step 13a. Held in memory like
            // the display options above: the reset is one call.
            FarmDecorSnapshot keptDecor = _meta.HasUpgrade(FarmDecorKeep.UpgradeId)
                ? FarmDecorSnapshot.Capture(Game1.getFarm(), _monitor)
                : null;
```

After step 13 (`_planningShrine?.Place(_stashService?.LastPlacedTile);`):

```csharp
            // 13a. Keep Farm Decor back on its tiles, after kept buildings, the stash chest and the
            // planning shrine, so each of them wins its tile and displaced decor can go to the stash.
            FarmDecorCarryoverService.Restore(keptDecor, baseline.ToolTiers, _stashService, _monitor);
```

- [ ] **Step 7: Let the stash chest stand on a kept path.** In `JunimoStashService.Placement.cs`, `IsTilePlaceable` rejects any terrain feature, so a path restored under the stash chest at 13a would push the chest to a fallback tile on the next save load. Change:

```csharp
            if (farm.terrainFeatures.ContainsKey(tile))
                return false;
```

to

```csharp
            // A path or floor under the chest is fine (Keep Farm Decor can restore one there).
            if (farm.terrainFeatures.TryGetValue(tile, out TerrainFeature feature) && feature is not StardewValley.TerrainFeatures.Flooring)
                return false;
```

(add `using StardewValley.TerrainFeatures;` if missing).

- [ ] **Step 8: `FarmDecorDebug.cs` + registration.** Static `Run(string[] args, IMonitor monitor)`, modes:
  - `clumps`: log every resource clump on the farm, `index` and top-left tile.
  - `path <x> <y>`: remove any clump covering that tile (`clump.occupiesTile(x, y)`) and set `farm.terrainFeatures[tile] = new Flooring(Flooring.GetFloorPathItemLookup()["405"])` (Wood Path).
  - `fence <x> <y>`: remove any clump covering either tile, then `farm.objects[tile] = new Fence(tile, "322", false)` and a Torch one tile east: `var torch = new Torch(1, "93"); farm.objects[east] = torch; torch.initializeLightSource(east);`.
  - `check <x> <y>`: log the tile's terrain feature type (and `whichFloor` for Flooring), object id, overlapping clumps, and every `farm.debris` entry within 3 tiles (`d.item?.QualifiedItemId`, `d.item?.Stack`, else `d.itemId.Value`).
  Register in `ModEntry.cs` beside `tly_stashnest` (console + bridge case), same shape as Task 6 Step 4.

- [ ] **Step 9: Build and test.** `dotnet test TheLongestYear.sln -c Release`. All pass.

- [ ] **Step 10: Commit.** Bump to 0.18.127.

```bash
git add src/TheLongestYear/Loop/FarmDecorSnapshot.cs src/TheLongestYear/Loop/FarmDecorCarryoverService.cs src/TheLongestYear/Loop/FarmDecorDebug.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/Loop/JunimoStashService.Placement.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear.Core/UpgradeCatalog.cs src/TheLongestYear/i18n/default.json tests/TheLongestYear.Tests/FarmDecorKeepTests.cs src/TheLongestYear/manifest.json
git commit -m "Keep Farm Decor: paths, fences, lights, signs and outdoor furniture come back each loop (0.18.127)"
git push
```

---

### Task 10: Live verification, TODO and changelog

Headless only (`docs/HEADLESS_DRIVING.md`): `tools/deploy.ps1 -Minimized`, `tools/bridge.ps1` for `tly_*`, `tools/send-smapi-command.ps1` for `debug ...`, results from `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. Load clones only: `tly_loadsave None_450288305` (the Rodger throwaway save; never the Load menu). Label every launch as an automated run (mine), not Jeff's. Close with `close-smapi.ps1`; then `git checkout -- test-output/log-archive`.

**Files:**
- Modify: `TODO.md` (new item at the top of `## Open`)
- Modify: `CHANGELOG.md` (an `## Unreleased` section at the top, above `## 0.18.118`)
- Modify: `src/TheLongestYear/manifest.json` (0.18.128)

Each mutating bridge batch waits for its log line before the next. A "reset" below means: `tly_reset`, wait for `Opened planning hub`, `tly_select Farming`, wait for `Selected Farming`. Record every result line in the TODO entry (Step 6).

- [ ] **Step 1: Setup.** Deploy minimized, `tly_loadsave None_450288305`, wait for `Run \d+ ready` plus 45 s. Bridge: `tly_addjp 20000|tly_buyupgrade stash_1|tly_buyupgrade stash_2` (12 slots). Reset once so the stash chest is placed with its new cap.

- [ ] **Step 2: Stash checks.**
  - S1 `tly_stashnest hats`: `accepted`, and the line shows `contents=[(H)0, (S)1000 colour=..., (P)0]`.
  - S2 `tly_stashnest ring`: `refused`, plus `JunimoStashNestingPatch: refused ... holds (O)529`; `tly_stashnest check` does not list that dresser.
  - S3 `tly_stashnest gear`: four `accepted` lines. Then `tly_stashnest check`; copy the colour, boots, rings and seed values.
  - S4 Reset, then `tly_stashnest check`: the dresser still lists its hat, dyed shirt and pants; colour, boots (`514/...`), `rings=[(O)529,(O)530]` and the trinket seed equal S3.
  - S5 `tly_stashrod`: the rod is deposited (not refused); the Combined Ring in S3 was accepted. Nothing top-level is ever refused.

- [ ] **Step 3: Legacy eject.**
  - L1 `tly_stashclear`, `tly_stashnest legacy`, reset, `tly_stashnest check`: the dresser holds hat, shirt and pants only; `(O)529` is its own stash entry; log `took 1 non-cosmetic item(s) out of stashed containers`.
  - L2 `tly_stashclear`, `tly_stashnest legacy`, `tly_stashnest fill`, reset: log `stash full, dropped '(O)529' x1 by the stash at (x, y)`; `tly_decor check x y` lists `(O)529` in debris.
  - L3 (rescue path) needs a save written by 0.18.118 with a filled dresser in the stash. None exists in the test saves; note it as not exercised. On the 0.18.128 save, a plain reload must NOT log `filled in ... stash record(s)` (records already carry contents).

- [ ] **Step 4: Keep Worn Gear.**
  - G1 Without the upgrade: `tly_stashnest wear`, reset, `tly_stashnest worn`: everything `none` (old behaviour kept).
  - G2 `tly_buyupgrade keep_worn_gear`, `tly_stashnest wear` (copy the line), reset, `tly_stashnest worn`: the same boots stats, `rings=[(O)529,(O)530]` on the left, `(O)529` on the right, the same trinket seed, and `trinketSlots=1`.

- [ ] **Step 5: Keep Farm Decor.** Fresh clone for each of D1 and D3 (`tly_totitle`, `tly_loadsave None_450288305`), so the axe keep differs.
  - D1 `tly_addjp 20000|tly_buyupgrade stash_1|tly_buyupgrade keep_farm_decor|tly_buyupgrade keep_axe_1|tly_additem (T)CopperAxe` (the copper axe in the inventory sets the in-run peak the keep is capped at). Reset once, then `tly_decor clumps`: pick a stump (600) at (x, y) and a boulder (672) at (bx, by). Clumps are map-placed, so they come back at the same tiles each loop; confirm by comparing the listing after the next reset. `tly_decor path x y`, `tly_decor fence bx by`. Reset.
  - D2 `tly_decor check x y`: Flooring `whichFloor` of the Wood Path, no clump, `(O)709` x2 in debris; log `Keep Farm Decor: placed ..., cleared 1 large debris`. `tly_decor check bx by`: no fence (basic pickaxe cannot break a boulder), the boulder is still there; the Wood Fence and Torch are in the stash (`tly_stashnest check`). `tly_decor check (bx+1) by` likewise.
  - D3 Fresh clone, same as D1 but without `keep_axe_1` and the copper axe: after the reset the stump stays and `(O)405` (Wood Path) is in the stash.
  - D4 `send-smapi-command.ps1 "debug time 2200"`: no errors in the log. Torch and lamp-post glow at night is a visual check: list it for Jeff's hands-on pass.
  - D5 Save and reload once after D2 (`debug sleep`, `tly_totitle`, `tly_loadsave`): the stash chest is at the same tile as before (no `blocked by terrain feature` fallback line), even when a kept path runs under it.

- [ ] **Step 6: TODO.md.** Add at the top of `## Open`:

```markdown
### BUILT 0.18.128 (sarahwinchester97, Nexus posts, 2026-10-01): stashed dresser came back empty
Spec 2026-10-01-stash-nesting-farm-decor-worn-gear-design, plan of the same date. The stash keeps a
container's hats, shirts, pants, furniture and wallpaper; anything else inside is refused at deposit.
Stashed items keep dye, boot tailoring, Combined Ring rings and trinket rolls. Older stashed
containers holding other items get them taken out (stash slot, else the ground by the stash). New:
Keep Farm Decor (Buildings, 500 JP) and Keep Worn Gear (Loadout, 1,000 JP). Live checks: <results>.
- Reply to sarahwinchester97 only after the release ships (bug-reply-after-fix). Her current dresser
  is gone; the rescue covers only saves where it is still in the stash.
- For Jeff: confirm the decorative big craftable list in FarmDecorKeep and the trinketSlots re-grant.
```

- [ ] **Step 7: CHANGELOG.md.** Add above `## 0.18.118 - 2026-09-30`, in player words, no file names, no em dashes:

```markdown
## Unreleased

### Fixed

- **A dresser in the Junimo Stash keeps what's in it.** Hats, shirts, pants, furniture, wallpaper and flooring stay inside through the rewind. Anything else in it (rings, boots, fish, tools) has to come out first: the stash hands the dresser back with a message. A dresser stashed by an older version with other things inside gets them taken out into their own stash slots, or set down beside the stash if it is full. Reported by sarahwinchester97.
- **Stashed items keep their looks and stats.** Dyed shirts and pants keep their colour, tailored boots keep their stats, a Combined Ring keeps both rings and a trinket keeps its rolled stats.

### Added

- **Keep Farm Decor** (Junimo Upgrades, Buildings, 500 JP). Paths and floors, fences and gates, lamp-posts, torches, braziers, signs, outdoor furniture and decorations come back on the same tiles each loop. A stump or hollow log in the way is cleared if your kept axe could break it, and you get its hardwood; a boulder is cleared if your kept pickaxe could. Otherwise that piece waits in your stash.
- **Keep Worn Gear** (Junimo Upgrades, Loadout, 1,000 JP). The boots, rings and trinket you are wearing when the year rewinds stay on you.
```

Match the upgrade wording to the final `game-writing` strings from Tasks 7 and 9.

- [ ] **Step 8: Commit.** Bump to 0.18.128.

```bash
git add TODO.md CHANGELOG.md src/TheLongestYear/manifest.json
git commit -m "Stash nesting, Keep Farm Decor, Keep Worn Gear: live checks, TODO, changelog (0.18.128)"
git push
```

No release, no `gh release`, no Nexus or README update in this plan.
