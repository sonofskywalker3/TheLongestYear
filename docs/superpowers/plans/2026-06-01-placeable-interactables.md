# Placeable Interactables (Spec B) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fragile tile-anchored interactables with place-and-click furniture books (swept + re-granted exactly-once each loop), a recolored home-anchored stash, and a view-only planning shrine.

**Architecture:** Pure decision logic (which/how-many books to grant) lives in `TheLongestYear.Core` and is xUnit-tested; the furniture registration, interaction patches, stash recolor, shrine placement, and menus are Game1/SMAPI/Harmony glue, runtime-verified via the SMAPI log + playtest (the test project references only Core). Custom furniture is added via a `Data/Furniture` asset edit + a shipped tilesheet, with a `Furniture.checkForAction` prefix opening our menus — the same pattern vanilla uses for catalogues.

**Tech Stack:** C# / .NET 6, SMAPI 4.0, HarmonyX, xUnit. Spec: `docs/superpowers/specs/2026-06-01-placeable-interactables-design.md`.

**Commands:**
- Tests: `dotnet test TheLongestYear.sln -c Release`
- Build (game closed): `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
- Build (game running): add `-p:EnableModDeploy=false -p:EnableModZip=false`
- Co-Authored-By footer on every commit.

**Reference — `Data/Furniture` row** (slash-delimited, parsed by index in `Furniture.RecalculateBoundingBox`/`getData`): `name / type / tilesheet-size(WxH or -1) / bounding-box(WxH or -1) / rotations / price / placement-restriction(-1) / DisplayName / sprite-index / texture-asset / ...`. Custom furniture points `texture-asset` at a mod-provided tilesheet. Confirm exact trailing fields against the live asset at build time (the game logs a parse error if a row is malformed).

---

## File structure

- **Create** `src/TheLongestYear.Core/Interactables/BookKit.cs` — the 3 book item ids + `ReconcilePlan(int heldCount)` (pure: target exactly one held). Unit-tested.
- **Create** `tests/TheLongestYear.Tests/BookKitTests.cs`.
- **Create** `src/TheLongestYear/assets/books.png`, `shrine.png` — generated sprites.
- **Create** `src/TheLongestYear/Integration/BookFurniture.cs` — registers the 3 custom furniture (asset edit + texture), the `checkForAction` prefix opening menus, and the loop-start sweep-and-regrant.
- **Create** `src/TheLongestYear/UI/PlanningShrineService.cs` — auto-places the shrine left of the stash (mirrors `JunimoStashService`).
- **Create** `src/TheLongestYear/UI/ShrinePreviewMenu.cs` — read-only owned/next-buyable view.
- **Modify** `src/TheLongestYear/ModEntry.cs` — wire the new services; remove the old interactable wiring + `tly_set{cookbook,craftbook,board}` commands.
- **Modify** `src/TheLongestYear/Loop/JunimoStashService.cs` — tint the stash purple/gold.
- **Modify** `src/TheLongestYear/Loop/WorldResetService.cs` — drop obsolete book/board quests, add the shrine quest, call the book sweep-and-regrant.
- **Modify** `src/TheLongestYear.Core/GameplayConfig.cs` — remove `Cookbook/Craftbook/SeasonGoalsBoard` tile fields.
- **Delete** `src/TheLongestYear/UI/CookbookInteractable.cs`, `CraftbookInteractable.cs`, `SeasonGoalsBoard.cs`.

---

## Task 1: Core — book ids + reconcile logic (TDD)

**Files:**
- Create: `src/TheLongestYear.Core/Interactables/BookKit.cs`
- Test: `tests/TheLongestYear.Tests/BookKitTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Tests;

public class BookKitTests
{
    [Fact]
    public void Three_book_ids_are_stable()
    {
        Assert.Equal(
            new[] { "sonofskywalker3.TheLongestYear_Cookbook",
                    "sonofskywalker3.TheLongestYear_Craftbook",
                    "sonofskywalker3.TheLongestYear_BundleLog" },
            BookKit.AllBookQualifiedIds);
    }

    [Theory]
    [InlineData(0, 1)]   // none held -> grant 1
    [InlineData(1, 0)]   // exactly one -> grant 0
    [InlineData(3, 0)]   // too many -> grant 0 (extras removed separately)
    public void GrantCount_targets_exactly_one(int held, int expectedGrant)
        => Assert.Equal(expectedGrant, BookKit.GrantCountToReachOne(held));
}
```

- [ ] **Step 2: Run test, verify it fails**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: FAIL — `BookKit` undefined (compile error).

- [ ] **Step 3: Create the Core type**

```csharp
namespace TheLongestYear.Core.Interactables
{
    /// <summary>Identity + reconcile rules for the three carried "book" furniture items. The
    /// per-loop invariant is "exactly one of each in the player's inventory"; the sweep removes
    /// every world/inventory instance and then grants <see cref="GrantCountToReachOne"/>.</summary>
    public static class BookKit
    {
        public const string CookbookId  = "sonofskywalker3.TheLongestYear_Cookbook";
        public const string CraftbookId = "sonofskywalker3.TheLongestYear_Craftbook";
        public const string BundleLogId = "sonofskywalker3.TheLongestYear_BundleLog";

        public static readonly string[] AllBookQualifiedIds = { CookbookId, CraftbookId, BundleLogId };

        /// <summary>How many to add so the player holds exactly one, given how many they already
        /// hold AFTER any extras/placed copies have been removed.</summary>
        public static int GrantCountToReachOne(int heldCount) => heldCount >= 1 ? 0 : 1;
    }
}
```

- [ ] **Step 4: Run test, verify it passes**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: PASS (4 new assertions).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Interactables/BookKit.cs tests/TheLongestYear.Tests/BookKitTests.cs
git commit -m "feat(books): Core BookKit ids + exactly-one reconcile rule

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Generate sprite assets

**Files:**
- Create: `src/TheLongestYear/assets/books.png` (3 furniture sprites, 16x16 each, side by side in one 48x16 tilesheet — sprite indices 0/1/2)
- Create: `src/TheLongestYear/assets/shrine.png` (1 sprite, 16x32)

Runtime-only (art). No unit test.

- [ ] **Step 1: Write a generator script**

Create `tools/gen-sprites.py` (run once; not shipped):

```python
from PIL import Image

# Three 16x16 book covers in one 48x16 tilesheet: red, blue, green spines.
books = Image.new("RGBA", (48, 16), (0, 0, 0, 0))
covers = [(150, 40, 40), (40, 70, 150), (40, 120, 60)]
for i, c in enumerate(covers):
    ox = i * 16
    for x in range(2, 14):
        for y in range(2, 15):
            books.putpixel((ox + x, y), (*c, 255))
    for y in range(2, 15):                      # spine highlight
        books.putpixel((ox + 3, y), (255, 255, 255, 255))
    for x in range(2, 14):                      # page edge
        books.putpixel((ox + x, 14), (235, 225, 200, 255))
books.save("src/TheLongestYear/assets/books.png")

# Shrine: 16x32 little stone shrine with a green junimo glow.
shrine = Image.new("RGBA", (16, 32), (0, 0, 0, 0))
for x in range(3, 13):
    for y in range(14, 30):
        shrine.putpixel((x, y), (110, 110, 120, 255))   # stone body
for x in range(5, 11):
    for y in range(8, 14):
        shrine.putpixel((x, y), (70, 160, 70, 255))      # junimo glow alcove
shrine.save("src/TheLongestYear/assets/shrine.png")
print("wrote books.png + shrine.png")
```

- [ ] **Step 2: Run it**

Run: `python tools/gen-sprites.py` (or `py tools/gen-sprites.py`). If Pillow is missing: `pip install pillow`.
Expected: `wrote books.png + shrine.png`, and both files exist under `src/TheLongestYear/assets/`.

- [ ] **Step 3: Mark assets to copy to output**

In `src/TheLongestYear/TheLongestYear.csproj`, ensure the assets ship:

```xml
<ItemGroup>
  <None Update="assets\**\*.png" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 4: Build to confirm assets copy**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded; `bin/Release/net6.0/assets/books.png` exists.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/assets/ tools/gen-sprites.py src/TheLongestYear/TheLongestYear.csproj
git commit -m "assets: generate placeholder book + shrine sprites

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Register the 3 books as custom furniture

**Files:**
- Create: `src/TheLongestYear/Integration/BookFurniture.cs`
- Modify: `src/TheLongestYear/ModEntry.cs`

Runtime-verified (asset edit + content load).

- [ ] **Step 1: Create BookFurniture with the asset edits**

```csharp
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework.Graphics;
using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Integration
{
    /// <summary>Registers the three carried "book" furniture (Cookbook/Craftbook/Bundle-log) as
    /// custom furniture: edits Data/Furniture to add the rows and provides the tilesheet. The
    /// checkForAction interception (open menus) and the per-loop sweep are added in later tasks.</summary>
    internal sealed class BookFurniture
    {
        private const string FurnitureAsset = "Data/Furniture";
        // The texture the Data/Furniture rows point at (mod-provided, loaded from assets/books.png).
        internal const string BookTextureAsset = "Mods/sonofskywalker3.TheLongestYear/Books";

        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;

        public BookFurniture(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(BookTextureAsset))
            {
                e.LoadFromModFile<Texture2D>("assets/books.png", AssetLoadPriority.Medium);
                return;
            }

            if (e.NameWithoutLocale.IsEquivalentTo(FurnitureAsset))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    // Format: name/type/tilesheetSize/boundingBox/rotations/price/placementRestriction/DisplayName/spriteIndex/texture
                    // type "decor", 1x1 tile, 1 rotation, free, no placement restriction (-1).
                    data[BookKit.CookbookId]  = $"Cookbook/decor/1 1/1 1/1/0/-1/The Longest Year Cookbook/0/{BookTextureAsset}";
                    data[BookKit.CraftbookId] = $"Craftbook/decor/1 1/1 1/1/0/-1/The Longest Year Craftbook/1/{BookTextureAsset}";
                    data[BookKit.BundleLogId] = $"BundleLog/decor/1 1/1 1/1/0/-1/The Longest Year Bundle Log/2/{BookTextureAsset}";
                }, AssetEditPriority.Default);
            }
        }
    }
}
```

Note: the row's final fields and `type` keyword are the 1.6 furniture format; if the game logs `Data/Furniture` parse errors for these rows at load, adjust the trailing fields against the live asset (dump one vanilla row with `patch export Data/Furniture` via Content Patcher, or read a vanilla entry) — sprite indices 0/1/2 map to the three 16x16 cells in `books.png`.

- [ ] **Step 2: Wire it in ModEntry.Entry**

After the existing `_introInjector` construction (~line 86), add:

```csharp
_bookFurniture = new BookFurniture(this.Monitor, helper);
```

And add the field with the other service fields (~line 36): `private BookFurniture _bookFurniture;`

- [ ] **Step 3: Build**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Integration/BookFurniture.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(books): register Cookbook/Craftbook/BundleLog as custom furniture

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 5: Runtime check (next time the game runs)**

In the SMAPI log on load: no `Data/Furniture` parse error for the three ids. (Deferred until a build is deployed.)

---

## Task 4: Open the right menu when a book is used

**Files:**
- Modify: `src/TheLongestYear/Integration/BookFurniture.cs`

The three menus already exist: `new CookbookMenu(_monitor, metaState)`, `new CraftbookMenu(_monitor, metaState)`, `new SeasonGoalsMenu(_monitor, run, metaState, requirements)` (see `MenuLauncher`). The patch needs access to them, so route through the existing `MenuLauncher` (it already builds all three).

- [ ] **Step 1: Add a checkForAction Harmony patch**

Add to `BookFurniture.cs` a static launcher accessor + a nested Harmony patch:

```csharp
        private static System.Func<TheLongestYear.UI.MenuLauncher> _launcher;
        public void AttachLauncher(System.Func<TheLongestYear.UI.MenuLauncher> launcher) => _launcher = launcher;

        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.checkForAction))]
        internal static class CheckForActionPatch
        {
            private static bool Prefix(StardewValley.Objects.Furniture __instance, bool justCheckingForActivity, ref bool __result)
            {
                if (justCheckingForActivity) return true;
                var launcher = _launcher?.Invoke();
                if (launcher == null) return true;
                switch (__instance.QualifiedItemId)
                {
                    case "(F)" + BookKit.CookbookId:  launcher.OpenCookbook();    __result = true; return false;
                    case "(F)" + BookKit.CraftbookId: launcher.OpenCraftbook();   __result = true; return false;
                    case "(F)" + BookKit.BundleLogId: launcher.OpenSeasonGoals(); __result = true; return false;
                    default: return true;
                }
            }
        }
```

(`MenuLauncher` already exposes `OpenCookbook`, `OpenCraftbook`, `OpenSeasonGoals`. The `"(F)"` prefix is the furniture qualified-id form. Verify the constant string concatenation against `__instance.QualifiedItemId` at runtime; if the qualified id differs, switch on `__instance.ItemId` without the prefix.)

- [ ] **Step 2: Wire the launcher accessor in ModEntry**

Where `_launcher` is constructed in `OnSaveLoaded` (~line 283), after it, add:

```csharp
_bookFurniture.AttachLauncher(() => _launcher);
```

- [ ] **Step 3: Ensure PatchAll runs for the nested patch**

The mod already calls `harmony.PatchAll()` in `Entry`; the nested `[HarmonyPatch]` class is discovered automatically. Build to confirm.

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Integration/BookFurniture.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(books): clicking a book furniture opens its menu

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Sweep + re-grant exactly one of each book per loop

**Files:**
- Modify: `src/TheLongestYear/Integration/BookFurniture.cs`
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (call site)
- Modify: `src/TheLongestYear/ModEntry.cs` (OnSaveLoaded safety call)

- [ ] **Step 1: Add ReconcileInventory to BookFurniture**

```csharp
        /// <summary>Per-loop invariant: the player holds exactly one of each book. Remove every
        /// instance from all locations + the inventory, then grant BookKit.GrantCountToReachOne.</summary>
        public void ReconcileInventory()
        {
            var p = StardewValley.Game1.player;
            if (p == null) return;

            foreach (string id in BookKit.AllBookQualifiedIds)
            {
                // Remove placed copies in every location.
                StardewValley.Utility.ForEachLocation(loc =>
                {
                    var toRemove = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>();
                    foreach (var pair in loc.furniture.Pairs())
                        if (pair.Value.ItemId == id) toRemove.Add(pair.Key);
                    foreach (var f in new System.Collections.Generic.List<StardewValley.Objects.Furniture>(loc.furniture))
                        if (f.ItemId == id) loc.furniture.Remove(f);
                    return true;
                });

                // Count + remove inventory copies.
                int held = 0;
                for (int i = 0; i < p.Items.Count; i++)
                    if (p.Items[i]?.ItemId == id) { held++; p.Items[i] = null; }

                // Grant exactly one back.
                int grant = BookKit.GrantCountToReachOne(held > 0 ? 1 : 0);
                if (grant > 0)
                    p.addItemToInventory(StardewValley.Objects.Furniture.GetFurnitureInstance(id));
            }
            _monitor.Log("BookFurniture: reconciled the three books to exactly one each in inventory.", LogLevel.Info);
        }
```

(`loc.furniture` is a `NetCollection<Furniture>`; iterate a copy when removing. `Furniture.GetFurnitureInstance(id)` builds a fresh furniture item. Verify `furniture.Pairs()` vs direct enumeration at build; the second loop removes by reference which is the safe path — drop the first loop if `.Pairs()` doesn't exist.)

- [ ] **Step 2: Call it on reset**

In `WorldResetService.PerformReset`, near the stash step (after `_stashService`/before the completion log), call the book reconcile. Add a `BookFurniture` reference to `WorldResetService` (constructor-injected from `ModEntry`), then:

```csharp
            _books?.ReconcileInventory();
```

- [ ] **Step 3: Call it on save load (mid-run safety)**

In `ModEntry.OnSaveLoaded`, after services are constructed, add `_bookFurniture.ReconcileInventory();` so an existing save without the books gets them.

- [ ] **Step 4: Build + test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings; tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Integration/BookFurniture.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(books): sweep + re-grant exactly one of each book per loop

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Remove the old tile-anchored interactables

**Files:**
- Delete: `src/TheLongestYear/UI/CookbookInteractable.cs`, `CraftbookInteractable.cs`, `SeasonGoalsBoard.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (remove `ConnectTo` calls + `tly_set{cookbook,craftbook,board}` commands + `CmdSet*` methods)
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs` (remove `CookbookTileX/Y`, `CraftbookTileX/Y`, `SeasonGoalsBoardTileX/Y`)

- [ ] **Step 1: Delete the three interactable files**

```bash
git rm src/TheLongestYear/UI/CookbookInteractable.cs src/TheLongestYear/UI/CraftbookInteractable.cs src/TheLongestYear/UI/SeasonGoalsBoard.cs
```

- [ ] **Step 2: Remove their wiring + commands in ModEntry**

Delete lines 152-154 (`SeasonGoalsBoard.ConnectTo`, `CookbookInteractable.ConnectTo`, `CraftbookInteractable.ConnectTo`), the `tly_setboard`/`tly_setcookbook`/`tly_setcraftbook` registrations + switch cases, and the `CmdSetBoard`/`CmdSetCookbook`/`CmdSetCraftbook` methods. (`tly_opencookbook`/`tly_opencraftbook` can stay — they call `_launcher`.)

- [ ] **Step 3: Remove the config tile fields**

In `GameplayConfig.cs`, delete the `CookbookTileX/Y`, `CraftbookTileX/Y`, `SeasonGoalsBoardTileX/Y` properties.

- [ ] **Step 4: Build + test (catch all references)**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings (fix any dangling references the compiler flags); tests PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(books): remove tile-anchored interactables + their config/commands

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Recolor the stash purple/gold

**Files:**
- Modify: `src/TheLongestYear/Loop/JunimoStashService.cs` (`PlaceChest`, after `new Chest(...)`)

- [ ] **Step 1: Tint the chest on placement**

After `var chest = new Chest(playerChest: true, tile, itemId: "256");` (~line 107), add:

```csharp
            // Distinct purple/gold tint so the stash is never confused with a vanilla Junimo Chest.
            chest.playerChoiceColor.Value = new Microsoft.Xna.Framework.Color(150, 90, 200);
```

- [ ] **Step 2: Build**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/JunimoStashService.cs
git commit -m "feat(stash): tint the stash chest purple to distinguish from vanilla Junimo Chests

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Planning shrine — auto-place + sprite

**Files:**
- Create: `src/TheLongestYear/UI/PlanningShrineService.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (wire it; load shrine.png via AssetRequested)

- [ ] **Step 1: Create the service (mirrors JunimoStashService auto-place)**

The shrine is a `Torch`-like decorative `Object` placed left of the stash. Place a flagged Big Craftable / `Object` at `stashTile + (-5, 0)`:

```csharp
using StardewModdingAPI;
using Microsoft.Xna.Framework;
using StardewValley;

namespace TheLongestYear.UI
{
    /// <summary>Auto-places a view-only planning shrine on the farm, ~5 tiles left of the stash
    /// (left of the porch stairs, same row). Interacting opens ShrinePreviewMenu. Re-placed each
    /// load so it always exists from loop 1.</summary>
    internal sealed class PlanningShrineService
    {
        internal const string ShrineFlag = "tly.planningShrine";   // modData key marking our object
        private readonly IMonitor _monitor;

        public PlanningShrineService(IMonitor monitor) => _monitor = monitor;

        public void Place(Vector2 stashTile)
        {
            Farm farm = Game1.getFarm();
            if (farm == null) return;
            Vector2 tile = new Vector2(stashTile.X - 5, stashTile.Y);

            // Remove any stale shrine first.
            var stale = new System.Collections.Generic.List<Vector2>();
            foreach (var pair in farm.objects.Pairs)
                if (pair.Value.modData.ContainsKey(ShrineFlag)) stale.Add(pair.Key);
            foreach (var t in stale) farm.objects.Remove(t);

            var obj = new Object("0", 1) { Name = "TLY Planning Shrine", Fragility = 2 };
            obj.modData[ShrineFlag] = "1";
            farm.objects[tile] = obj;
            _monitor.Log($"PlanningShrineService: placed planning shrine at ({tile.X}, {tile.Y}).", LogLevel.Info);
        }
    }
}
```

(Drawing a custom sprite for a placed `Object` requires a draw override or using a Big Craftable with our texture; for the beta a flagged object + a `checkForAction` patch is enough to make it interactable. If a visible custom sprite is needed, switch to a `Furniture` built from a `Data/Furniture` row pointing at `shrine.png` — same mechanism as the books. Decide at implementation based on how it looks.)

- [ ] **Step 2: Wire placement after the stash is placed**

In `WorldResetService.PerformReset` and `ModEntry.OnSaveLoaded`, after `_stashService.PlaceChest()`, call `_planningShrine.Place(stashTile)` with the stash's resolved tile (expose it from `JunimoStashService` via a `LastPlacedTile` property).

- [ ] **Step 3: Build**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/UI/PlanningShrineService.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/Loop/JunimoStashService.cs src/TheLongestYear/Loop/WorldResetService.cs
git commit -m "feat(shrine): auto-place a planning shrine left of the stash

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Read-only ShrinePreviewMenu + interaction

**Files:**
- Create: `src/TheLongestYear/UI/ShrinePreviewMenu.cs`
- Modify: `src/TheLongestYear/UI/PlanningShrineService.cs` (checkForAction patch)

- [ ] **Step 1: Create the read-only menu**

A scroll list, no buy buttons. Rows: owned upgrades (✓) and next-buyable upgrades (cost shown, greyed). Reuse `UpgradeCatalog` + `MetaState`. Minimal IClickableMenu:

```csharp
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Read-only "what have I unlocked / what could I buy next reset" board. No purchasing —
    /// that stays on the reset/win shrine popup.</summary>
    internal sealed class ShrinePreviewMenu : IClickableMenu
    {
        private readonly List<string> _lines = new();

        public ShrinePreviewMenu(MetaState state)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, showUpperRightCloseButton: true)
        {
            foreach (UpgradeCategory cat in System.Enum.GetValues(typeof(UpgradeCategory)))
                foreach (var def in UpgradeCatalog.ByCategory(cat))
                {
                    bool owned = state.HasUpgrade(def.Id);
                    bool buyable = !owned
                        && (def.PrerequisiteId == null || state.HasUpgrade(def.PrerequisiteId))
                        && state.MeetsMetaRequirement(def.MetaRequirement);
                    if (owned) _lines.Add($"[x] {def.DisplayName}");
                    else if (buyable) _lines.Add($"[ ] {def.DisplayName}  ({def.Cost} JP)");
                }
        }

        public override void draw(Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);
            int y = yPositionOnScreen + 80;
            Utility.drawTextWithShadow(b, "Junimo Shrine — Planning", Game1.dialogueFont,
                new Vector2(xPositionOnScreen + 60, yPositionOnScreen + 40), Game1.textColor);
            foreach (string line in _lines)
            {
                Utility.drawTextWithShadow(b, line, Game1.smallFont, new Vector2(xPositionOnScreen + 60, y), Game1.textColor);
                y += 32;
                if (y > yPositionOnScreen + height - 60) break;   // TODO-free: simple clamp, no scroll for v1 preview
            }
            base.draw(b);
            drawMouse(b);
        }
    }
}
```

(`UpgradeCatalog.ByCategory`, `def.PrerequisiteId`, `def.MetaRequirement`, `def.Cost`, `def.DisplayName`, `state.HasUpgrade`, `state.MeetsMetaRequirement` all exist — see `JunimoShrineMenu.VisibleCatalogForActiveCategory`. The clamp drops overflow rows; acceptable for a v1 preview, and the comment says so rather than leaving a silent cap.)

- [ ] **Step 2: Open it from the shrine via checkForAction patch**

Add to `PlanningShrineService.cs`:

```csharp
        private static System.Func<MetaState> _state;
        public void AttachState(System.Func<MetaState> state) => _state = state;

        [HarmonyLib.HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction))]
        internal static class CheckForActionPatch
        {
            private static bool Prefix(StardewValley.Object __instance, bool justCheckingForActivity, ref bool __result)
            {
                if (justCheckingForActivity) return true;
                if (!__instance.modData.ContainsKey(ShrineFlag)) return true;
                var state = _state?.Invoke();
                if (state == null) return true;
                Game1.activeClickableMenu = new ShrinePreviewMenu(state);
                __result = true;
                return false;
            }
        }
```

Wire `AttachState(() => _meta.State)` in `ModEntry.OnSaveLoaded`.

- [ ] **Step 3: Build + test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings; tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/UI/ShrinePreviewMenu.cs src/TheLongestYear/UI/PlanningShrineService.cs src/TheLongestYear/ModEntry.cs
git commit -m "feat(shrine): read-only planning preview menu on shrine interaction

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Quest cleanup + shrine quest

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (`FireBookQuestIntros`/`AddIntroQuest` area, ~lines 460-515)

- [ ] **Step 1: Remove the obsolete book/board intro quests**

Delete the `AddIntroQuest` blocks for the cookbook, craftbook, and the Season-Goals/fireplace board (`tly.-9004`) — and any `DismissedIndicators` gating tied only to them. Keep the stash quest (`tly.-9003`).

- [ ] **Step 2: Add the shrine quest**

Where the stash quest is added, add alongside it:

```csharp
            if (!_meta.DismissedIndicators.Contains("tly.shrine"))
            {
                AddIntroQuest(
                    id: "tly.-9005",
                    title: "The Junimo Shrine",
                    description: "There's a small Junimo shrine just left of your farmhouse. " +
                                 "Check it to see what you've unlocked and what you can plan for next loop.");
            }
```

- [ ] **Step 3: Build + test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings; tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/WorldResetService.cs
git commit -m "feat(quests): drop obsolete book/board quests, add the shrine quest

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: Full build + integration verification

**Files:** none (verification only).

- [ ] **Step 1: Full build + test suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` then `dotnet test TheLongestYear.sln -c Release`
Expected: Build 0 warnings; all tests PASS. (Deploy only when the user OKs — they asked to hold deploys.)

- [ ] **Step 2: Boot-log checks**

`Harmony: N patch class(es) applied, 0 failed.`; no `Data/Furniture` parse errors; `placed planning shrine`, `reconciled the three books`, and stash-place logs present.

- [ ] **Step 3: Playtest (user-run)**

New game / loaded save: three books in inventory; place each → click → correct menu opens (Cookbook/Craftbook/Season-Goals). Stash is purple, at home tile. Planning shrine sits ~5 tiles left of the stash; clicking it shows the read-only owned/next-buyable list. After a `tly_reset`: still exactly one of each book in inventory (even if you placed them in another location first), stash back home, shrine present. The "Junimo Shrine" quest points to the shrine; no cookbook/craftbook/board quests appear.

---

## Self-review notes

- **Spec coverage:** books-as-furniture → Tasks 1,3,4,5; sweep/regrant exactly-one → Tasks 1,5; remove old interactables → Task 6; stash recolor + home anchor → Task 7 (home anchor is existing behavior, preserved); planning shrine → Tasks 8,9; quest changes → Task 10; art → Task 2. All covered.
- **Runtime-format risks flagged:** the `Data/Furniture` row trailing fields, the `(F)` qualified-id prefix, `furniture.Pairs()` vs enumeration, and the shrine sprite (object vs furniture) are each called out with a concrete fallback to settle at build/runtime — these are asset/runtime-format details the compiler + log confirm, not placeholders.
- **Type consistency:** `BookKit.{CookbookId,CraftbookId,BundleLogId,AllBookQualifiedIds,GrantCountToReachOne}`, `BookFurniture.{ReconcileInventory,AttachLauncher}`, `PlanningShrineService.{Place,AttachState,ShrineFlag}`, `ShrinePreviewMenu(MetaState)` are defined once and used consistently.
