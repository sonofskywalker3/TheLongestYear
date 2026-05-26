# Playtest feedback — 2026-05-26

## From the first playtest of the bundle-gate refactor (commits f45bcb5..ce72446)

### 1. Weird symbol next to crop growth — IDENTIFIED
- It's the `←` (U+2190 LEFTWARDS ARROW) I put between the bundle row and
  the "needs N this month" badge. User describes it as "a dot that was
  splatted, or a popped speech bubble" — classic missing-glyph rendering
  in Stardew's pixel smallFont, which doesn't include U+2190.
- **Action:** drop the arrow entirely. Just write the badge starting with
  "needs N before Summer 1" (per feedback item #3's phrasing). Also audit
  the `×` in "Bonus this week (1.5×):" — U+00D7 MULTIPLICATION SIGN may
  also be missing; fall back to ASCII `x` or `(1.5x)` to be safe.

### 2. Bundles do NOT belong on the hub
- The hub is where the player makes the WEEKLY SELECTION decision. The
  bundle progress + "items still required this season" list is a SEPARATE
  surface — needs its own access point.
- **Action:** strip the bundle-progress rows off the cards on the hub. Keep
  theme name + bonus + liability + the per-week bonus-items preview. Move
  the bundle progress + per-season required-items list to a new menu (e.g.
  a "Year Goals" / "Season Goals" tracker accessible from the Junimo Shrine
  or its own hotkey).

### 3. Inconsistent phrasing across BundleSeasonBadge
- Seasonal + PerItem badges: "needs N this month"
- Percentage badge: "needs N more by Spring"
- Player wants **all three to say**: "Before Summer 1" (using the NEXT-season
  day-1 framing instead of "this month" or "by <season>").
- **Action:** unify BundleSeasonBadge phrasing to `Before <NextSeason> 1`
  (e.g. "Before Summer 1", "Before Spring 1" at year wrap-around — but year
  wrap = end of Winter = run end, so that case doesn't appear in normal play).

### 4. Bonus list isn't weighted toward easy items (REGRESSION FROM v3 INTENT)
- Mining selection on Spring showed Adventurer's items that the player
  expected to be deferred. Specifically the Adventurer's quota was set to
  `0 / 1 / 2 / 2` precisely because those items (Solar Essence, Void Essence,
  Bat Wing) are mid-late-mine drops — they should NOT be the bonus list on
  Spring 1.
- Fishing selection on Spring showed 3 of 4 items being Crab Pot ingredients,
  not the "easy fish" (Chub, Sardine) the player expected to head the bonus
  list.
- **Root cause:** BonusItemSampler does uniform random sampling. Percentage
  bundles' `InPlayItemsFor` returns ALL ingredients that pass the obtainability
  predicate, with no rarity weighting. For Mining + Spring the pool includes:
  - Quartz, Earth Crystal (Geologist's, KIND 2 Spring-pinned)
  - Copper Bar (Blacksmith's, KIND 2 Spring-pinned)
  - All Adventurer's items obtainable in Spring (Bug Meat, Slime, +
    whichever Essences pass the predicate — the predicate currently defaults
    to "true if not in catalog" which means unknown items always pass)
- **Action:** add rarity-aware weighting to BonusItemSampler. Common items
  should appear far more often than Rare/VeryRare. Use CcItem.Rarity →
  weight (e.g. Common=8, Uncommon=4, Rare=2, VeryRare=1) and weighted-sample
  instead of uniform shuffle.
- **Additionally:** Adventurer's Bundle's Spring quota is 0 — there's no
  reason its items should be IN PLAY at all on Spring. Consider: for
  Percentage bundles, exclude from in-play sampling on seasons where the
  cumulative quota is 0 (i.e. no items required this season).

### 5. Bonus/liability effects are stubs — player surfaced this expecting it to work
- User asked "how does forage +30% work? Is it in effect on day one?"
  (Foraging actually displays +25%; +30% is Fishing's bite rate or Mining's
  drops — value confusion is itself a sign the labels need refining.)
- **Status:** ThemeModifiers maps Theme → (BonusId, LiabilityId) and
  DisplayNameFor returns the human-readable strings, but NO consumer wires
  these to actual gameplay effects. Only ContractPickMenu (display) and
  RunController (log line) read them. No Harmony patches; no drop multiplier;
  no foraging-disable code.
- **What IS active:** the 1.5× JP multiplier on bonus-list items (via
  DonationService.IsSelectedBonusItem). Everything else is cosmetic.
- **Action:** Plan 06 territory — wire each modifier id to its effect:
    forage_yield_up      → patch ForagedItem.checkForAction or
                           Crop.harvest forage path; multiply spawnQuantity.
    crop_growth_up/down  → patch HoeDirt.dayUpdate; adjust growth ticks.
    fish_bite_up         → patch BobberBar / FishingRod fish-bite RNG.
    mine_drops_up        → patch MineShaft.tryToAddOreClumps /
                           StoneNodeDrop / MonsterDrop tables.
    *_off                → conditional drop-table replacement returning
                           empty (or vanilla minus the themed category).
    all_drops_up         → tickle every drop table with a +10% multiplier.
    all_sell_prices_down → patch Object.sellToStorePrice; halve return.
- **Also:** move the % numbers from DisplayNameFor's hardcoded strings into
  GameplayConfig (per the existing TODO comment in ThemeModifiers.cs:27)
  so tuning doesn't need a redeploy.

## Things the user noted that already work
- Game launched, mod loaded with 8 mods total. Player is on Spring 1.
- The "needs 1 more by Spring" phrasing means the gate badge IS rendering
  on Percentage bundles — wiring works, just the copy is wrong.

## CRITICAL BUGS surfaced in playtest

### B1. Construction Bundle is silently dropped from the gate
- Vanilla Data/Bundles has `Wood 99` listed TWICE in the Construction bundle
  ingredients (parsed.NumberOfSlots=4, raw ingredients list has 4 entries
  but only 3 unique items after `BundleClassifier.CollectQualifiedIngredients`
  dedups them).
- My classifier: `if (parsed.NumberOfSlots == ingredients.Count)` falls through
  to "didn't match any classification rule" because 4 ≠ 3 (deduped).
- Net effect: Construction Bundle is NOT in `_requirements`, so it never
  appears on a card's bundle progress list AND its donations don't count
  toward fullCcDone.
- **Fix:** in `BundleClassifier.Classify`, when checking the X==Y PerItem
  case, allow `parsed.NumberOfSlots == ingredients.Count` OR
  `parsed.NumberOfSlots == parsed.Ingredients.Count` (the non-deduped count).
  The "true" X==Y is "every slot will be filled," which holds for Construction
  even with duplicate wood. Need to also be careful about IsFullyComplete
  semantics: donating Wood once shouldn't satisfy 2 vanilla slots — but our
  ledger is set-based so "donated Wood" is one-flag-fits-all. Decide whether
  fullCc demands two Wood entries (track stack count) or treats unique-id
  donation as sufficient (current behavior).

### B2. Chef's Bundle Y=X=7 with SVE crashes classifier
- Log: "Percentage bundle needs Y > X; got Y=7, X=7. (Parameter 'ingredients')
  -- skipping."
- SVE adds `Candy` to Chef's, bumping both Y and X to 7 (SVE makes it
  required to donate all of them, no quota).
- My DefaultBundleQuotas has "Chef's" → so classifier tries Percentage first,
  CreatePercentage validates Y > X, throws, and the whole bundle is skipped.
- **Fix:** in `BundleClassifier.Classify`, when bundleQuotas matches but
  parsed.NumberOfSlots >= ingredients.Count (X >= Y), fall through to the
  PerItem branch instead of throwing. Or: trim the quota array to
  Min(quota[i], ingredients.Count) and accept X = Y as a degenerate
  Percentage. Or: catch the throw and warn-and-fall-through. First option
  is cleanest.

### B3. Real CC donation does NOT fire the Harmony patch
- User reports donating one item in-CC and going to sleep. SMAPI log has
  ZERO "Donated 1x ... -> +N JP" lines from that session.
- Verified via tly_testdonate: the DonationService.OnItemDonated path works
  when invoked directly. So the issue is the Harmony patch on
  `Bundle.tryToDepositThisItem` not catching the real deposit.
- Possible causes:
  (a) User opened a Junimo Note but didn't actually deposit (slot didn't
      accept the item — gives an in-game UI bounce-back).
  (b) SVE overrides Bundle internally — its CP or Harmony patches replace
      the deposit method, breaking our patch coverage.
  (c) Our patch targets the wrong overload / signature.
- **Diagnostic next step:** ask user what item + which bundle they tried.
  If they confirm a visible deposit (sprite in slot, sound played), then
  it's (b) or (c) and we need to repatch — investigate SVE's compat patches
  on Bundle/JunimoNoteMenu and target the right method.

### B4. SVE adds 8 unrecognized bundles + 20 unresolvable SVE items
- 8 SVE bundles skipped: Fish Farmer's, Brewer's, Wild Medicine,
  Master Fisher's, Winter Star, Forager's, Home Cook's, Construction (B1).
- 20 SVE items unresolvable by ItemRegistry: FlashShifter.SVECP_Cucumber
  (Spring Crops), Butternut_Squash (Summer Crops), Sweet_Potato (Fall Crops),
  Butter (Artisan), Red_Baneberry (Summer Foraging), Mushroom_Colony
  (Fall Foraging), Bearberrys (Winter Foraging), Fir_Wax + Birch_Water
  (Exotic Foraging), Minnow + Goldfish (River Fish), Tadpole (Lake),
  Starfish (Ocean), Frog (Night Fishing), King_Salmon + Butterfish
  (Specialty), Candy (Chef's), Amber (Field Research),
  Lucky_Four_Leaf_Clover (Enchanter's), Persimmon (Dye).
- These are SVE Content Patcher items with the prefixed-id form. They
  probably DO load eventually but our BundleCatalogBuilder runs on
  SaveLoaded which may be too early. Or the qualified id form needs
  different normalization for SVE prefixed ids.
- **Action:** decide whether to (a) defer catalog build to a later event
  (e.g. DayStarted of day 1) so all CP items are loaded, OR (b) write
  default quotas for the 7 unknown SVE bundles + accept some items missing.

### B5. No JP HUD
- User: "there's not a JP display I can see, so I have no idea what's
  working or not."
- The Junimo Shrine menu shows JP when open. `tly_meta` prints it. But
  there's no always-on HUD element.
- **Action (Plan 06):** small JP counter in a screen corner showing
  banked JP + this week's selection + maybe the bonus-multiplier-active
  flag. Could mirror Stardew's money display style.

## Open questions to ask the user when they're done playing
- Was the hub actually visible on Spring 1 morning? (My screenshot showed
  it wasn't on screen — maybe they dismissed it before I captured.)
- If they opened it via `tly_openhub`, did the Sunday-night `OnDayEnding`
  trigger fire on day 7? (Will confirm via log when they finish.)
