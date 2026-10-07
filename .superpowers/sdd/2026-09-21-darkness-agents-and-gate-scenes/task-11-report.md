# Task 11: The dark aura

Status: DONE_WITH_CONCERNS (see Open). Commits: 30532f7 (first version), 012f92f (glow disc fix).

## What was built

Every item whose type was tampered away this loop (`TamperRecord.OldItemId`) is drawn with a pulsing
dark purple glow under the sprite, in every menu that draws items and when held overhead. It clears
with `tly_reset`.

- `Loop/TaintedAuraPatch.cs`: two Harmony prefixes, patched manually from ModEntry so a signature
  mismatch fails loudly at startup.
  - `Object.drawInMenu(SpriteBatch, Vector2, float, float, float, StackDrawType, Color, bool)`.
  - `Object.drawWhenHeld(SpriteBatch, Vector2, Farmer)` (called from `Game1.drawPlayerHeldObject`
    whenever an Object is the active slot, so any held item, not only pickups).
  - Both signatures match the PC 1.6 decompile exactly (Object.cs 5190 and 5252).
- `Core/Sabotage/TaintedItems.cs`: pure logic, unit tested.
  - `Refresh(list)`: the cached id set, rebuilt only when the list reference or its count changes.
    A reset clears `Run.Tampers` in place, so count 0 empties the set. Bare ids are qualified with `(O)`.
  - `Pulse(ms)`: 0.45 + 0.20 sin(ms / 450), as in the brief.
  - `Falloff(r)`: the soft edge of the glow disc, 1 at the centre, 0 at the rim.
- `ModEntry`: creates the cache, sets `TaintedAuraPatch.Tainted = () => cache.Refresh(_meta?.Run?.Tampers)`
  and calls `TaintedAuraPatch.Apply(harmony)` after the PatchClassProcessor loop.
- A draw call allocates nothing in the steady state (the set is reused; the glow texture is built once).

## Deviations from the brief

1. **Glow texture.** The brief draws `Game1.shadowTexture` tinted purple. On the first live run that
   showed no purple at all: the shadow texture's pixels are black, so a tint only darkens (it looked
   like a slightly bigger brown shadow). The patch now builds one 64x64 white soft-edged disc texture
   on first draw (premultiplied) and tints that, which is a real purple glow. Colour (120, 20, 180),
   scale 1.6 in menus (times the menu's `scaleSize`) and 1.5 held; edge `1 - r^2`. These are tuned
   values for Jeff to look at, not the brief's `(90, 0, 130)`, `4.2`.
2. **Menu centre** is `location + (32, 32) * scaleSize` (the brief had no `scaleSize` on the offset).
3. **Held aura** is centred on the sprite (`objectPosition + (32, 32)`) at the farmer's own held depth.
4. **Debug additions in ModEntry** (needed to look at menus with no mouse or keyboard): `tly_ringtest <id> keep`
   leaves the bundle page open instead of closing it, and `tly_sabotage aurachest` opens a loose chest
   holding every tainted item plus a Parsnip. Debug only, same as the neighbouring commands.

## Files

- `src/TheLongestYear/Loop/TaintedAuraPatch.cs` (new)
- `src/TheLongestYear.Core/Sabotage/TaintedItems.cs` (new)
- `tests/TheLongestYear.Tests/TaintedItemsTests.cs` (new, 6 tests)
- `src/TheLongestYear/ModEntry.cs` (wiring and the two debug additions)

## Tests

`dotnet build TheLongestYear.sln` (0 errors), then `dotnet test tests/TheLongestYear.Tests --no-build`:
Passed 3875, Failed 0 (3869 before, plus 6 new).

## Live check

All launches were mine, minimized, no mouse or keyboard. Throwaway save created by me with
`tly_newgame standard skipintro` (`standard_451074156`; the in-place reset renamed it to
`standard_451074707`); it is deleted and the Saves folder matches its listing from before.
`config.json` was not changed. Pruned tracked logs restored with `git checkout -- test-output/`. The game is closed.

Commands: `tools/deploy.ps1 -Minimized`; `tools/bridge.ps1` (`tly_newgame`, `tly_loadsave`, `tly_select`,
`tly_sabotage tamper` x12, `tly_additem`, `tly_sabotage aurachest`, `tly_ringtest <id> keep`, `tly_reset`);
`tools/send-smapi-command.ps1` (`debug season winter`, `debug clear`, `debug warp FarmHouse 8 8`,
`debug iq (O)344` for a shop menu); a PrintWindow capture script in my scratchpad that restores the
window without activating it, calls `tools/screenshot.ps1`, and re-minimises it. Log: "Harmony: 93 patch
class(es) applied, 0 failed", no ERROR lines.

### Frames (`test-output/scenes/aura/`, untracked and git-ignored; I looked at each)

| File | What it shows |
| --- | --- |
| `held.png` | Farmer holding tainted Copper Ore overhead with a purple pulse behind it; hotbar slot 1 (Copper Ore) and slot 3 (Green Bean) glow, Parsnip and Corn do not. |
| `hotbar-jelly.png` | Hotbar with tainted Jelly glowing and the untouched items plain. |
| `chest.png` | Chest menu: all 12 tainted items in the chest glow, the Parsnip beside them does not; in the player inventory below only the tainted Jelly glows. |
| `bundle-page.png` | Spring Crops bundle page: the tainted Jelly in the page's inventory glows (the page is dimming non-matching items). |
| `shop.png` | Shop menu: Jelly listed for sale has the glow, and so does the Jelly in the inventory row; the neighbours do not. |
| `after-reset-chest.png` | After `tly_reset` the same chest holds only the Parsnip and nothing glows; the Jelly in the inventory is plain. |
| `after-reset-shop.png` | After `tly_reset` the Jelly in the shop and the inventory is plain. |

## Open

- **Held aura over the farmer's head.** The glow is drawn at the held item's depth, so it tints the top of
  the farmer's hair as well as the wall. It reads as part of the effect; if Jeff wants it strictly behind
  the farmer it needs a lower depth.
- **Chest in the world not shown.** The chest frame is the chest menu with a loose chest
  (`tly_sabotage aurachest`), not a chest placed in a room, because opening a placed one needs input.
  The cell draw is the same `drawInMenu` call.
- Not covered: tools, weapons, rings and other non-Object items are never tainted (the darkness only
  swaps bundle ingredient objects), so only `Object` draws are patched. Placed-in-world objects are
  skipped by design.
- The visual (colour, size, pulse speed) is a first version for Jeff to look at, as the spec says.
