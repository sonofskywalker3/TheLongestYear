# Reply drafts, 2026-08-25 (post with 0.13.0)

## Bug 1122358 (reply to ChaoticMindset + gazumbrado)
Thanks both. You're right: Fiber, the jellies and Tea Leaves never carry quality in vanilla (tea bushes hand out base-quality leaves, jellies come off the rod at base). 0.12.16 vetted the list of items but still rolled quality per pool. 0.13.0 fixes it at the root: quality asks are now only rolled for things the game itself gives quality to (crop harvests, real rod fish, spawned forage), so those slots can't come back. Existing boards pick it up at the next reset.

## Bug 1122901 (Bumblewyn, Keep pet)
Confirmed, thanks: the snapshot only kept the first pet it found. 0.13.0 keeps every pet on the farm (name, breed and hearts), placed next to each other by the porch after the rewind. Status: Fixed.

## Post (lexihope, cart restock)
Good catch, and no, that's not intended: the cap was applied every time the cart's stock was built, so buying an item let the next one in the merchant's list slide into the freed slot. 0.13.0 remembers the day's selection instead, so a purchase leaves the slot empty until the next visit.
