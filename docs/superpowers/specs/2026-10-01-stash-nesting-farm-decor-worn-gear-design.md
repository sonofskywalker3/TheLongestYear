# Stash nesting, Keep Farm Decor, Keep Worn Gear

Date: 2026-10-01. Branch: `master` (not story work).

Source: Nexus posts, sarahwinchester97, 1 Oct 2026. She filled a modded dresser (Tall Beech
Dresser) with hats and rings, put the dresser in the Junimo Stash, and it came back empty. Jeff
replied the same day promising that a stashed dresser keeps hats and clothes, that rings and
shoes need their own stash slots, and that an upgrade would keep worn shoes and rings.

## Cause

The stash does not carry item instances across the loop. `JunimoStashService.ToRecord` writes a
`StashItemRecord` (id, stack, quality, plus preserve fields, rod attachments, enchantments) and
`CreateFromRecord` rebuilds a fresh item from it. Nothing records an item's contents, so any
container comes back empty. The same gap loses dye colour on shirts/pants, tailoring on boots,
the inner rings of a Combined Ring, and a trinket's rolled stats.

## 1. Stash: cosmetic-only nesting (Jeff ruling: option C)

**Rule.** A container may go into the stash only if everything inside it, at any depth, is
cosmetic. Otherwise it is refused at the moment of insertion. Nothing is ever trimmed or
deleted later.

Cosmetic (allowed nested):
- Hats
- Shirts and pants (Clothing), dyed or not
- Furniture (chairs, tables, rugs, paintings, lamps, windows, beds, catalogues)
- Wallpaper and flooring (the Wallpaper item class)

Everything else is non-cosmetic, including boots, rings, trinkets, fish, any sellable/shippable
object, tools, seeds, resources, food, and craftable decorations (lamp-posts, torches, signs,
fences, paths). These can still go in the stash as top-level items, one slot each, as today.

**Containers checked** (recursively): StorageFurniture `heldItems` (dressers, modded storage
furniture), FishTankFurniture held fish, any Furniture or Object `heldObject`, a Chest item's
`Items` (mods that carry filled chests), and mod bag items that expose an item list.
Exemption: a Tool's `attachments` (rod bait/tackle) stay supported as today.

**Refusal.** Hook the insertion path (`Chest.addItem` on the stash chest, already patched by
`JunimoStashCapPatch`): a failing container is returned to the player unchanged and a HUD
message shows. Draft text (final wording goes through the game-writing skill, no em dashes):
"This container holds things that can't be stored in the stash. Take out everything except
hats, shirts, pants, furniture, wallpaper and flooring, then try again."

**Record.** `StashItemRecord` gains `Contents` (list of nested records, null when none) and
the cosmetic identity fields below, all nullable with default null so old saves deserialize.

**Legacy saves.** A container already in the stash from an older version, holding
non-cosmetic items: on load, eject those items into the stash as their own entries where slots
are free; anything that does not fit drops on the ground next to the stash chest. Never deleted.

## 2. Stash: keep item identity (same fix, same release)

Record and restore, nullable fields:
- Clothing dye colour (`clothesColor`) and dyeable state
- Boots tailoring (applied boot sheet index / colour index, i.e. the tailored-boot stats)
- Combined Ring: its inner rings, as nested records
- Trinket: its generation seed / rolled stats so it comes back identical

## 3. Keep Farm Decor (new upgrade)

- One level, **500 JP**. Category: Buildings (sits next to Keep Pet).
- Jeff: priced above Keep Pet because paths give a speed boost and the layout saves clearing.

**Kept, at the same tiles:** paths and flooring, fences and gates, lamp-posts, torches,
braziers, signs, outdoor furniture and placed decorations on the Farm.
**Never kept:** machines, crops, sprinklers, scarecrows, chests.

Snapshot the Farm's decor before the reset (alongside the pet / fish pond snapshots) and
re-place it after the fresh farm and kept buildings exist.

**Conflicts on the fresh farm:**
- Small debris (weeds, twigs, small stones) on a kept tile: cleared, no drops.
- Large debris (stump, hollow log, boulder) on a kept tile:
  - If the player's KEPT tool tier can break it (copper axe: stump; steel axe: hollow log;
    steel pickaxe: boulder), clear it. Stumps and logs drop their vanilla **hardwood** on the
    ground where the debris stood. Boulders drop **nothing** (Jeff: no stone).
  - Otherwise the debris stays and the decor on those tiles goes into the stash, or drops
    next to the stash chest if the stash is full.
  - Only debris that interferes with kept decor is touched. Large debris elsewhere is left
    alone (Jeff: not a hardwood farm).
- A kept building placed over decor: the building wins; that decor goes to the stash, or next
  to the stash chest if full.
- A path where a non-kept building stood: the path comes back.

## 4. Keep Worn Gear (new upgrade)

- One level, **1,000 JP**. Keeps worn boots, both rings and the trinket(s) across the loop.
- Implementation: `FarmerReset` skips the boots/rings/trinket unequip block when owned. The
  farmer object persists through the in-place reset, so the worn instances (Combined Rings,
  tailored boots, trinket stats) survive untouched.
- Only what is WORN at the reset. Spare rings/boots still need stash slots.

## Follow-up

No reply to sarahwinchester97 until this ships (bug-reply-after-fix rule). Her dresser from the
current version is lost; the legacy-save eject covers only saves where the dresser is still in
the stash.

## Tests

- Core: cosmetic classifier (each allowed and blocked category, nested depth 2+, rod exempt).
- Core: `StashItemRecord` round-trips with Contents and identity fields; old JSON deserializes.
- Core: debris-clear decision table (tool tier x debris type x on-decor).
- Live: dresser with hats only (accepted, survives); dresser with a ring (refused, message,
  item back in inventory); dyed shirt survives; Keep Farm Decor with a path under a stump and
  copper axe kept (hardwood at the stump tile); Keep Worn Gear with a Combined Ring.
