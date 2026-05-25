# The Longest Year — Design Spec

- **Date:** 2026-05-25
- **Status:** Approved design (pre-implementation)
- **Type:** Roguelite time-loop wrapper mod for Stardew Valley
- **Platform:** PC first (`net6.0`), Android port later
- **Framework:** SMAPI + Harmony, C#, flat-with-subfolders layout (files < 400 lines)
- **Project folder:** `TheLongestYear/` (7th project in the Stardee Valoo workspace)

---

## 1. Concept

A roguelite "loop-wrapper" mod. The vanilla game's theme, map, NPCs, crops, and items are kept; the mod adds a **run/meta/loop layer** on top. Every run is a single in-game year played as a Hades-style gauntlet: you push as deep into the year as your permanent upgrades and banked items allow, fail, and restart — keeping your meta-progression each time.

This is a **systems mod, not a content mod** — no new art or maps. The novelty is the loop, the contract system, and the cross-run item-banking economy.

## 2. Premise & narrative

Joja Corp holds a **one-year purchase option** on the valley, contingent on rezoning the land industrial to build a distribution warehouse. If the **Community Center is restored within that year**, the town qualifies for **historic-landmark designation**; the resulting preservation ordinance freezes the zoning, Joja's option lapses, and they can never build. That is the win.

The **Junimos**, refusing to let Joja destroy the valley, use their magic to **rewind the year** each time you fall short — giving you attempt after attempt. The magic they recover from your progress is granted back to you as **Junimo Points (JP)**, the meta-currency that powers your permanent upgrades.

> v1 ships minimal placeholder intro/outro text. Full cutscenes are deferred.

## 3. Core structure

- A year = **4 months (seasons) × 4 weeks = 16 weeks**. Every run starts Spring 1.
- Each month has **5 themed contracts**, mapped onto the Community Center's five item rooms:

  | Theme | CC Room |
  |-------|---------|
  | Foraging | Crafts Room |
  | Farming / Ranching | Pantry |
  | Fishing | Fish Tank |
  | Mining | Boiler Room |
  | Mixed | Bulletin Board |

  (The Vault / gold room is handled separately.)
- Each contract = **item-granular required items** (e.g. "donate *a* copper bar," not a full vanilla bundle) + a **bonus** + a **liability**.
  - **Bonus:** helps the contract's playstyle (e.g. Mining → more drops / higher drop chance / more ladder chance).
  - **Liability:** throttles a *different* income stream (e.g. foraging drops disabled), creating real opportunity cost.

## 4. Weekly play loop

- Each week you **champion one not-yet-championed theme**, chosen **1-of-2** from the themes still available *and* completable that week. The championed theme's **bonus + liability are active for that week only**.
- Over the 4 weeks of a month you champion **4 of the 5** themes. The **5th is never championed** — you get no bonus/liability for it, but you must still complete it.
- The **contract-pick screen is the weekly planning hub** — it surfaces the Weather and Cart previews (see §9 Foresight) according to your purchased upgrade tiers.

## 5. Gates & failure

Two checkpoints, both of which end the run on failure:

- **Weekly gate:** your championed contract's required items must be donated **by that week's end** (day 7 / 14 / 21 / 28). Miss it → run fails.
- **Monthly gate:** **all five** contracts donated **by day 28** to advance to the next season. Miss it → run fails.

On failure: award JP (see §8), perform the **in-place reset** (§7), re-roll the year's contracts, re-apply upgrades, and restart at Spring 1.

**Win:** clear month 4 (Winter) → the whole CC is restored → landmark ending → the loop breaks. (Endless "victory-lap" free-play mode is deferred.)

## 6. Donations: cumulative, and consumed on submission

- **Cumulative toward the whole CC, anytime.** Any CC item you donate counts toward its room/bundle and ticks off any contract that needs it — regardless of which theme is currently championed. Over-donate forage during a Fishing week and the Foraging contract is already (partly) checked off when its turn comes; this is also how you clear the unchampioned 5th theme before month-end.
- **Consumed on submission, never refunded on a failed run.** A donated item is gone the moment it's submitted. If the run later fails, that item is *not* returned. This is the load-bearing rule of the whole economy: deploying a banked rarity is a **gamble**, so you only spend rarities on a run you're confident you can finish.

## 7. Persistence model

**Banked forever (survive every reset):**
- Junimo Points (never reset).
- Purchased upgrades (cumulative / permanent).
- The **Junimo Stash**: capacity tier + contents.

**Reset on run-fail (in-place reset to a Spring-1 / week-1 baseline):**
- Calendar → Spring 1.
- World: crops, terrain features, placed objects, farm buildings → baseline.
- Money, inventory (minus the Stash), skills/levels, relationships → baseline (modified by Carryover upgrades).
- CC donations → wiped.
- The year's contract set → re-rolled (§10).
- Mine progress, quests, etc. → reset.

**Notes:**
- **Only the Junimo Stash persists. Regular chests reset with the world** (narratively, the Junimos protect your satchel through the rewind). If every chest persisted there would be no scarcity and hoarding would be trivial — which would kill the central tension.
- **Vanilla CC room rewards** (greenhouse, minecart, bus repair, bridge, panning) fire **in-run** as genuine power spikes that help you finish the year, and **reset on the next loop** like everything else. Rooms therefore matter twice: JP *and* an in-run boost, both earned fresh each attempt.

## 8. Junimo Points economy

- **Per-item JP** on donation, **scaled by item rarity** — common forage pays a little, a red cabbage / rain-fish / sweet gem berry pays a lot.
- **Completion bonuses attach to vanilla bundles and rooms only:** completing a bundle → a JP bonus; completing a room → a larger JP bonus.
- **No JP for completing contracts or the monthly gate.** Contracts are *gates*; rewarding them would double-pay the same donations (you'd be paid for the gate *and* the bundle). The gate's reward is simply that you advance.
- JP is **banked across runs** and never reset.
- **Tension by design:** the rarest items are both the biggest JP payout *and* the most valuable to bank — so the donate-now-vs-bank-for-the-winning-run choice genuinely hurts. The duplicate case falls straight out of the rules: a second red cabbage is either a JP boost now or a second "shot" insured for a future winning run.
- All JP values are **config-tunable**; points must stay calibrated so banking a rarity is often the smarter play, not auto-overridden by the JP payout.

## 9. The Junimo Stash (the strategic engine)

- **One Junimo Stash**, with **scarce slots, expandable via upgrade tiers.** It is the *only* persistent container.
- It is a **cross-run savings account of hard-to-get items.** The meta-skill is **opportunistic hoarding**: recognize an opportunity (it's raining during a mining week → farm the season's rain-fish; the Traveling Cart has red-cabbage seed → buy and grow it) and **bank the rarity for the run that can actually win**, rather than burning it for JP on a doomed run.
- Scarcity is deliberate: you cannot bank everything, so "which rarities earn a precious slot?" is the core recurring decision.

## 10. Contract generation & solvability guarantee

Every reset, the generator produces the year's 20 contracts (5/month × 4 months) such that:

- **Their union completes the entire real CC** — the run is always theoretically winnable.
- **Each required item is scheduled in a month it is obtainable** (never out of season).
- **Early weeks use early-obtainable items** (grown / foraged / caught / mined within that week).
- **Hard-but-possible items appear only rarely**, and **never truly impossible** (since all five contracts are mandatory each month).
- The **week-1 offered themes** must each be completable within week 1 given the run's current upgrades; harder themes become offerable in later weeks.

The randomness is therefore in **which valid partition of the real CC** you draw, plus which optional bundle-slot items are chosen — not in whether the run is winnable. The vanilla bundle definitions are the **ground truth** of what the CC needs; contracts are a re-skinned, scheduled, themed view of those requirements.

## 11. Upgrade tree

JP-purchased, cumulative/permanent, bought **between weeks** at the **Junimo Shrine** menu. Priced so you afford roughly **1–2 per run**.

1. **Loadout** — backpack tiers · starting tool tiers (copper→iridium, per tool) · starting gold tiers · starter seeds/sprinklers.
2. **Carryover** — retain X% skill XP · retain X hearts · pre-unlocked recipes.
3. **Efficiency** — larger stamina · shop/seed discounts · early horse.
4. **Obtainability** *(makes rare items reliable & bankable)*:
   - **Junimo Cultivation (crops):** each upgrade adds one normally-out-of-reach crop to the **Mixed Seeds** pool, in-season, even in the perpetual Year One (e.g. *Cultivation: Red Cabbage*, then Starfruit, Ancient Fruit, …). Converts a hard item from unreliable (Cart RNG only) to **farmable**.
   - **Junimo Fortune (non-crops):** boosts obtainability of rare items Mixed Seeds can't grow — rare-fish catch chance, geode/mineral yields, rare monster/forage drop rates.
5. **Foresight** *(surfaced on the contract-pick screen)*:
   - **Weather Sage — 7 tiers.** Tier 1 reveals one (random) day of the upcoming week's weather; each tier reveals one more; **Tier 7 = all 7 days.**
   - **Cart Whisperer — 10 tiers, 2 items per tier.** Each tier reveals 2 more items from **this week's Traveling Cart stock** (Friday + Sunday visits); **Tier 10 = the full ~20-item week.**
6. **Stash** — capacity tiers.

> **Mixed Seeds rule:** year-2 crops are gated behind Cultivation upgrades (not a free base rule). Buying one unlocks that crop in the Mixed Seeds pool in-season, giving a farmable/bankable path that also tightens the solvability guarantee.

## 12. Technical approach & safety

- **In-place reset** (chosen over fresh-save-per-run) — no save-file IO, instant rewind, sidesteps the Android scoped-storage save problems. Implemented with a comprehensive reset checklist; leverage the game's own new-game initialization helpers (e.g. `Game1.loadForNewGame`) where they exist, with targeted manual resets otherwise.
- **Two-loop leak test:** run two consecutive resets and assert a clean baseline (catches state that leaks between runs — the classic in-place-reset failure mode).
- **One-time save backup** before the first-ever reset (the reset is destructive; guard against a reset bug nuking a save).
- **Reflection (`AccessTools`) for platform-differing members**, anticipating the Android port (PC DLL ≠ Android runtime; see workspace memory).
- **One error pattern:** typed exceptions, logged via SMAPI's `Monitor`. No bare `except`-equivalent catches.
- **All numbers live in `ModConfig`** (gate timings, JP values, rarity tiers, cruel-item rate, upgrade prices, stash sizes, foresight tier counts).

## 13. Module layout (files < 400 lines)

```
TheLongestYear/
  ModEntry.cs              — wiring, SMAPI event subscriptions
  ModConfig.cs             — all tuning dials
  manifest.json
  TheLongestYear.csproj
  Loop/
    RunManager.cs          — week + month gating, fail/restart, win detection
    WorldReset.cs          — in-place reset routine + leak checklist
  Meta/
    MetaStore.cs           — global persistence (JP, upgrades, stash)
    UpgradeTree.cs         — upgrade definitions, pricing, apply-at-run-start
  Contracts/
    ContractSystem.cs      — generation + solvability, draw, championing, bonus/liability modifiers
    CCMapping.cs           — theme ↔ room ↔ items; reads vanilla bundle ground truth
    ItemRarity.cs          — rarity tiers + per-item JP values
  Stash/
    JunimoStash.cs         — the persistent stash container + UI
  UI/
    ContractPickMenu.cs    — weekly planning hub (weather/cart previews)
    JunimoShrineMenu.cs    — between-week upgrade shop
  Obtainability/
    MixedSeedsPatch.cs     — Cultivation: inject unlocked year-2 crops in-season
    FortunePatches.cs      — rare drop/catch-rate boosts
  Narrative/
    Story.cs               — placeholder intro/outro text
```

## 14. v1 scope (MVP — prove it's fun & stable on PC)

**In:**
- In-place reset + two-loop leak test + one-time save backup.
- 16-week gauntlet with weekly + monthly gates and win detection.
- 5-room contract system with champion / bonus / liability.
- Solvable-partition contract generator.
- Cumulative donations + consume-on-donate.
- Banked JP: rarity-scaled per-item + bundle/room completion bonuses.
- Between-week Junimo Shrine shop with a starter set across all six categories.
- Obtainability framework + **Red Cabbage cultivation** + 1–2 more crops + 1 Fortune (rare-fish) upgrade as proof-of-pattern.
- Foresight: **Weather Sage** (7 tiers) + **Cart Whisperer** (10 tiers, 2/tier).
- Scarce Stash (base tier + a couple of capacity upgrades).
- Placeholder narrative text.
- Full `ModConfig`.

**Deferred:**
- Cutscenes / full narrative.
- Endless victory-lap mode.
- Android port.
- Deep balancing pass.
- Advanced contract modifiers (per-run "blessings," etc.).
- The long tail of Obtainability unlocks.

## 15. Open questions for the planning phase

- **Donation interface:** reuse the vanilla `JunimoNoteMenu` / bundle data as the donation surface vs. a custom CC-themed contract panel (leaning custom for weekly-randomization flexibility).
- **In-place reset internals:** exactly which `Game1` new-game helpers are safe to reuse vs. which state needs targeted manual reset.
- **Cart preview:** predicting the Traveling Cart's stock ahead of its visit (the cart stock is RNG seeded by date/save seed).
- **Reading vanilla bundle definitions** as the CC ground truth (`Data/Bundles`) and mapping them to themes/rooms.
- **Difficulty curve / cruel-item rate** tuning hooks (config-exposed, calibrated in play-testing).
