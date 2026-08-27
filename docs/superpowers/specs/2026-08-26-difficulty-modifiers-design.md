# Difficulty Modifiers - Design

**Date:** 2026-08-26
**Status:** Approved by Jeff (brainstorm session, same night). Supersedes the "Easy/Normal/Hard
tiers" shape that sub-project B of the 0.13.0 engine spec assumed.
**Driving input:** difficulty is the top ask from emmalution's stream (82.7K subs, streaming the
mod since 16 July).

## 1. What this is

Ten independent **difficulty modifiers**, each a four-step named setting
(**Easy / Normal / Hard / Extreme**), living in a new GMCM section. There is no overall
difficulty tier. A player turns up the dials he wants turned up.

Every modifier defaults to **Normal**, and Normal is defined as *exactly today's shipping
balance*. An existing save that never opens the new section plays byte-for-byte as it does now.

### 1.1 Rulings this design is built on

Jeff ruled each of these during the brainstorm. They are recorded here because several of them
close off designs that look reasonable on paper.

1. **The season gate is NOT a difficulty dial.** Each bundle's quota ramp is capped at `X`, the
   bundle's `NumberOfSlots`. The ramp is therefore a *partition of a fixed total*, not a total.
   Raising Spring's quota steals from Fall rather than adding work. Jeff: "making the first seasons
   harder means there's less left for the later ones, it's backwards." The gate stays out.
2. **Asks and economy are both in scope.** All four ask-side axes, all five economy dials.
3. **No overall tier.** Jeff: "we kill the easy/normal/hard overall switch in favor of 5
   difficulty modifier switches, or however many." This removes the whole preset-semantics
   problem (what happens when you edit a dial under a preset).
4. **Named steps, not raw numbers or sliders.** The mod owns what each step means, so the numbers
   can be retuned in a later release without every player's config going stale.
5. **No Off step.** Four steps, Easy through Extreme. `Off` would have had to mean two different
   things (coherent for quality asks, degenerate for JP earned), so it is gone.
6. **GMCM only, applies at the next reset.** No new-game Advanced Options entry. A change made
   mid-run sits inert until the next loop.
7. **Impossible mode is a separate, post-1.0 project.** Parked in `TODO.md`. It is not a fifth
   step; it is a mode that disables these modifiers entirely and composes fully random boards.

### 1.2 Explicitly out of scope

- **Farm types.** Jeff confirmed leaving them out. They are a difficulty *multiplier* (Riverland
  trivialises the Fish Tank quotas, Hilltop hands you Geologist and Blacksmith), and re-enabling
  them before modifiers exist means retuning a single fixed bar seven times. There is also a hard
  mechanical blocker independent of balance: `WorldResetService` restores kept buildings to
  hardcoded Standard-farm tiles (Coop 54,9 / Barn 46,12 / Silo 51,9), kept pets stagger west from
  (54,8), and the intro places Lewis at a guessed Standard tile. Several of those are water or
  sand on Riverland and Beach. Farm types need every restore placement made farm-aware first.
- **The Junimo Vault money bundles.** `PoolTuning.VaultAmountMultiplier` stays a config.json knob.
  Money slots (`ItemId == "-1"`) are explicitly excluded from the stack modifier, so a difficulty
  step never silently changes what the bus repair costs.

## 2. The ten modifiers

Four change what the bundle board asks for. Five change what you carry between loops. One
governs the mercy system.

### 2.1 Ask-side (baked into the board at generation)

| # | Modifier | Easy | Normal | Hard | Extreme | Vanilla board? |
|---|---|---|---|---|---|---|
| 1 | Stack size | 0.75x | 1.0x | 1.5x | 2.0x | yes |
| 2 | Quality asks | 0.5x | 1.0x | 2.0x | 3.0x | yes |
| 3 | Required slots | -1 | 0 | +1 | require all | yes |
| 4 | Item rarity | 0.5 bias | 1.0 bias | 1.6 bias | 2.4 bias | **no (Engine only)** |

### 2.2 Economy (read live from the run's stamp)

| # | Modifier | Easy | Normal | Hard | Extreme |
|---|---|---|---|---|---|
| 5 | JP earned | 1.5x | 1.0x | 0.75x | 0.5x |
| 6 | Shrine prices | 0.75x | 1.0x | 1.25x | 1.5x |
| 7 | Starting gold | 2.0x (1000g) | 1.0x (500g) | 0.5x (250g) | 0.0x (0g) |
| 8 | Starting cart slots | 3 | 1 | 0 | 0 |
| 9 | Hold and pity prices | 0.5x | 1.0x | 2.0x | 4.0x |

### 2.3 Mercy

| # | Modifier | Easy | Normal | Hard | Extreme |
|---|---|---|---|---|---|
| 10 | Season pity | starts sooner, eases further | today's curve | starts later, eases less | **off** |

## 3. Per-modifier mechanics

### 3.1 Stack size

The factor lands in two different places because the two board sources decide stacks differently.

**Engine board.** Today `BundleSlotFiller.RollStack` returns `1` for every domain except
`QualityCrops` (a flat `QualityCropStack`, default 5) and `MonsterDrops` (price-banded ranges).
`LargeQuantityForageChance` separately turns one seasonal-forage slot into a 40-99 ask. The factor
scales those tuning numbers before generation:
`QualityCropStack`, `CheapMinStack`/`CheapMaxStack`, `MidMinStack`/`MidMaxStack`,
`DearMinStack`/`DearMaxStack`, `LargeQuantityMinStack`/`LargeQuantityMaxStack`, and
`LargeQuantityForageChance`.

**Vanilla board.** The post-pass (section 4) multiplies each slot's authored stack.

**Rules for both.** Round away from zero. Floor of 1. **Cap of 99** per slot, because a bundle
slot asking for more than one inventory stack of a 99-cap item reads as a bug. Money slots
(`ItemId == "-1"`) are never scaled. Category refs (bare negative ids other than -1, e.g. `-5`
"any animal product") ARE scaled, since a stack of a category is a legitimate ask.

### 3.2 Quality asks

**Engine board.** The factor multiplies `SilverQualityChance` (0.10) and `GoldQualityChance`
(0.05). Clamped so the two together never exceed 0.90, leaving a real chance of a plain ask at
every step.

**Vanilla board.** Relative to what vanilla authored, so Normal is a genuine no-op:
- Normal: no change at all.
- Hard / Extreme: each currently-plain eligible slot rolls for an added star, at
  `GoldQualityChance * (factor - 1)` for gold then `SilverQualityChance * (factor - 1)` for silver.
- Easy: each currently-starred slot has its star stripped with probability `1 - factor`.

**Eligibility is not negotiable at any step.** The existing vetting still governs: the built-in
never-quality set (Seaweed, Green Algae, White Algae, from Nexus 1122358), the config extension
list `QualityIneligibleItemIds`, and `ItemPools.QualityEligibleIds`. Extreme cannot put a gold
star on Fiber. This is the bug class that caused 1122358 and it must not be reintroduced.

### 3.3 Required slots

Changes `BundleSpec.NumberOfSlots` (the pick-X count) without touching how many slots are shown.
Easy -1, Hard +1, Extreme sets it to `Slots.Count` (donate everything on the board). Clamped to
`[1, Slots.Count]`.

**This is the only ask-side modifier that raises the real total** rather than redistributing it,
which is precisely why it exists given ruling 1.1.1.

**Consequence that must be handled: the quota tables.** `DefaultBundleQuotas` values are absolute
counts that must never exceed a bundle's `X`. Raising `X` is safe (quotas simply become a smaller
fraction). **Lowering `X` on Easy is not**: a quota of 5 against a new `X` of 4 is unsatisfiable
and would brick the run. Every quota read must be clamped to the live `NumberOfSlots`. This clamp
is a correctness requirement, not a nicety.

### 3.4 Item rarity (Engine board only)

Biases the weighted sampler toward harder items using the hardness score that already exists for
the pity trim (`ItemHardness.Score`: rarity tier 1-4, +2 if the domain needs a station or recipe,
+1 if the item's earliest spawn season is Fall or Winter).

Each candidate's weight becomes `round(weight * bias^(score - 1))`, floor 1. At Hard's 1.6 bias a
score-4 item is about 4.1x more likely to be picked than it is today; at Easy's 0.5 it is about
0.125x.

**On a Vanilla board this modifier does nothing, by definition**, because changing which item a
bundle asks for is changing the bundle. The GMCM line itself must say "Engine bundles only" in
the option name, not only in the tooltip. A setting that silently does nothing is a bug report
waiting to happen.

### 3.5 JP earned

A single multiplier applied inside `JpCalculator`, covering per-item JP, the season multipliers,
and the bundle / room / weekly-quest / checkpoint completion bonuses, plus `VaultPayment`. Applied
at the end of the existing `Scale` so the season ramp shape is untouched. Minimum award of 1 JP
is preserved wherever it exists today.

### 3.6 Shrine prices

A multiplier on `UpgradeDefinition.Cost`, rounded away from zero, floor 0. `Cost` is read in six
places (`UpgradePurchase`, `UpgradePurchaseService` logging, `JunimoShrineMenu` twice,
`ShrinePreviewMenu` twice, the `tly_upgrades` console listing). All six must route through one
pure helper so the displayed price and the charged price can never disagree. This is the exact
bug class that 0.14.2 just fixed for Shop Discount (posted price vs charged price), so it is
worth stating twice.

### 3.7 Starting gold

A multiplier on `GameplayConfig.StartingMoney` (500), consumed by `RunBaselineBuilder.Build`.
`StartingMoney` keeps its existing GMCM number option; it is the Normal-step baseline and the
step scales it. The step's tooltip names that interaction so the two controls do not look like
they are fighting.

### 3.8 Starting cart slots

Replaces the hardcoded `CartSlotRules.MinSlots = 1` floor with the step's value, so it is the
number of items the Traveling Cart shows before any Cart Stall upgrade is bought. The 0.12.0
economy spec explicitly parked "tier sets the starting slot count" for this work.

Note honestly: **Hard and Extreme are identical for this modifier** (both 0), because the floor is
reached at Hard. Extreme has nothing further to take.

Interaction: this is inert when `LimitTravelingCartStock` is off, exactly as the Cart Stall
upgrades already are.

### 3.9 Hold and pity prices

A multiplier over both curves, `BundleHoldCosts` and `PityCosts` (both default 0/50/100/200/300).
Applied in `BundleHoldPricing.CostFor`, which both callers already share. The first hold stays
free at every step, because 0 times anything is 0, and that is the correct behavior: the step
makes *repeated* holds expensive rather than taxing the first mistake.

### 3.10 Season pity

Jeff's original ruling was "Hard turns pity off entirely", made while an overall tier still
existed. With no tier, that ruling is honored by making pity the tenth modifier rather than a
consequence of a tier.

The step derives a pity profile from the existing config baselines, so config.json remains the
Normal definition:

| | Threshold | Quota step | Floor | Trim per step | Enabled |
|---|---|---|---|---|---|
| Easy | `round(base * 0.6)` = 3 | `base * 1.5` = 0.15 | `1 - (1 - base) * 1.2` = 0.40 | `round(base * 1.5)` = 3 | yes |
| Normal | `base` = 5 | `base` = 0.10 | `base` = 0.50 | `base` = 2 | yes |
| Hard | `round(base * 1.6)` = 8 | `base * 0.5` = 0.05 | `1 - (1 - base) * 0.5` = 0.75 | `round(base * 0.5)` = 1 | yes |
| Extreme | n/a | n/a | n/a | n/a | **no** |

The existing "Season pity" GMCM section and its five dials stay exactly where they are; they are
the baselines the step scales, same pattern as starting gold.

**Pity counting always runs regardless of the step**, which is already how `PityEnabled` behaves.
A player who sets pity to Extreme, gets stuck, and drops back to Normal finds his accumulated
`SeasonFailCounts` intact and pity resumes where it would have been.

## 4. The Vanilla board post-pass

Today, `BundleSource=Vanilla` means TLY writes nothing: `loadForNewGame` generates the
Standard or Remixed board and `WorldResetService` deliberately keeps its hands off
(`WorldResetService.cs:505`, "no engine write"). As the code stands, an ask-side modifier would
only exist on the Engine board.

This design adds a post-pass so that modifiers 1, 2, and 3 mean the same thing on all three board
sources. **It never changes which item a slot asks for.** Vanilla authored the bundle; the pass
only adjusts how much and what quality, plus the pick-X count.

- **Input:** the live `Data/Bundles` entries as raw slash-delimited strings, immediately after
  `loadForNewGame`.
- **Transform:** parse with the existing `BundleParsing`, apply modifiers 1-3, write back with
  the existing `BundleDataWriter` format.
- **Determinism:** seeded from the same `BundleEngineSeed.For(UniqueMultiplayerID, EffectiveBundleSeedLoop)`
  the Engine path uses, so a replayed reset produces an identical board and the anti-save-scum
  guarantee holds.
- **Skipped entirely when every ask-side modifier is Normal**, so the default Vanilla path stays
  literally untouched, including its logging.
- **Money bundles are skipped.** A Vault entry's slots are left exactly as authored.
- Lives in Core as a pure `IDictionary<string,string>` transform so it is unit-testable without
  the game.

## 5. Architecture

**Approach: resolve to a profile, stamp it at reset, read the stamp.** Jeff approved this over
resolving at read time (which would let a mid-run GMCM change alter the current season's JP) and
over multiplying at each call site (which would scatter ten dials across a dozen files with no
single place to read the effective difficulty).

### 5.1 Types (all in `TheLongestYear.Core`, all pure)

- `DifficultyStep` - enum `{ Easy, Normal, Hard, Extreme }`.
- `DifficultySettings` - the ten steps, serialized into `GameplayConfig.Difficulty`. Every
  property defaults to `Normal`.
- `DifficultyProfile` - the resolved effective values: the ask-side factors, the five economy
  numbers, and the pity profile. Serializable, because it is what gets stamped.
- `DifficultyResolver` - `Resolve(DifficultySettings, GameplayConfig) -> DifficultyProfile`. A
  pure function and the single home of the entire balance table. One file, fully unit-tested,
  retunable in a later release without touching a consumer.

### 5.2 The stamp

`MetaState.Difficulty` holds a `DifficultyProfile`, written at every reset (and at first-run
setup) from the live config.

**Resolved values are stamped, not the ten steps.** This matches the existing pity-stamp idiom
(`BoardEaseSeason` / `BoardEaseSteps`), which exists so a reload reproduces the reset exactly. If
steps were stamped instead, a future release that retunes what "Hard" means would silently
change an in-flight run's economy on reload. The step *names* are stamped alongside the values,
for diagnostics and display only.

**Legacy saves:** `MetaState.Difficulty` is null. Consumers fall back to resolving live from
config, which defaults to all-Normal and is therefore identical to today's behavior. The next
reset writes a real stamp. No migration code and no save-format break.

### 5.3 Which modifiers read what

- Modifiers 1-4 never read the stamp at runtime. They are consumed once, at board generation,
  and are baked into the written board. They are stamped only so `tly_difficulty` can report
  what this loop was generated under.
- Modifiers 5-10 read the stamp on every use. This is what makes "applies at the next reset"
  real rather than aspirational.

### 5.4 Diagnostics

A new `tly_difficulty` console command prints the ten configured steps, the ten stamped steps,
and every resolved value. When a stream viewer files a balance report, this is what tells us what
he was actually playing on. Follows the read-only shape of `tly_netstate`.

## 6. Testing

The balance table is a pure function, so the bulk of this is unit-testable.

- `DifficultyResolverTests` - every modifier at every step; Normal resolves to exactly today's
  config values (the regression guard that protects existing saves).
- `VanillaBoardDifficultyPassTests` - stack scaling with the 99 cap and the floor of 1; money
  slots untouched; quality added only to eligible items; quality stripped on Easy; required-slot
  clamping; an all-Normal profile returning the input unchanged.
- `BundleSlotFillerTests` additions - quality chance clamp at 0.90; ineligible items still never
  starred at Extreme.
- `SlotPoolBuilder` / sampler additions - rarity bias changes pick distribution in the expected
  direction over a seeded run.
- Quota clamp - a bundle whose `X` dropped below its configured quota is satisfiable.
- `JpCalculatorTests` / `UpgradePricing` additions - multiplier applied, minimums preserved.
- I18n guard - every new GMCM string has a `default.json` entry (the existing `I18nGuardTests`
  catches this automatically).

## 7. Player-facing copy

Both `README.md` and the Nexus description must gain a Difficulty section in the same task,
content-identical per the workspace rule. It must state plainly:

- Every modifier defaults to Normal; changing nothing changes nothing.
- Changes apply at the next loop, not immediately.
- **Item rarity only affects TLY Custom (Engine) bundles.** Stack size, quality asks, and
  required slots work on vanilla Standard and Remixed boards too.

## 8. Risks

1. **Compounding.** Ten dials at Extreme is not a tuned experience and nobody has played it.
   Mitigated by independence (a player raises only what he wants) and by the honesty of the
   design: there is no "Extreme" badge claiming it is beatable.
2. **Required slots at Extreme could brick a run** if a bundle's shown slots include something
   that season cannot produce. The existing `DerivedSeasonPins` clamp and the curated quota ramps
   are the defense, and the quota clamp of section 3.3 is mandatory.
3. **The Vanilla post-pass touches a path that currently has zero writes.** Highest-risk change
   in the design. Mitigated by skipping the pass entirely when the three relevant modifiers are
   all Normal, so the default Vanilla experience cannot regress.
4. **Cart slots at 0** means an empty-looking cart, which players may report as broken. The GMCM
   tooltip must say the cart is empty until Cart Stall I is bought.
