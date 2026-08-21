# Nexus reply drafts — 2026-08-21 sweep (NOT POSTED — needs "yes, push")

Post with `AndroidConsolizer/release-notes/nexus-bug-reply.mjs` (bug threads; set status) /
`tly-post-comment.mjs` (posts tab) once the fixed build is live. Replace {VERSION} with the release.

## Bug 1108030 — Loop reset on remix bundles → set "Fixed"
Thanks all — and sorry this one sat so long. Root cause: the game keeps the Standard/Remixed choice in a
value that isn't saved, so when the mod rebuilt the world on a reset the game handed back the standard
board every time. {VERSION} takes bundle generation over entirely: every loop gets a freshly rolled board
(vanilla + remix pools + a few new authored bundles), so the choice you made at farm creation no longer
matters. The green book regenerates with it — no regenerate_bundles / reload dance needed.

## Bug 1109718 — Cultivation: Red Cabbage/Starfruit → "Fixed"
You were right, it was 0%. The patch was attached to the wild-seeds code path (hence cabbage from Summer
Seeds) instead of the Mixed Seeds one. Fixed in {VERSION}: Mixed Seeds planted in Summer now have the
10% roll per owned upgrade, and Summer Seeds go back to normal.

## Bug 1111046 — Items in Junimo Chest don't stay → "Fixed"
Confirmed — the stash was only snapshotted when the game saved, so day-28 deposits were restored from the
night-before snapshot. {VERSION} banks the chest at the moment of the rewind. Anything lost is unfortunately
gone, but it won't happen again.

## Bug 1107194 — Ancient Seed / museum rewards → "Fixed"
Good catch on the pattern (seeds come back, statues/recipe don't). 1.6 tracks the one-of-a-kind rewards on a
separate list the reset wasn't clearing. {VERSION} clears it, so from your next rewind onward every museum
reward is available again each loop.

## Bug 1113630 — CC ceremony doesn't trigger → "Fixed"
Excellent write-up, thank you. Embarrassing one: the mod suppresses the Spring-5 "let me show you the
Community Center" intro, and it had the wrong event id — it was suppressing the completion ceremony instead.
{VERSION} fixes the id. Your save should recover on its own: walk into town on the next sunny day and the
ceremony (and everything downstream — Joja closing, Pierre's Wednesdays, the lightning) will play.

## Bug 1116791 — Rain totems / Bug 1107279 — Rain will not occur → "Fixed"
Same root cause for both: the weather schedule was being re-applied every morning, which overwrote whatever a
totem / CJB / console set the day before, and the schedule itself only had two wet days a season. {VERSION}
writes the schedule the morning before (so anything you set later that day wins, just like vanilla) and
brings the density up to vanilla-like numbers.

## Bug 1115192 — Caroline Tea Sapling recipe → "Fixed"
Thanks — the replay detection looked for recipe/flag grants inside the cutscene but not for "send a letter",
which is how Caroline's event delivers the recipe. {VERSION} covers letter-delivered unlocks too, so it
replays each loop once you're back at two hearts.

## Bug 1110130 — Hay feeder disappears → "Fixed"
Confirmed and fixed in {VERSION}: kept buildings were being created without the interior setup the game does
on construction (which is why upgrading — which re-runs it — brought the hopper back). Existing hopper-less
coops will get it back on the next rewind; until then the upgrade workaround is the fix.

## Posts tab
- **faldans** (day-28 JP screen flash → Summer 1): thanks for the log line — that was exactly it. The owl
  event ends on a tick where the game briefly looks idle, our scene opened in that gap, and the game's own
  save screen replaced it. {VERSION} skips the overnight event on fail nights and re-opens the scene if
  anything replaces it. If you still have the day-28 save, loading it on {VERSION} will rewind properly.
- **CausticOptimist**: bait on a kept rod is fixed in {VERSION} (kept tools now keep attachments +
  enchantments). Multiplayer is still untested/unsupported — noted as the top feature ask.
- **SilencedLink**: Keep Coop keeps a *basic* coop; Keep Big Coop / Keep Deluxe Coop are separate shrine
  upgrades (chained) if you want the tier back.
- **Bumblewyn** (empty themes): intended — when the theme's pool has no open bundle slots left there's
  nothing to ask for, so the drawback lifts immediately. I'll make the in-game message say so.
- **Reddit / Thrippalan**: the one-item cart is the mod — Joja's squeezing the merchant's suppliers; the
  Cart Stall upgrades at the shrine add slots. {VERSION} explains this in-game on the first visit and adds a
  config toggle (LimitTravelingCartStock) if you'd rather have the full cart.
