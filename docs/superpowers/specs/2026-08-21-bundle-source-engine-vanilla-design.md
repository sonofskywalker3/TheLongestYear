# `BundleSource: Engine | Vanilla` — design (2026-08-21, awaiting ruling on two UX points)

## Why

- Nexus bug 1108030 (6 reporters): Remixed boards came back **Standard** after a reset on 0.11.60.
  Root cause: `Game1.bundleType` is a non-persisted static (reset to `Default` on exit-to-title and
  at the top of `loadForNewGame`'s caller chain — decompile `Game1.cs:3008`, `:17515`), and the
  reset's `loadForNewGame` → `GenerateBundles(bundleType)` (`Game1.cs:4199`) therefore always
  wrote the Standard set. The engine made this moot on master by overwriting the board — but it
  also silently takes the board away from players who chose Standard, Remixed, or another bundle
  mod (ada113 / ErraticPixel: Challenging Community Center Bundles compat ask).
- `BundleSource = Vanilla` is the cheap route to that compat ask: keep whatever board the save
  has, classify it with the existing `LegacyReadAndClassify` path, and regenerate **the same
  kind of board** on every reset.

## Config

`GameplayConfig.BundleSource` (string enum, `"Engine"` default | `"Vanilla"`), exposed in GMCM as
a dropdown (`AddTextOption` with `allowedValues`; the interface stub gains that method). Tooltip
(and the GMCM paragraph): *"Engine: The Longest Year builds its own board every loop. Vanilla:
keep the game's own Standard/Remixed board (or another bundle mod's) and re-roll it the same way
on every reset. Changing this takes effect at the next reset."*

## Persisted state (`MetaState`)

- `BundleSource` (string, per save) — the mode the save is actually running under. Stamped on
  the new-game activation (from the Advanced Options choice, see UX) and re-stamped from the
  config at every reset. SaveLoaded reads the save's value, never the config, so a global config
  flip cannot change an in-flight loop.
- `VanillaBundleType` (string `"Default"` / `"Remixed"`) — the player's Standard/Remixed choice.
  Captured at new-game activation from `Game1.bundleType` (still set from the dropdown at that
  moment). For **existing** saves with no value: inferred once from the live board
  (`BoardInspection.LooksRemixed`: any bundle name outside the vanilla Standard name set ⇒
  Remixed) and persisted.

## Behaviour by mode

| | Engine (today) | Vanilla (new) |
|---|---|---|
| New game | AGO shows "TLY Custom"; engine writes the board at first SaveLoaded (`GenerateFreshRun`) | AGO shows Normal/Remixed; vanilla writes the board; `BundlesGeneratedForReset` stays −1 |
| SaveLoaded | `EngineModeDecider` → manifest / fresh / legacy | always `LegacyReadAndClassify` (the decider short-circuits on the save's `BundleSource`) |
| Reset (`WorldResetService` step 11) | engine `Generate` + `WriteToWorld` + marker | set `Game1.bundleType = VanillaBundleType` **before** `loadForNewGame` (so vanilla generates the right set — Remixed re-rolls with the fresh `uniqueIDForThisGame` from step 0, Standard is fixed); skip the engine write; marker stays −1; `LastGeneratedRequirements` = `builder.BuildRequirements()` over the new live board |
| Other bundle mods | overwritten (as today) | honoured: `GenerateBundles` reads the (Content-Patcher-edited) `Data/Bundles` / `Data/RandomBundles` |
| Weapon/hat donations | as today | patches stay live whenever the live board has (W)/(H) slots (v0.12.4 rule) |

Mid-loop flip of the config (GMCM or config.json): **takes effect at the next reset** — the
reset re-stamps `MetaState.BundleSource` from the config and generates accordingly. Stated in the
GMCM tooltip and the README. No attempt to convert a live board mid-loop.

## Classification completeness for arbitrary boards (audit)

The legacy path must be complete for any board a bundle mod can produce. Known gaps found while
building `tly_jpbudget` + the A4 survey, all to be closed in this task:

1. **`BundleCatalogBuilder.Build` catalogs only the first X ingredients** of a pick-X-of-Y bundle
   (`take = Math.Min(NumberOfSlots, …)`). The remaining Y−X items get no rarity/season entry →
   weekly-theme sampler sees them as Common + year-round; the hub/season-goal surfaces that read
   the catalog miss them. Fix: catalog every concrete ingredient. (Likely ada113's "partial
   items".)
2. **`SeasonResolver` prefers crop over forage seasons** (Grape = Fall-only although it is Summer
   forage). Fix: union crop + forage seasons.
3. **Legacy `BuildRequirements` applies no obtainability clamp** (the engine path does, via
   `GeneratedBundleSet.ClampRampForObtainability`). Fix: apply the same clamp with the merged
   pins in the legacy builder so a Remixed/modded board can't demand an unobtainable minimum.
4. **Challenging Community Center Bundles (CCCB, Nexus 6361, `alja.CCCB`) is a C# mod, not a
   Content Patcher pack** (source: github.com/Jaksha6472/ChallengingCommunityCenterBundles, v3.1.0;
   its `Vanilla` pack is saved at `docs/superpowers/notes/ccb/content_Vanilla.json`). It rewrites
   `Game1.netWorldState.Value.BundleData` **values** under the game's own keys on **`DayStarted`**
   and writes the vanilla strings back on `Saving`/`DayEnding`. TLY classifies at `SaveLoaded` —
   i.e. it sees the vanilla board, and the live CC then shows CCCB's 9–11-item, pick-5–10,
   quality-2/4, ×10–99 asks. **That is ada113's report exactly** ("some extra items added, not all,
   lower quantities/wrong qualities"). Fix for Vanilla mode: fingerprint `BundleData` (ordinal join
   of values) at `SaveLoaded`, re-check on every `DayStarted`, and when it changed rebuild the CcItem
   catalog + requirements from the live data (log `board changed by another mod — re-classified`),
   then re-derive the week's open-slot pools. Engine mode keeps overwriting (as today) — CCCB's
   name-matched swap would also re-apply over engine names it knows (Spring Crops, Animal, …) every
   morning, so Engine + CCCB stays unsupported and documented as such.
5. **Format features CCCB actually uses** (all supported by the parser, to be covered by fixtures):
   bare string object ids (`Moss`, `Powdermelon`, `FlashShifter.StardewValleyExpandedCP_Butterfish`
   → `NormalizeItemId` prefixes `(O)`), quality 2/4 asks, stacks to 99, pick-X-of-Y with X up to 10
   (derived ramp `[X/4, X/2, 3X/4, X]` — e.g. Crab Pot 10-of-10 becomes PerItem), raised Vault
   amounts (8k/15k/25k/50k — `VaultBundleMap` reads the live value), `C`/`R`/`F`/`BO` reward ids
   (ignored), artifacts/placeables as requirements (Ancient Sword, Crystal Path ×50, Torch ×30).
   **Not used:** category refs, `(O)`-qualified ids, (BC)/(W)/(H)/(F) requirement ids, new bundles,
   rooms or keys, `Data/RandomBundles` edits.
6. **Category-ref ingredients** (`-5` any egg …) remain skipped everywhere; a category-only bundle
   classifies as null and is dropped from the gate (logged). CCCB doesn't use them; SVE's own
   bundles don't either. Left as a documented limitation.
7. **Quality / quantity asks** are read per ingredient (`IngredientStacks/Qualities`, MAX across
   duplicates) and shown on the hub; the gate is slot-state based so vanilla enforces them.
8. **The Missing** (Abandoned Joja Mart) is never classified (room has no theme) — by design; it
   does not gate.
9. Curated quotas (A4) apply by name in both paths.

Deliverable of the audit: a test fixture per gap (synthetic bundle strings) plus a fixture that
runs `BundleClassifier` over every string in `content_Vanilla.json` (48 bundles) and asserts
48 classified, 0 dropped, monotone ramps ending at X; a `tly_classify` run on a `debug
ShuffleBundles` board for the remixed path.

## UX — Advanced Options row (RULING 1)

**Option A — "TLY Custom" becomes a third dropdown entry** (Normal / Remixed / TLY Custom),
default = the config's `BundleSource` (TLY Custom when Engine). Picking TLY Custom stamps
`BundleSource = Engine` on the new save; picking Normal/Remixed stamps `Vanilla` +
`VanillaBundleType`. The config is *not* rewritten by the dropdown — it stays the default for
future new games.
**Option B — the config drives the dropdown**: Engine → only "TLY Custom" (today); Vanilla → the
vanilla Normal/Remixed row. The player has to know to change GMCM before starting a new game.

**Recommendation: A.** One discoverable choice at the moment it matters, per save, no hidden
global coupling; the tooltip on each entry explains it. B is cheaper (no new dropdown logic) but
reproduces the 1108030 confusion in a new form ("why can't I pick Remixed?").

## Mid-loop flip (RULING 2)

**Recommendation: takes effect at the next reset**, stated in GMCM and README; the per-save
`MetaState.BundleSource` is what runs a loop. (Alternative — apply immediately by rewriting the
board — rejected: it would wipe donations and the engine manifest mid-loop.)

## Tests / smoke

- `EngineModeDecider` gains a `bundleSource` input (Vanilla ⇒ Legacy) — unit tests.
- `BoardInspection.LooksRemixed` — unit tests against the vanilla Standard name set.
- Catalog/season/clamp fixes — unit tests per gap (Core) + `tly_classify` on a `debug
  ShuffleBundles` board.
- Smoke: one reset in each mode on a clone (Engine: `Requirements source: engine manifest`;
  Vanilla/Remixed: `legacy read-and-classify` + a remixed name set that differs from the
  pre-reset board; Vanilla/Standard: the 26 vanilla names).
