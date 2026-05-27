# The Longest Year — TODO

Ongoing scratchpad for design / feature ideas captured during playtesting.
Items here are NOT yet planned; they need spec'ing before execution.
Once an item is planned, it moves into `docs/superpowers/plans/`.

## Open

### Weekly Theme Journal entry (active quest for bonus items)
Source: 2026-05-26 playtest discussion with user.

Each week's selected theme creates a Stardew Valley journal entry (the
quest log, accessible via the bookmark icon on the menu). The entry:
- Lists the 4 bonus items for that week (uses the same sample as the hub
  card so it stays consistent).
- Marks each item as "donated" when the player donates one to the CC.
- Completes when all 4 are donated.
- **On completion, suppresses that week's liability for the remaining
  days of the week.** The bonus stays active either way.

Goals served:
1. Harder push to chase the bonus items — completing the journal gives
   the player a tangible "I beat the liability" payoff beyond the 1.5×
   JP per item.
2. Reminder UI — if the player puts the game down mid-week and comes
   back, the journal entry tells them what the bonus items were
   without having to re-open the hub.

Implementation sketch (not yet a plan):
- Hook into vanilla's `Quest` system OR build a custom journal entry
  via Content Patcher / SMAPI.
- New `RunState.LiabilitySuppressedThisWeek` flag (bool, cleared on week
  transition in `OnDayStarted`).
- The effects layer (Plan 06, currently deferred) reads the flag and
  skips applying the liability when true.
- A donation observer (we already have one — `DonationObserver`) can
  flip the per-item completion state; when all 4 are flagged, set the
  liability-suppressed flag.
- Edge case: if one of the 4 bonus items is in a bundle that's already
  fully complete (no slot to donate into), the quest is unachievable.
  Either pick replacement items at sample time (skip already-satisfied
  bundles), or accept incompleteness and document it.

## Deferred to Plan 06

- **UX5 — effects layer.** Wire `forage_yield_*` / `crop_growth_*` /
  `fish_bite_*` / `mine_drops_*` / `all_drops_up` / `all_sell_prices_down`
  to actual gameplay effects. Currently display-only via
  `ThemeModifiers.DisplayNameFor`.
- **UX6 — always-on JP HUD.** Small corner counter showing banked JP +
  this week's selection + bonus-multiplier indicator.

## Resolved / closed

(empty — items move here after they're shipped)
