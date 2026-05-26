# Playtest Fixes — Session Handoff

**Date:** 2026-05-26
**Branch:** `feat/v1-plan-05-ui` (already checked out; 10 commits past `ac71216`)
**Status:** Bundle-gate refactor merged; first playtest found 4 critical bugs + 5 UX/feature gaps.
**Tests:** 217 passing.

This continues from `2026-05-26-bundle-gate-handoff.md`. That refactor is complete and verified by unit tests; this doc is the **next** session's work — the bugs the playtest surfaced and the UX changes the user called out.

Raw playtest notes (the unstructured form the user gave them in):
`test-output/playtest-2026-05-26-feedback.md` — read it first, it has reproduction details that aren't repeated here.

---

## Critical bugs (gate is currently wrong)

### CB1 — Construction Bundle silently dropped from the gate
- **Symptom:** Log shows `bundle 'Construction' (X=4, Y=4) didn't match any classification rule`. The bundle never enters `_requirements`, so donating Wood/Stone/Hardwood doesn't count for the gate, and the bundle never shows on a card.
- **Root cause:** Vanilla `Data/Bundles` lists Wood twice in Construction's ingredient string (`(O)388 99 0 (O)388 99 0 (O)390 99 0 (O)709 10 0`). `BundleClassifier.CollectQualifiedIngredients` dedups to 3 items; `parsed.NumberOfSlots` is 4. The classifier's X==Y check (`parsed.NumberOfSlots == ingredients.Count`) fails (4 ≠ 3) and the bundle falls through to "unclassified".
- **Fix:** In `BundleClassifier`, after dedup, clamp `effectiveX = Min(parsed.NumberOfSlots, ingredients.Count)`. Compare against `ingredients.Count`. When `effectiveX == ingredients.Count`, classify as PerItem. (Vanilla semantics: every distinct ingredient must be donated; duplicates in the slot list are satisfied implicitly by our set-based donation ledger.) Add a unit test using a synthetic ParsedBundle with duplicate ingredient ids.

### CB2 — Chef's Bundle crashes the classifier when X=Y
- **Symptom:** Log shows `Percentage bundle needs Y > X; got Y=7, X=7. (Parameter 'ingredients') -- skipping`. Chef's is in `DefaultBundleQuotas`, so the Percentage branch fires first; `CreatePercentage` validates Y > X and throws; the whole bundle is skipped.
- **Root cause:** When SVE-edited save data inflates Y by adding ingredients (or any catalog where the bundle's quota was tuned for Y > X but actual Y ≤ X), the classifier blindly trusts the quota table.
- **Fix:** In `BundleClassifier.Classify`, when `bundleQuotas` matches BUT `parsed.NumberOfSlots >= ingredients.Count` (X >= Y), don't take the Percentage branch — fall through to PerItem. Add a guard: if Percentage would throw, log warn + try PerItem. Unit test: bundle with name in quota table but X==Y → classifies as PerItem with the (cumulative-Spring-quota interpreted as "due season" pins? no — just plain PerItem with empty pins, every ingredient required for full-complete).

### CB3 — Real in-CC donation does NOT fire the Harmony patch
- **Symptom:** User donated one item via the JunimoNoteMenu and went to sleep. SMAPI log has ZERO `Donated 1x ... -> +N JP` lines. JP stayed at 0.
- **Verified:** `tly_testdonate (O)16 1` works correctly — `DonationService.OnItemDonated` paid +5 JP (bonus x1.5) and JP went 0 → 5. So the JP pipeline + bonus multiplier are fine. The issue is purely the Harmony patch on `Bundle.tryToDepositThisItem` not catching real deposits.
- **Investigation order:**
  1. Confirm the patched type. `DonationPatches.cs:15` uses `typeof(Bundle)`. Verify it resolves to `StardewValley.Menus.Bundle` (the correct one) and not something else. Search the decompile for `tryToDepositThisItem` to confirm the exact signature.
  2. Confirm Harmony actually applied the patch — log `harmony_summary` via the SMAPI console (the `ConsoleCommands` mod adds it) and check whether our patch shows under `Bundle.tryToDepositThisItem`.
  3. If patched but not firing: check whether some OTHER mod (the user has `ConsoleCommands`, `GenericModConfigMenu`, etc.) is using `[HarmonyPriority(Priority.First)]` + returning `false` from a prefix to skip the original. Unlikely but possible.
  4. If the patch fires but `__state` is null: the Prefix signature may be wrong for the `tryToDepositThisItem` overload that vanilla actually calls (there may be multiple overloads in 1.6).
  5. Last resort: add a SMAPI `IInputEvents.ButtonPressed`-driven inventory diff observer that detects "the player just lost an item that's also a bundle ingredient" — a fallback in case Harmony coverage is broken.
- **Fix:** start with step 1 — read the decompile for the exact `Bundle.tryToDepositThisItem` signature in 1.6 Android, then verify our patch targets it.

### CB4 — Save-baked SVE item ids inflate Chef's Y → triggers CB2
- **Symptom:** Even with SVE disabled (`SMAPI skipped Mods\.Stardew Valley Expanded`), the save's `Game1.netWorldState.Value.BundleData` still contains SVE-prefixed ingredient ids from when SVE was active. 20 ids unresolvable + 8 SVE bundles unclassified. Chef's Y=7 (with SVE Candy entry) instead of vanilla Y=6.
- **This is the upstream of CB2** but worth its own line. Once CB1+CB2 are fixed structurally, this becomes a non-issue (graceful skip + correct PerItem classification), but the user may want a one-shot tool to **scrub SVE remnants from their save's BundleData** so the catalog reflects vanilla.
- **Optional fix:** add a `tly_scrubbundles` debug command that rebuilds `Game1.netWorldState.Value.BundleData` from vanilla `Content/Data/Bundles.xnb` (reset the bundle data on next save). Probably not worth the complexity in v1 — the structural fix (CB1+CB2) is enough to make the gate work correctly with SVE remnants present. Just mention it to the user as an option.

---

## UX / feature feedback (post-CB fixes)

### UX1 — `←` glyph rendering as a splat (missing-glyph indicator)
- `BundleSeasonBadge` returns strings starting with `  ← needs N this month`. Stardew's `smallFont` (pixel font) doesn't include U+2190 LEFTWARDS ARROW, so it renders as a tofu/missing-glyph box ("a dot that was splatted, or a popped speech bubble" per user).
- **Fix:** drop the arrow entirely. Use plain ASCII: `"needs N before Summer 1"`. Also audit U+00D7 `×` in `"Bonus this week (1.5×):"` — replace with ASCII `x`: `"Bonus this week (1.5x):"`. Test by rebuilding + asking user for a confirming screenshot.

### UX2 — Move bundle progress OFF the planning hub
- User: *"I don't want the bundles on this page, we need a separate spot to pull up the list of items required to donate in order to pass the season."*
- The hub is the SELECTION decision surface (pick a theme for the week). Bundle progress is a SEPARATE concern.
- **Action:** strip the bundle-progress rows from each card on `ContractPickMenu`. Keep theme name, bonus, liability, per-week bonus-items preview. Card shrinks back to roughly the v1 height (~360 instead of 460).
- **New menu:** create `SeasonGoalsMenu` (or rename — see UX-naming question below) showing for the current season, by bundle:
  - Bundle name, donated/X count.
  - Per-item donation list — what's still needed before next-season-1 (the bundles' `IsSatisfiedAtSeasonEnd` predicates spelled out).
- **Access:** hotkey (config-settable, off by default), or a button on the Junimo Shrine, or in the GameMenu's tabs. Pick one for v1 and document.

### UX3 — Unify badge phrasing to "before Summer 1"
- Current strings:
  - Seasonal/PerItem: `"  ← needs N this month"`
  - Percentage: `"  ← needs N more by {season}"`
- User wants ALL THREE to say: **"needs N before Summer 1"** (next-season day-1 framing).
- **Fix:** rewrite `BundleSeasonBadge` to a single phrasing pattern:
  ```
  "needs {missing} before {nextSeason} 1"
  ```
  where `{nextSeason}` is the season AFTER `_offerSeason`. Winter-end case: `"needs {missing} before year-end"` (or just suppress the badge — Winter day 28 = run end).
- The Seasonal-bundle case (Spring Foraging shown on Spring) should also use the same phrasing: `"needs N before Summer 1"`.

### UX4 — Bonus sampler ignores difficulty
- User: *"I thought we were weighting this towards the easier items on the front end. You have the 2 high-level essences that you specifically deferred the Adventurer's bundle for on the mining focus, plus three of the 4 fishing items are crab pot, not the easy fish like the Chub or Sardine!"*
- **Cause:** `BonusItemSampler` does uniform random sampling across the theme's bundles' `InPlayItemsFor`. Percentage bundles return ALL ingredients passing the obtainability predicate, with no rarity weighting. Adventurer's items + Crab Pot items dominate because Percentage bundles have many ingredients.
- **Fix (two-part):**
  1. **Rarity weighting.** Pass a `CcItem`-rarity lookup into the sampler. Weight inverse to rarity: Common=8, Uncommon=4, Rare=2, VeryRare=1. Replace uniform shuffle with weighted-pick (`System.Random` + weighted draw). Determinism preserved (same seed → same picks).
  2. **Exclude 0-quota Percentage bundles from in-play.** `BundleRequirement.InPlayItemsFor` for Percentage currently returns all obtainable ingredients regardless of cumulative quota. Add: if `CumulativeRequiredBySeason[(int)season] == 0`, return empty (the bundle isn't gated this season → no urgency → keep it out of the sampler pool to focus on what the player should be chasing).
- Update `BonusItemSamplerTests` with cases for both behaviors.

### UX5 — Bonus/liability effects are stubs (Plan 06 scope, but call it out)
- `ThemeModifiers.For(theme)` returns `(BonusId, LiabilityId)` stable strings. `DisplayNameFor` shows human strings like "+25% Foraging Yield". NO consumer wires these to actual gameplay effects.
- **What works today:** the 1.5× JP multiplier on bonus-list items (via `DonationService.IsSelectedBonusItem`).
- **What doesn't:** drop-rate buffs, growth-rate adjustments, sell-price multiplier — all displayed-only.
- **Action (Plan 06, not this branch):**
  - `forage_yield_up` → Harmony patch the forage spawn / harvest path; multiply `spawnQuantity`.
  - `crop_growth_up/down` → patch `HoeDirt.dayUpdate`; adjust the growth-progress tick.
  - `fish_bite_up` → patch `BobberBar.update` / `FishingRod` bite RNG.
  - `mine_drops_up` → patch `MineShaft.tryToAddOreClumps`, stone-node drop tables, monster loot.
  - `*_off` → conditional drop-table replacement (return empty for the disabled category).
  - `all_drops_up` → multiplier on every drop table.
  - `all_sell_prices_down` → patch `Object.sellToStorePrice`.
- **Also:** move the % numbers out of `DisplayNameFor`'s hardcoded strings into `GameplayConfig` so they're tunable without redeploy (TODO comment already in `ThemeModifiers.cs:27`).

### UX6 — Add a JP HUD
- User: *"There's not a JP display I can see, so I have no idea what's working or not."*
- The Junimo Shrine menu shows JP when open; `tly_meta` prints it. But there's no always-on HUD.
- **Action (Plan 06 or this branch — your call):** small JP counter in a screen corner showing:
  - Banked JP (large)
  - This week's selection (small badge)
  - Bonus-multiplier-active flag (subtle indicator while the selection's bonus item is in the player's inventory or in the bag)
- Style after vanilla money display. Hideable via config.

---

## Naming cleanup (deferred from the bundle-gate refactor)

The previous session renamed "champion → selection" everywhere (see commit `ce72446`). But the **file** `ContractPickMenu.cs` is still named after the dead `Contract` class. Consider renaming to `WeeklyHubMenu.cs` or `SelectionPickMenu.cs` (whatever fits — the UI is now bundle-shaped, not contract-shaped). Not blocking, but worth doing at the same time as UX2 (the menu redraw).

---

## Suggested execution order

1. **CB1 + CB2** — classifier fixes, unit tests. ~30 min.
2. **CB3** — investigate the Harmony patch. Decompile read + log inspection FIRST. Once you know why it doesn't fire, the fix is usually one-line. ~30–60 min depending on root cause.
3. **UX1 + UX3** — copy fixes in `BundleSeasonBadge`. ~10 min.
4. **UX4** — sampler rarity weighting + 0-quota Percentage exclusion + tests. ~45 min.
5. **UX2** — strip bundle rows from cards + new `SeasonGoalsMenu`. ~1–2 hours including a hotkey or GameMenu tab.
6. **Naming cleanup** — rename `ContractPickMenu.cs` while you're in it.
7. **UX6 (HUD)** + **UX5 (effects layer)** — defer to a Plan 06 branch; too big for this one. Mention to user when 1–6 are done.

---

## Workflow

- Same workspace rules as the previous session — see `2026-05-26-bundle-gate-handoff.md` §"Workflow rules" for the canonical list (Co-Authored-By footer, local commits only, no push, `<400 lines per file`, `dotnet test`, `dotnet build src/TheLongestYear/TheLongestYear.csproj`).
- Game may already be running when you start — deploy will IOException; **ask the user to close the game** before rebuilding. The previous session left the game running for the playtest.
- Existing memories that matter:
  - `feedback_selected_not_championed.md` — keep "selection" terminology
  - `feedback_meaningful_playtests_only.md` — solo-verify engineering correctness; request a playtest only when feedback will be more than yes/no
  - `feedback_no_auto_publish.md` — local commits only

## What I confirmed works (don't regress)

- Selection menu opens on save load (Spring 1 safety net fires when no `CurrentSelection`).
- `SelectionService.OfferForWeek` is deterministic per (seed, week).
- `BonusItemSampler` picks reasonable in-season items for the selected theme (the Foraging sample `[(O)16 Wild Horseradish, (O)18 Daffodil, (O)20 Leek, (O)399 Spring Onion]` is sensible).
- `DonationService.OnItemDonated` pays base JP + applies 1.5× bonus when `Run.CurrentWeekBonusItems.Contains(id)` (verified via `tly_testdonate`).
- CC pre-unlock + Junimo Note revealing (no in-game verification yet; engineering verified).
- 217 unit tests pass; no warnings; mod builds clean.
