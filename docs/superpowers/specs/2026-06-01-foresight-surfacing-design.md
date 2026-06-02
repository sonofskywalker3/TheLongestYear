# Foresight Surfacing — design (2026-06-01)

First of two specs splitting the remaining connected work (chosen "foresight first"). The second
spec covers the loop/reset narrative (day-28 cutscene, event gating, save churn, recipe reset,
FarmHouse furniture) and is **not** part of this doc.

## Goal

Make the Weather Sage forecast actually useful, and surface both Weather Sage and Cart Whisperer
foresight on the read-only Junimo planning shrine — not just the once-a-week planning hub.

## Problems (from 2026-06-01 playtest)

1. Weather rows are labeled `Day 1, Day 2, …` (a relative offset). "Day 1" reads as *today* — the
   day the theme is picked — which is useless, because today's weather is already locked in.
2. The forecast tops out at a **7-day** tier (`weather_sage_7`, "full 7-day forecast"). Since the
   first useful day is *tomorrow*, the meaningful horizon is **6 days** (days 2–7 of the week).
3. Labels should be **day-of-month**, not a relative offset (and not day-of-week).
4. Foresight only appears on the weekly hub. The shrine (checkable any day) shows none of it, so
   revealed info can't be re-referenced later in the week.

## Decisions (locked with user)

- **Day label format:** day-number only. Numbers track the real calendar, so a low number only
  appears when the window rolls into the next season.
- **Shrine layout (revised after first look):** a **pinned calendar-style panel** at the top of
  `ShrinePreviewMenu`, then the scrollable upgrade list below. The first attempt stacked one text
  row per day, which ate the whole window — replaced with a compact horizontal calendar:
  - **Weather:** a single `Weather` header, a row of day-of-month **numbers**, and beneath it a row
    of the in-game **HUD weather icons** (from `LooseSprites\Cursors`, the same sprites the TV/HUD
    use). Hover a column → `Day N - Weather` tooltip.
  - **Cart:** a single header `Traveling Cart - <Weekday>` (the next visit's weekday *name*, e.g.
    "Friday" — cart days are always Fri/Sun), then a horizontal row of the revealed item **icons**.
    Hover an icon → name + cart price tooltip.
  - The window is **~50% larger** (1260×1020, capped to viewport) so the calendar and the upgrade
    list both have room.

## Behavior

- **Rolling "next N days" window, starting tomorrow.** Tier *N* of Weather Sage reveals the next
  *N* days (tomorrow … tomorrow + N − 1). `WeatherForecast.Build` already begins at tomorrow.
- **Weather Sage caps at 6 tiers.** Drop `weather_sage_7`. Tier *N* = *N* days, max 6.
- **Same forecast on both surfaces.** The hub (opened on a week-start morning) shows days 2–7; the
  shrine, opened any day, rolls forward. Identical code path — only `Game1.dayOfMonth` differs at
  open time, which makes the shrine roll for free.
- **Cart Whisperer unchanged** in tiers (1–3 → 2/4/6 items); now also shown on the shrine.

## Components

### Core (`TheLongestYear.Core` — unit-tested, TDD)

- **`ForecastDay` record:** `(int SeasonIndex, int DayOfMonth, string Weather)`. Pure; carries the
  calendar day so the UI can label it without re-deriving the date.
- **`WeatherForecast.Build(...)`** returns `ForecastDay[]` instead of `string[]`. Day-advance and
  season-wrap logic is unchanged; it now records the day number it landed on per slot. Existing
  callers + tests updated.
- **`UpgradeCatalog`:** remove `weather_sage_7`; reword tiers 1–6 from "next week's weather" to
  "the next N days' weather" (it is rolling now, not week-bound).
- **`RunController.WeatherSageTier()`** cap `7 → 6`.

### UI glue (log-verified)

- **`WeeklyHubMenu`:** label weather rows `Day {ForecastDay.DayOfMonth}: {Weather}` from the new
  `ForecastDay[]`. Cart rows unchanged.
- **`ShrinePreviewMenu`:** add the pinned calendar panel above the scrollable list (see revised
  layout decision above). The menu already holds `MetaState`, so it computes tiers itself via
  `HighestKeptTier("weather_sage_", 6)` / `HighestKeptTier("cart_whisper_", 3)`, and reads live
  `Game1.uniqueIDForThisGame / dayOfMonth / season` at construct (rolling per open). Cart stock via
  `ShopBuilder.GetShopStock("Traveler")` (the existing shop id) inside a try/catch; price from each
  entry's `ItemStockInformation.Price`. Weather icons drawn from `Game1.mouseCursors` with the TV
  source rects (Sun 413,333 / Rain 465,333 / Storm 413,346 / Snow 465,346 / Festival 413,372, 13px,
  scaled 3×). Next cart weekday from `NextCartVisitDay` (`dayOfMonth % 7 % 5 == 0`). Neither tier
  owned → no panel; list sits where it does today.

## Migration

A save that already owns `weather_sage_7` is harmless: `HighestKeptTier("weather_sage_", 6)` caps
the reveal at 6 and `_7` is simply no longer offered for purchase. No refund logic.

## Testing

- **Core:** `Build` returns correct day numbers across a season boundary (Spring 25 → 26,27,28,
  Summer 1,2,3), correct slot count per tier, festival sentinel preserved, day-1/day-2 forced Sun.
- **Glue:** log-verified — panel renders, tiers gate correctly. Playtest only to eyeball the panel.

## Known limitation (documented, not fixed here)

A window that rolls past day 28 assumes the run continues into the next season under the same seed.
If that boundary is actually a *reset* (gate-closed year loop), post-reset weather differs. This
affects only the last day or two of a season, and the reset path is the second spec's concern.
