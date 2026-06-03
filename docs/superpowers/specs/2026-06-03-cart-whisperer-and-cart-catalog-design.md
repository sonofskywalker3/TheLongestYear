# Cart Whisperer (bundle sense) + Cart Catalog — design (2026-06-03)

Replaces the broken cart-stock *preview* (it showed today's hypothetical stock seeded by the live
clock, never the real next-visit cart — confirmed in playtest). Two features:

1. **Cart Whisperer** (repurpose the existing upgrade): on a cart day, flag which of the cart's
   real stock is useful toward *any* CC bundle.
2. **Cart Catalog** (new, built fully into TLY): a 1000 JP book that mail-orders from the cart's
   daily stock. Also documented in the `CartCatalog/` workspace folder for a future standalone mod.

---

## Part 1 — Cart Whisperer = "is the cart worth visiting?"

### Behavior
- Collapse the 3-tier Cart Whisperer chain into **one** upgrade, **350 JP**, keep the name
  "Cart Whisperer".
- On a **cart-visit day** (the cart is in town: `Forest.ShouldTravelingMerchantVisitToday()`), the
  shrine shows which of the cart's **actual current stock** (`ShopBuilder.GetShopStock("Traveler")`,
  which is the real cart on a visit day) is **bundle-relevant**. On non-visit days: "No cart in town
  today" + the next visit weekday.
- Not predictive — it reads the real present-day stock and tells you whether a trip is worth it.

### Bundle-relevance set (data-driven, cached in Core)
An item is relevant if it can contribute to **any** vanilla CC bundle, regardless of what's required
this run:
- **Direct**: the item id is a bundle ingredient (`Data/Bundles`, all bundles).
- **Seed → crop**: the item is a seed whose harvested crop is a bundle item (`Data/Crops`:
  seed item → `HarvestItemId`).
- **Ingredient → product**: the item is an ingredient of a crafting/cooking recipe whose product is
  a bundle item (`Data/CraftingRecipes`, `Data/CookingRecipes`).

Build this set once (lazily) from content and cache it. The membership test is pure and unit-testable
in Core (`BundleRelevance` — feed it the three maps, assert direct/seed/ingredient hits).

### Migration
The player may own `cart_whisper_1/2/3`. Keep `cart_whisper_1` as the single upgrade (owning it = has
the feature); drop `cart_whisper_2/3` from the catalog (defunct ids in OwnedUpgrades are harmless).
Remove the old `CartStockPreview` slot logic + the broken hub/shrine cart-preview rows.

---

## Part 2 — Cart Catalog (built into TLY; 1000 JP)

### Behavior
- A **1000 JP** upgrade grants the **"Cart Catalog"** book (a 4th book alongside cookbook/craftbook/
  bundle-log; reconciled by `BookFurniture`). In TLY the book is **only** obtainable this way — it is
  **not** sold by the cart (that path is the standalone mod's, documented separately).
- Open the book any day → a menu listing **today's** cart stock (`GetShopStock("Traveler")` — the
  same rotating daily stock the cart would carry) → select items → pay **cart price × 1.05** (5%
  shipping markup) in gold → items **arrive by mail next morning**.
- Usable **every day** (the markup is the incentive to walk to the real cart when it's in town).

### Components (glue, log-verified + playtest)
- **Upgrade**: new catalog entry `cart_catalog` (Foresight or a new "Convenience" category), 1000 JP.
- **Book item + menu**: follow the existing book pattern (cookbook/craftbook). The catalog menu
  renders the day's stock (icon, name, price×1.05, stock count) and an "order" action.
- **Ordering**: deduct gold immediately; queue a next-morning mail delivery with the item attached.
  - **Mail-with-item**: vanilla supports mail letters with attached items (`%item object … %%` /
    `Game1.player.mailForTomorrow` + an attached `Item`). Verify the exact API in build; fall back to
    a `DayStarted` "deliver queued orders to the mailbox" handler if direct mail attachment is fiddly.
  - Pending orders persist in MetaState (so a save/quit before delivery isn't lost).
- **Markup math** is pure → unit-test in Core (`price → ceil(price × 1.05)`).

### Open build-time checks (verify, don't guess)
- Exact mail-with-attached-item API on PC 1.6.15 (decompile `Game1.mailbox` / mail letter item attach).
- Whether `GetShopStock("Traveler")` off a visit day returns a sensible "today's catalog" (it does —
  it's seeded by the day regardless of the cart physically visiting).

---

## Testing
- **Core (unit):** `BundleRelevance` membership (direct / seed→crop / ingredient→product / negative);
  the 5% markup rounding.
- **Glue (log/playtest):** Cart Whisperer shows the right relevant items on a visit day; Cart Catalog
  orders deduct gold and the item arrives next morning; book granted by the 1000 JP upgrade.

## Cross-reference
The standalone-mod design lives in the workspace `CartCatalog/` folder (docs only — acquisition via
the cart for gold, ~50% appearance until bought, ~5,000g; everything else identical). It is enriched
with real API findings as the TLY build proceeds.
