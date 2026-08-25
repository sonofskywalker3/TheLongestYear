# Reply drafts, 2026-08-25 (post with 0.13.0)

Jeff's instruction: tell them about the fixes and ask for more feedback from testers before closing.
Bug 1122901 stays open (New issue) until someone confirms; 1122358 is already marked Fixed, so the
reply asks for confirmation on the new items rather than changing status.

## Bug 1122358 (reply to ChaoticMindset + gazumbrado)
Thanks both. You're right: Fiber, the jellies and Tea Leaves never carry quality in vanilla (tea bushes hand out base-quality leaves, jellies come off the rod at base, Fiber is capped at base by the game's own crop data). 0.12.16 vetted a list of items but still rolled quality per pool. 0.13.0 fixes it at the root: quality asks are only rolled for things the game itself gives quality to (crop harvests, real rod fish, spawned forage), so those slots can't come back. Existing boards pick it up at the next reset.

If you can, give 0.13.0 a loop or two and tell me whether any silver/gold ask still turns up on something that can't have it, or anything else on the board looks impossible. I'd rather hear about it here than assume it's clean.

## Bug 1122901 (Bumblewyn, Keep pet)
Confirmed, thanks: the snapshot only kept the first pet it found. 0.13.0 keeps every pet on the farm (name, breed and hearts), placed next to each other by the porch after the rewind. Existing saves pick it up on their next reset.

Could you (or anyone with more than one pet) confirm after a reset on 0.13.0 that they all come back? I'll leave this open until someone has seen it work.

## Post (lexihope, cart restock)
Good catch, and no, that's not intended: the cap was applied every time the cart's stock was built, so buying an item let the next one in the merchant's list slide into the freed slot. 0.13.0 remembers the day's selection instead, so a purchase leaves the slot empty until the next visit. If you see it refill again on 0.13.0, let me know.
