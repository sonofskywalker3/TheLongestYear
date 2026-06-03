# Cart Whisperer (bundle sense) — design (2026-06-03)

Replaces the broken cart-stock *preview* (it showed today's hypothetical stock seeded by the live
clock, never the real next-visit cart — confirmed in playtest).

**Scope (revised):** this spec is now **TLY-only** = the Cart Whisperer repurpose (Part 1). The
**Cart Catalog** is being built as its **own standalone mod** in the workspace `CartCatalog/` folder
(book bought from the cart for gold), with **no TLY upgrade**. TLY just coexists: the Cart Catalog
mod re-offers the book whenever the player doesn't currently own one, so it's naturally re-buyable
each loop, and the Junimo Stash can carry it across loops. No TLY-side coupling code is required.

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

### Migration / display
The player may own `cart_whisper_1/2/3`. Keep `cart_whisper_1` as the single upgrade (owning it = has
the feature); drop `cart_whisper_2/3` from the catalog (defunct ids in OwnedUpgrades are harmless).
**Display name is just "Cart Whisperer"** — never "I"/tier-numbered, since the chain is gone. Remove
the old `CartStockPreview` slot logic + the broken hub/shrine cart-preview rows.

---

## Testing
- **Core (unit):** `BundleRelevance` membership (direct / seed→crop / ingredient→product / negative).
- **Glue (log/playtest):** Cart Whisperer shows the right relevant items on a visit day; "no cart
  today" + next weekday off-days.

## Cross-reference — Cart Catalog (separate mod)
Built as its own standalone mod in the workspace `CartCatalog/` folder — a "Cart Catalog" book bought
from the Traveling Cart that mail-orders from the cart's daily stock (+5% markup, next-morning
delivery, usable any day). **No TLY upgrade.** TLY compatibility is automatic: the mod offers the book
only when the player doesn't currently own one (so it re-sells each loop after the reset wipes
inventory), and the book is a normal item the Junimo Stash can carry across loops. See
`CartCatalog/docs/standalone-design.md`.
