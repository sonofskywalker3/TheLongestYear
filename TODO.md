# The Longest Year — TODO

Ongoing scratchpad for design / feature ideas captured during playtesting.
Items here are NOT yet planned; they need spec'ing before execution.
Once an item is planned, it moves into `docs/superpowers/plans/`.

## Open

### ▶ NEXT SESSION: run the netWorldState audit

Jeff, 2026-08-26 evening: a fresh agent should run the audit tonight. Everything that agent needs is
in **`docs/superpowers/HANDOFF-2026-08-26-networldstate-audit.md`** - it is self-contained, so start
there rather than reading this file top to bottom. Background and rationale are in the SYSTEMATIC
entry further down.

Also queued, needing Jeff first: **the difficulty setting** (top ask from emmalution's stream). Jeff
is brainstorming it tomorrow - do not design it unilaterally. The GMCM "Features" section added in
0.14.2 is where it would live.


### 0.14.2 (built, NOT released) - Shop Discount discounts the price, not the payment

Jeff: "why doesn't the price reduction jp buy change the posted item prices?" Because it patched
`ShopMenu.chargePlayer`, the gold-deduction chokepoint, so the shelf kept vanilla's number - and
worse, vanilla gates the sale on that full price before it ever charges you (decompile
ShopMenu.cs:1631), so Shop Discount V with 90g could not buy a 100g item it would only have charged
75g for. Now postfixes `ShopBuilder.GetShopStock`, which fixes the posted price and the
affordability gate together.

Jeff's rulings: do NOT extend it to buildings (CarpenterMenu) or animals (PurchaseAnimalsMenu) -
both deduct gold directly and were never covered by the old patch either - and do NOT discount tool
upgrades. Descriptions reworded from "X% off all shop purchases" to name the exclusions.

**Verified in-game 2026-08-26 (0.14.2 deployed, save None_447449779, shop_discount_1 owned = 5%):**

| Shop | Vanilla | Shown | |
|---|---|---|---|
| Pierre, Parsnip Seeds | 20 | 19 | discounted |
| Pierre, Bean Starter | 60 | 57 | discounted |
| Pierre, Cauliflower Seeds | 80 | 76 | discounted |
| Pierre, Potato Seeds | 50 | 48 | discounted (47.5 rounds away from zero) |
| Clint, Copper/Iron/Gold Ore | 75/150/400 | 71/143/380 | discounted (regular stock) |
| Clint, Steel Axe upgrade | 5000 | 5000 | EXEMPT |
| Clint, Copper Hoe / Pickaxe | 2000 | 2000 | EXEMPT |
| Clint, Gold Watering Can | 10000 | 10000 | EXEMPT |

Screenshots `test-output/shop-0*.png`. Tool upgrades are only in the `ClintUpgrade` shop, not
`Blacksmith` - `debug shop Blacksmith` shows ore only.

### Playtest tooling rebuilt (2026-08-26) - `tools/game.ps1` + `tools/screenshot.ps1`

The old helpers lived in `test-output/`, which is gitignored, so they were never in the repo. They
are in `tools/` now, and the input problem that blocked two repros this session is fixed:

- **An unfocused SDV is a PAUSED SDV.** Queued `debug warp` commands do not run and PrintWindow
  keeps returning the last frame, so a sleeping game looks exactly like a failed command. Every
  action in game.ps1 focuses first.
- **SetForegroundWindow alone does not work and fails silently.** The foreground lock ignores it
  unless the caller owns the foreground. Keyboard input then goes nowhere, because XNA reads keys
  with GetKeyboardState (per input queue) - which is precisely why key presses "did not move the
  farmer" while mouse clicks worked (a click focuses the window under the cursor as a side effect).
  Focus() attaches our input queue to the foreground thread to lift the lock, then VERIFIES with
  GetForegroundWindow, and the script exits non-zero if it cannot.
- Walking needs a HELD key (`-Walk right -Ms 1500`); a tap moves the farmer a couple of pixels.
- `pwsh -File` passes every argument as a string, so `-Click 707,530` arrived as one string and an
  `[int[]]` cast silently produced 707530: a click at nonsense coordinates that still reported
  success. Coordinates are parsed explicitly now.
- Add-Type failures used to leave every later call a silent no-op that still printed success;
  game.ps1 now proves the type exists and aborts if not. Capture stayed in screenshot.ps1 because
  input and capture need different assemblies, and screenshot.ps1 now honours absolute paths
  instead of quietly writing next to itself.


### RELEASED 0.14.1 (2026-08-26) - festival main events once per day + weekly-goal bundle cap

Both from Jeff watching emmalution's stream.

**Festival repeat (Egg Hunt x3 in one day, and the Luau soup the same way).** Not a loop problem:
TLY festivals deliberately do not end the day (FestivalTimeFlow), so the festival map stays
re-entrant for the rest of its window and walking back in restarts the whole festival with the host
offering the main event again. Confirmed live: warped into Town at 10:20am on Spring 13, left,
re-entered, and the complete festival came back at 12:40pm. Guard is on
`Event.answerDialogueQuestion` (the one place a "yes" to the host starts a main event, so it covers
every festival, not just the Egg Hunt); the stamp is festival id + day so it expires at sunrise.
Verified from the decompile that "yes" ONLY ever means start-the-main-event (the Flower Dance
partner ask is a separate "danceAsk" key), so the guard cannot misfire.

**Weekly goal asking for 3 items from a 2-slot bundle.** Real, and 0.14.0 made it worse: the pool
holds every open line of a bundle that only requires some of them, so the sampler could put three
goals in a bundle that needs two. Until 0.14.0 vanilla's blanket flag flip on completion ticked the
leftover goal; with the deposit rule that ask is impossible and the week cannot be completed.
`BonusSlotSampler` now takes `remainingNeedForBundle` and never exceeds it;
`RunController.RemainingNeedForBundle` computes required-minus-completed from live CC state.

**VERIFIED IN-GAME 2026-08-26 (Jeff played it, 0.14.1 deployed, Spring 13):**

    [16:49:14] Festival main event starting: 'festival_spring13' (day 12); further runs today are blocked.
    [16:53:13] Festival main event blocked: 'festival_spring13' already ran today (day 12).

First Egg Hunt ran, then leaving Town and asking Lewis again was refused. End to end, on a real save.

Game-driving note for next time: the automation path could NOT set this up. Keyboard input never
moves the farmer (mouse clicks land, key presses do not), and with the window unfocused the game
pauses so queued `debug warp` commands sit unprocessed and PrintWindow keeps returning a stale
frame - which reads exactly like a warp that failed. Deploy, load the save and set the date from
the console, then hand the keyboard over; do not try to drive the farmer. Worth solving properly
(SendInput to an unfocused SDL window) before the next session that needs a walked repro.


### OPEN QUESTION (Jeff, 2026-08-26, from emmalution's stream): repeating the Egg Hunt every loop

Jeff watching the video: "she did go do the egg hunt twice, that shouldn't happen, can we avoid that?"

What happens today: nothing suppresses festivals across loops. Every rewind puts the player back at
Spring 1, so Spring 13 comes round again and the Egg Festival plays in full, egg hunt minigame and all.
`FestivalTimeFlow` already treats a festival as a real time cost (time runs normally inside it, the
player is ejected at the scheduled end), which is deliberate: attending is meant to be a choice that
costs hours. What it does not do is notice that the player has seen this one before.

Hard constraint on any fix: **the Egg Festival is the only source of Strawberry Seeds**, which matter
a lot on a Spring-heavy board. Blocking attendance outright would quietly nerf every run.

Options, needs a ruling before anything is built:
- **(a) Skip prompt on entry.** "You have done this one before. Take part / just browse the stalls."
  Skipping hands over the participation reward and gives the rest of the day back. Keeps the shop and
  the seeds, drops the repeat minigame.
- **(b) Auto-award on repeat.** Walking in grants the egg hunt prize immediately and leaves the player
  free to shop. No prompt, less friction, but it removes the choice.
- **(c) Leave the minigame, cut the time cost on repeats.** The festival still plays but a repeat
  costs fewer in-game hours.
- **(d) Do nothing.** A player who does not want the hunt can already walk out.

This is the same class as the event-hygiene pass (event 65 / CaveChoicePrompt): keep the mechanic the
scene carried, drop the scene once it has been watched. The festival version of that is (a).


### Smoke 2026-08-26 (0.14.0 deployed, Clone throwaway save `None_447355732`): ALL PASS

Driven with `tools/send-smapi-command.ps1` + `test-output/click.ps1` (which gained a `-Key` switch for
real virtual keys). Screenshots `test-output/smoke-0*.png`.

| Check | Result | Evidence |
|---|---|---|
| Fail night -> hold prompt -> KEEP -> pity DECLINED -> shrine opens | PASS | `Hold choice: KEEP (cost 0 JP)`, `Pity offer: declined`, **`Opened Junimo Shrine (JP: 38)`** (smoke-07) |
| Fail night -> hold prompt -> RESHUFFLE -> pity ACCEPTED -> shrine opens | PASS | `Hold choice: Reshuffled (seed loop 43)`, `Pity offer: ACCEPTED (Trim...)`, **`Opened Junimo Shrine (JP: 38)`** |
| Shrine closes -> reset runs, JP intact | PASS | `Loop reset complete. Run 43 begins` then `Run 44 begins`; JP stayed 38 across both |
| No silent fall-through | PASS | the new `Junimo Shrine could not open` warning never fired |
| Petless rewind re-opens adoption | PASS | `PetCarryover: no pet on the farm after the rewind; stamped MarniePetRejectedAdoption`, on both resets |

Before this fix the shrine line was simply absent and the night went hold prompt -> weekly focus -> Day 1,
which is what SincerelyZoey and SilencedLink reported. Both of 0.13.0's Fail-night paths were affected;
only the Win path and the Vanilla-board path (called from OnCutsceneEnded, not from inside a question
callback) were ever fine.

NOT verified in-game: that Marnie's counter actually lists "Adopt" on the next loop (that needs a walk to
Marnie's in a fresh loop). The mail flag is the documented gate in the decompile and the stamp is
confirmed in the log. Also unverified live: the weekly-goal deposit rule (Core-tested, 9 new unit tests) -
proving it in-game needs an n-of-m bundle finished with other items, which is a long grind.


### Triage of the 2026-08-26 YouTube + Nexus findings (code-checked, no game run)

**1. Weekly theme goals tick for items you never donated - REAL, needs a ruling.**
@ggrace67 is right. `WeeklyThemeQuestService.IsSlotComplete` reads vanilla's per-ingredient bools
(`Game1.netWorldState.Value.Bundles[bundleIndex][ingredientIndex]`, via `RunController.SlotStateForBundle`).
The service's doc comment assumes "vanilla only marks a slot complete once the full required stack and
quality are deposited" - that assumption is wrong for n-of-m bundles. On completion vanilla blanket-flips
**every** ingredient bool in the bundle to true (decompile: `JunimoNoteMenu.cs` lines 1009-1011, and again
at 1085), so a goal slot in a "5 of 9" bundle you finished with the other 4 items ticks for free, pays the
weekly JP and lifts the drawback. Ruling needed:
  (a) require a real donation: record `(bundleIndex, ingredientIndex)` in `DonationService.OnItemDonated`
      into a new per-week set on RunState and AND it with the live bool. Goals are only ever sampled from
      OPEN slots, so nothing legitimate is lost; costs one small persisted list.
  (b) accept it as-is: finishing a whole bundle is not nothing, and the goal slot was genuinely part of it.
  (c) middle: credit it, but only if the bundle completed AFTER the theme was picked.
Recommend (a) - the drawback is meant to be paid off with hand-ins, and (b) makes "pick a theme whose goals
sit in a bundle you were about to finish anyway" the dominant strategy.

**2. Demetrius' cave cutscene not re-firing - NOT A BUG, working as designed.**
@nancyjohnson7147's "this cave seems familiar to you" popup IS the mod: `CaveChoicePrompt` (event-hygiene
pass, 2026-06-10). Event 65 plays once per playthrough and from loop 2 stays in the eventsSeen re-seed;
the mushrooms-vs-bats choice is re-offered by a one-line question on cave entry because `FarmerReset`
clears `caveChoice`. Nothing to fix. It is not written down anywhere a player would find it, which is why
it reads as a bug: worth a line in the Nexus description / README FAQ.

**3. rose1729's missing pet offer - root cause found, needs a ruling.**
Both vanilla doors to a new pet are shut after a reset, which is why loops 2 and 3 never offered one:
  - The pet-arrival cutscene: `FarmerReset` clears `eventsSeen` and then re-seeds it from the cross-loop
    "seen ever" set, marking every non-replayable id as already seen. Only the furnace teach (992553) and
    the Demetrius cave (65) are exempt (`EventGatingTables.Default`), so the pet scene is re-marked seen
    every loop and can never re-fire.
  - Marnie's counter "Adopt" option: vanilla gates it on `(Utility.getAllPets().Count == 0 && Game1.year >= 2)
    || mailReceived "MarniePetAdoption" || "MarniePetRejectedAdoption"` (decompile `GameLocation.cs:10908`
    and `:10935`). After a reset the year is back to 1 and `FarmerReset` clears mailReceived, so the option
    is not on the list either.
Options: (a) add `MarniePetRejectedAdoption` to mailReceived on reset when no pet was restored, which turns
Marnie's Adopt option on immediately and costs nothing else; (b) mark the pet-arrival event replayable when
the farm has no pet (needs its id - `tly_dumpevents` in-game will give it); (c) leave it and say so in the
Keep Pet upgrade text. (a) is the smallest and matches the earlier animals ruling ("start over at 0 hearts").

**4. Stash slot "eaten" by a hat (@whisperinwind87) - NOT A BUG REPORT. Closed 2026-08-26.**
"You inspired me to try this mod, and not [now] one of my slots of my stash will be forever taken by
a certain hat I got outside the pub." That is a player telling us about a souvenir they intend to
carry through every loop even though it does nothing for them. Nothing is broken; it was logged as a
defect only because the sweep was reading comments looking for defects. Filed here as the correction,
not the investigation: ask "does this describe something broken?" before it goes on a docket.


### CRITICAL (2026-08-26 sweep) - Nexus bug 1123181: the JP perk screen never opens on reset

**ROOT CAUSE FOUND + FIXED IN CODE (0.13.1, not smoked yet, not released).**

Vanilla runs `GameLocation.answerDialogue` (which calls our afterQuestion callback) and only *then*
`tryOutro()`s the DialogueBox (verified in the decompile: DialogueBox.receiveLeftClick line 528 calls
answerDialogue before tryOutro). So while the hold callback runs, the DialogueBox is still
`Game1.activeClickableMenu`, `MenuLauncher.CanOpen` refuses ("another menu is already open", Trace, so
invisible), and `TryOpenShrineThenContinue` takes its silent fall-through straight into
`ContinueAfterResetSpend`: reset, no shop. Introduced in **0.12.17** (`e352fff`, the hold prompt), which
is exactly the version the reports start at. All three Fail-night call sites were affected (hold, pity
accept, pity decline); the Win path and the Vanilla-board path call it from `OnCutsceneEnded` and were
always fine. The 0.13.0 pity smoke passed because the pity offer path defers a tick already.

Fix: `DeferShrineThenContinue` queues the open, `TickShrineWatchdog` drains it once no menu is up (same
pattern as `_holdReaskPending` / `_pityReaskHeld`), and the fall-through now logs a Warn with the
blocking menu name. Build clean, 830 tests pass. **Still owed: live smoke on the Rodger save** (fail a
season, answer keep and reshuffle, confirm the shrine opens both times and JP can be spent), then a
release + replies to SincerelyZoey and SilencedLink (and a status flip on 1123181).


**Two reporters, reproduced on a clean save and a clean reinstall. This kills meta-progression.**

- **SincerelyZoey** (premium, 25 Aug 8:22AM, 0.12.18): "On reset, the JP perk buying page no longer
  shows up. Instead, you get the option to keep or remix bundles, then you can choose the weekly focus
  and then the game goes straight into Day 1." Follow-up 8:24AM: most of that save was on an older
  version, so she started a **new save - the JP perk screen still doesn't trigger**.
- **SilencedLink** (member, 25 Aug 6:57PM): same bug after deleting the mod folder and redownloading;
  clicked exactly once; "After I chose reshuffle the game froze and then just went on to Spring 1."
- Reported against **0.12.18**; nobody has retested on 0.13.0 yet. Status still **New issue**, unanswered.

Prime suspect: the same teardown that bit the season-pity offer - a question opened from inside another
question's answer callback gets torn down by `answerDialogue` (fixed there by deferring one tick via the
watchdog drain). The reset chain is keep/reshuffle -> perk buy -> weekly focus, so the perk question is
exactly that nested case. 0.13.0 inserted the pity offer into the same chain, which could make it worse
or accidentally mask it. Reproduce on the Rodger save first, then check the reset question chain.

### NEW (2026-08-26 sweep) - a streamer picked the mod up: emmalution (82.7K subs)

Found via the r/StardewValley thread (Thrippalan, 26 Aug). **emmalution** is running TLY as a full
challenge series, credited and linked to the Nexus page in every description ("The main mod is called
The Longest Year... currently in beta and you MUST use the Standard Farm"). She got the suggestion from
**Tired Ginger Bri** in her Discord. She was already #1 on `marketing/youtuber-outreach.md` (suggested
by u/Khajiit-ify back in June) - she found it on her own.

| Video | Date | Views |
|---|---|---|
| Time-Loop Roguelite (Spring), edited | 16 Jul 2026 | **53.7K** (2.6K likes, 87 comments) |
| Time-Loop Roguelite (Summer), edited | ~12 Aug 2026 | 17K (1K likes, 56 comments) |
| LIVE 01 | ~1 month ago | 8.4K |
| LIVE 02 | ~1 month ago | 6.7K |
| LIVE 05 ("I'm scared to check the Summer deadlines...") | ~18 Aug 2026 | 3.4K |

(LIVE 03/04 exist but YouTube's lazy list wouldn't page far enough to confirm counts.)

**Bug/design signal harvested from her comment sections** (none of this is on Nexus):

- **Weekly theme completion is credited by bundle, not by hand-in** - @ggrace67 (Summer, 15:38):
  "if you complete a bundle it counts all items in it as used for the weekly theme even if you didn't
  donate them so it still completes and lifts the drawback." That's a free drawback-clear exploit.
- **Demetrius' cave cutscene doesn't re-trigger after a reset** - @nancyjohnson7147 (5 likes): you have
  to walk over the cave, then a "this cave seems familiar to you" popup asks mushrooms or bats. Might be
  the intended fallback, but nobody knows that; either fix the cutscene or say so in the notes.
- **A hat permanently eats a Junimo Stash slot** - @whisperinwind87: "not one of my slots of my stash
  will be forever taken by a certain hat I got outside the pub." Non-donatable item stuck in the stash.
- **Difficulty setting wanted** - @maglomanic-mama: "A difficulty setting would be nice, like you
  mentioned. Having to restart more would make it more fun." emmalution raised it on stream too.
- **Perfection-goals variant** - @fernandothehorse: extend the deadline pressure past the CC to
  Perfection goals (8 hearts by Summer 1, 10 recipes crafted, etc).
- **Red cabbage RNG still hurts** - @localinternetclown: got stuck grinding the Skull Cavern for a seed.
  Third independent report of this (u/Lagao, Thrippalan, now this).
- The **one-item cart reads as intended design** to viewers: @pokadotplot, "Nuking the traveling cart is
  an excellent difficulty adjustment" (8 likes).
- No other bug reports across ~140 comments; sentiment is uniformly positive.

Jeff commented on the Spring video as @sonofskywalker3 asking for feedback (26 Aug).

### 9th sweep (2026-08-26 18:02, `forum-sweeps/2026-08-26-18-02_*`) - everything else is quiet

- **Nexus TLY**: 104 posts, 3 open bugs. Newest post is still rose1729 (25 Aug 12:41, pet offer, below).
  Nothing new today. Page stats: 916 unique DLs / 1,220 total / 9,502 views / 17 endorsements on 0.13.0.
- **Nexus bugs**: 1123181 (above, NEW), 1122901 Keep pet (open on purpose, awaiting a multi-pet
  confirmation on 0.13.0), 1122358 Fixed, 1113831 Day-3 crash still silent since 21 Aug.
- **Reddit**: r/StardewValley 64 comments - one new exchange, Thrippalan (26 Aug) explaining her husband
  got inspired by emmalution's videos and was confused by the one-item cart; Jeff already replied.
  r/StardewValleyMods (33) and r/SMAPI (1) unchanged since 13 Jul.
- **forums.stardewvalley.net** thread 52534: still zero replies from anyone else. playstarbound: still
  never posted (account activation).
- **Android Consolizer**: one unanswered feature request - Estallking (22 Aug): hold LT/RT to scroll the
  toolbar instead of tapping per slot. **Nap Time / Cart Catalog**: quiet.


### NEW (2026-08-25 12:41, Nexus post, rose1729): no pet offer after declining Keep Pet

"I didn't keep my pet at the end of my first loop. I thought this would mean I get offered a new pet
with 0 hearts in future loops, but I haven't been offered the pet at all in my 2nd/3rd loops. Is this
intended behavior?" Not answered yet. Suspect: the vanilla adoption offer is gated on a flag the reset
does not clear (see `MarniePetAdoption` in PetCarryoverService / WorldResetService) or on
`Game1.player.whichPetType`/day counters the loop rewinds past. Decide: should a fresh loop re-offer a
pet (0 hearts) when keep_pet is not owned? Jeff's earlier ruling for animals was "start over with 0
hearts", so probably yes.

### 8th sweep (2026-08-25 10:38, `forum-sweeps/2026-08-25-10-38_*`): ALL THREE RELEASED in v0.13.0 (2026-08-25), smoke PASSED

**Smoke 2026-08-25 (deployed 0.13.0, Rodger throwaway save, `test-output/cart-*.png`):**

| Check | Result | Evidence |
|---|---|---|
| Board has no quality asks on Fiber / jellies / Tea Leaves | PASS | `tly_genbundles` "quality asks:" lines: only `The Missing` (vanilla slots, Abandoned Joja Mart: 348 q1, 454 q2, 795 q2) plus the Vault money bundles printed as `-1 qNNNN` (diagnostic quirk, cosmetic) |
| Cart: buy the only item, reopen | PASS | `debug shop Traveler`: Pumpkin bought, reopen shows "Out of stock" instead of the next item (cart-06-reopen.png) |
| Keep every pet | path ran, not proven | save has no pets: `PetCarryover: no pets on the farm; snapshot cleared`; the two-pet case is unit-tested only |

**Opt-in pity offer smoke 2026-08-25 (`test-output/offer-*.png`):** plain hold prompt (offer-02), then
"It looks like Spring has been giving you a hard time... We would ask a little less of Spring next time."
with "Yes please (free)" / "No thank you, I will manage" (offer-06). Accept: `Pity offer: ACCEPTED (Ease,
cost 0 JP, consecutive uses now 1, ease 0/2)`, reset `pity ease Spring 2 steps`, next offer 50 JP.
Second Fail night, Reshuffle: trim wording + "Yes please (50 JP)" (offer-08); declined: `uses reset, no
easing stamped`, reset `pity trim none, pity ease none`. First attempt failed: the offer was created inside
the hold answer's callback and the game tore it down (watchdog "replaced before it closed"); fixed by
deferring the offer one tick via the watchdog drain (same trick as the NotEnoughJp re-ask).

Jeff (watching the smoke): the merchant's "The Junimos might know a way around that." breaks the lore
(only the farmer and the Wizard see Junimos); removed from `dialog.cart.first-visit`. The cart intro
popping up in the farmhouse was the `debug shop Traveler` test, not a player path. Follow-up nit:
`tly_genbundles` quality-asks line should skip Vault slots (id -1, the "quality" is the gold amount).

Plan `docs/superpowers/plans/2026-08-25-0-13-0-fixes.md`. Final-review finding worth remembering: Fiber
enters the pools as a CROP (Fiber Seeds 885 -> 771), and vanilla marks it base-only with
`CropData.HarvestMaxQuality == 0`; the eligibility rule now honours that field. Parked: crops with
`HarvestMaxQuality == 1` (none in vanilla) could still draw a gold ask; curated forage additions never
carry quality; the Cart Whisperer preview stamps the day's cart selection early (benign). Replies drafted
in `release-notes/2026-08-25-replies-draft.md`, NOT posted.

Nexus bugs (read in-browser; the logged-out sweep shows 0 bugs, known):
- **1122358 (Fixed) got two new replies after the Void Salmon follow-up:** ChaoticMindset (24 Aug 20:46,
  0.12.16): asked for GOLD-star Fiber and GOLD River Jelly, "neither can be obtained without mods";
  gazumbrado (24 Aug 23:39): SILVER Tea Leaves. Decompile check: `Bush.GetShakeOffItem` items are
  created via `ItemRegistry.Create` at base quality, so Tea Leaves (and salmonberry/blackberry from
  bushes) never carry quality; Fiber and jellies likewise per the reporters. Root cause: quality asks
  are rolled per DOMAIN (SeasonalForage/Fish/crops) with only a hand list of exceptions
  (`BundleSlotFiller.BuiltInQualityIneligibleItemIds` = algae). Needs a structural rule: quality only
  for items that actually receive quality in vanilla (crop harvests, rod-caught real fish, true
  spawned forage), never for curated additions (`SeasonalForageAdditions` = Tea Leaves), bush drops,
  Fiber, jellies. Reply not yet posted.
- **1122901 NEW (Bumblewyn, 24 Aug 16:35, 0.12.0-beta.1): "Keep pet" only keeps one pet.** Confirmed
  in code: `PetCarryoverService.SnapshotPet` takes `pets[0]` (`MetaState.PetState` is a single
  `PetSnapshot`). Fix = snapshot/restore a list. Reply not yet posted.
- 1113831 Day-3 crash still silent (Needs more info since 21 Aug).

Nexus posts (102 total; the sweep's 28-vs-31 page-1 count is pagination, not deletions):
- **lexihope (25 Aug 01:18): does the Traveling Cart restock further down its list after you buy the
  initial items?** Observed after buying Cart Stall II. Needs a code check of `CartSlotLimitPatch`
  (is the cap on the number of visible stalls, or on purchases?) and a reply either way.

Reddit: 63 comments, newest 21 Aug, no other threads. AC / Nap Time / Cart Catalog: quiet.

### Season pity: MERGED to master as v0.12.19 (2026-08-25), unreleased, live smoke PASSED

**Smoke 2026-08-25 (deployed 0.12.19, Rodger throwaway save, screenshots `test-output/pity-*.png`):**

| Check | Result | Evidence |
|---|---|---|
| `tly_pity set spring 7` gives 2 ease steps | PASS | status: `steps Spring 2`, `ease stamp -1`, `board trim -1` |
| Fail night prompt shows the eased text | PASS | pity-04-prompt.png: "Keep them and we will ask a little less of Spring. Let time reshuffle them and we will leave out the hardest of Spring's asks." |
| Keep stamps the ease; reset applies it | PASS | `Reset: ... consecutive holds 1, pity trim none, pity ease Spring 2 steps`; status `quota ease Spring 2 steps factor 0.80; ease stamp season 0 steps 2` |
| Reload after keep | PASS | relaunch + `tly_loadsave`: `Requirements source: engine manifest (loop 37, seed loop 36)`, no mismatch WARN, stamp intact |
| `tly_genbundles` determinism with the stamp | PASS | "determinism OK (second generation matched the first byte-for-byte)" |
| Second Fail night: hold priced 50 JP, eased text again | PASS | pity-09-prompt2.png |
| Reshuffle trims and clears the ease | PASS | `BundleEngine: pity trim 'Blacksmith's': 11 candidates -> 7 (units 4, quality off False, need 3)`, fish/Quality Crops 3 items + quality off; `Reset: ... pity trim Spring x4, pity ease none`; status `board trim season 0 units 4`, ease stamp cleared |
| Reload after reshuffle | PASS | same trim log lines on the load-time regeneration, `engine manifest (loop 38, seed loop 38)`, no mismatch |
| "eased Nx" title in the Season Goals menu | not eyeballed | the Bundle Log book was in the inventory, not placed; covered by review + I18nGuardTests |
| Real day-28 `RecordFail` / `RecordPass` path | not exercised live | `tly_failreset` queues the cutscene directly and skips `OnDayEnding`; both are unit-tested (SeasonPityTests) |

Note: with `tly_failreset` the counter stays at the value set by `tly_pity set` (no `RecordFail`), which is why the reshuffle trimmed 4 units, not the plan's 6.

Spec `docs/superpowers/specs/2026-08-25-season-pity-design.md`, plan
`docs/superpowers/plans/2026-08-25-season-pity.md` (Task 10 step 4 is the smoke script). 794 tests.
Open follow-ups parked by the final review (cosmetic): trim log prints raw units and fires before the
can't-fill bail; `SeasonPity` class doc lists the old mutator set; `BundleEngine.TrimFor` hand-rolls
the season bound check; turning `PityEnabled` off mid-loop drops the quota ease on the next reload
while the trimmed board stays (config-driven, accepted).

### (superseded) 0.13.x brainstorm: DerivePins / obtainability

Keep-bundles hold RELEASED in 0.12.17/0.12.18 (spec
`docs/superpowers/specs/2026-08-24-keep-bundles-hold-design.md`). 0.12.18 also pulled Void Salmon
(WitchSwamp is post-CC Dark Talisman content; the 0.12.16 "hard but fair" ruling was reversed and
an apology posted on bug 1122358).

**Smoke 2026-08-24 (branch build, Rodger throwaway save, screenshots `test-output/hold-*.png`):**

| Check | Result | Evidence |
|---|---|---|
| Free first hold, reset keeps the board | PASS | `tly_hold keep: Kept. JP 403` unchanged; `Reset: bundle seed loop 30 (CompletedResets 31, consecutive holds 1)`, seed 743281092 |
| Paid hold (50 JP), same board again | PASS | JP 403 to 353; second reset seed 743281092 again, holds 2 |
| Reload from title after a hold | PASS | `Requirements source: engine manifest (loop 32, seed loop 30, 26 bundles)`, no mismatch WARN |
| Reshuffle resets counter and rerolls | PASS | seed -1007977301 at seed loop 33, holds 0, next hold free |
| Real Fail night: cutscene, prompt, shrine, reset | PASS | prompt shows "Keep these bundles (50 JP)" / "Let time reshuffle them" before the shrine (hold-11-prompt.png); reset to loop 34 |
| Keep with too little JP re-asks | PASS after fix 6bf175c | HUD "The Junimos need 50 JP" + prompt re-rendered twice (hold-21/22); first build lost the re-ask callback (SDV nulls afterQuestion after invoking it), fixed by deferring one tick |
| Held Nx title, junimo-9b intro line | not eyeballed | covered by review + I18nGuardTests; check on the next new-farm playtest |


Parked note (moved from the RELEASED v0.12.11 block below): `DerivePins` for artisan goods /
dishes / geode tiers so the clamp catches those structurally.

Ideas to discuss before building it:
- Escalating per-season likelihood instead of hard pins, so an item gets more likely to appear
  as the loop runs longer rather than being forced in outright.
- A pity counter that eases the board after N consecutive fails. User's note (2026-08-24): a
  player can loop 40 times without ever reaching Fall and start run 41 with a huge stash, a
  barn, coop, silo, and every crafting station already built, so full pins on run 41 would be
  wrong; the board needs to ease off gradually, not snap to guaranteed asks.
- Wording: the weekly-theme card and journal text should say plainly that weekly goals point at
  season bundle slots, not at a one-week target. lexihope read "68 daffodils" as something to
  gather in a single week when it was actually the season-slot ask.

### 🔧 FIXED ON MASTER (unreleased) — 7th sweep (2026-08-24): all 4 bugs root-caused + fixed same day
*Sweep `AndroidConsolizer/release-notes/forum-sweeps/2026-08-24-15-09_*`. Sweep script is now
profile-free (public pages only); bug bodies + private bugs read via Claude-in-Chrome on the
regular browser. Reddit unchanged (63). Nap Time / Cart Catalog quiet.*

**Post-release sweep 2026-08-24 18:30** (`forum-sweeps/2026-08-24-18-30_*`): NO new activity anywhere.
Nexus posts/bugs identical for all four mods (only diff = our four 0.12.16 replies + Fixed flips, no
reactions yet); Reddit verified in-browser at 63 comments, newest 21 Aug (the sweep script's Reddit
fetch returned 0 comments this run without erroring — a silent-empty case worth guarding in
`sweep-forums.mjs`). Private bugs unchanged: 1117543 muting = 2 replies, last 21 Aug 14:37 (Needs
more info); 1113831 Day-3 crash = 1 reply (ours), last 21 Aug 10:49.
**1117543 CLOSED 2026-08-24 14:14 as Not a bug** (Jeff: "yes") — reply posted via Claude-in-Chrome
(ALSOFT 0x88890004 = Windows audio-device loss; reopen if a full log shows otherwise). Gotcha: a reply
form left open for a long time POSTs 401 ("Something went wrong saving your reply") — reload the page
and re-expand before submitting. Only 1113831 remains open (Needs more info, silent since 5 Aug).

- **1122358 — Engine bundles roll CC-gated / impossible items** (SincerelyZoey + IshoMoogoo,
  23 Aug, 0.12.11): pineapple, Qi Fruit, taro root, void salmon, silver/gold-quality algae
  (quality algae may not exist unmodded); "my spring crops bundle is asking for q fruit."
  Echoed on the posts tab by gazumbrado (slime jack, Qi Fruit, pineapples, taro root on a
  fresh save). → bundle-pool item filter needed (exclude Ginger Island / Qi / CC-gated items,
  clamp qualities to obtainable).
- **1122423 — Weekly theme asks for out-of-season / unreachable items** (spenderg, 23 Aug,
  0.12.11; + lexihope): Pike in a Spring theme right after reset; lexihope: "10 corn or 68
  daffodils" — goals that were "basically never going to happen." Likely same root as 1122358
  (slot sampler inherits the bad bundle slots) — verify overlap before treating separately.
- **1122619 — Advanced Options: picking Remixed soft-locks the OK button** (SincerelyZoey,
  24 Aug, 0.12.11): with TLY installed, selecting the remix bundle option in new-character
  Advanced Options makes OK unresponsive; must switch back to TLY Custom or Normal to exit.
  Unexpected — `BundleOptionPatch` (v0.12.1) was supposed to show ONLY "TLY Custom"; find out
  how Remixed is still selectable (arrow cycling? non-dropdown path?) and why OK dead-ends.
- **1122027 — Shrine upgrade purchase buys every affordable tier in one click** (spenderg,
  22 Aug, reported vs 0.11.60): bought Mine Upgrade 1, was charged ~200 JP and received tiers
  1+2. Check if the chained-upgrade purchase loop still exists in 0.12.11 before answering.

**Fixes (commits 8c04dbb → effe3c2, v0.12.12–0.12.15, 723 tests pass, replies NOT yet posted):**
- **1122358 → v0.12.12** — island/Qi items could never be caught by location markers (crops have no
  location; category pools scan all of Data/Objects). Default `ExcludedItemIds` now vets Qi Fruit,
  Pineapple, Taro Root/Tuber, Banana, Mango, Ginger, Magma Cap, Radioactive Ore/Bar, Cinder Shard,
  Dragon Tooth, Fossilized Skull, the 5 island dishes + Piña Colada (ids verified against the game's
  own Data/Objects via the Android content dump). `BugLand` joins the location markers (Slimejack 796 —
  Dark Talisman is post-CC); WitchSwamp stays (Void Salmon = hard-but-fair, user ruling 2026-08-24).
  New `QualityIneligibleItemIds` (Seaweed/algae) stops silver/gold asks on quality-incapable items.
  ⚠ Existing saves keep their current board until the next reset regenerates bundles.
- **1122423 → v0.12.13** — SeasonResolver treated all fish as year-round (only crop/forage maps), so
  the weekly obtainability filter passed Pike into Spring. New Core `SpawnSeasonMap` feeds the
  resolver real fish/crab-pot spawn seasons from the engine pools. (lexihope's "68 daffodils" is the
  intended LargeQuantityForage roll, 40–99 — flag if it should be tuned down.)
- **1122619 → v0.12.14** — the AGO patch found vanilla's dropdown callback by counting non-label
  options, but AGO headers use the Default style → off by one: we replaced the Year1Completable
  checkbox's callback, vanilla's 2-entry capture stayed live, Remixed (index 2) threw on OK.
  Now located via closure inspection (`DelegateClosures.References`); also un-breaks the silently
  eaten Year1Completable setting.
- **1122027 → v0.12.15** — one gamepad A press dispatches receiveGamePadButton AND a synthesized
  receiveLeftClick in the same tick; after the first buy the next tier slid into the same row slot
  and the second dispatch bought it. Same-tick guard in TryBuy.

**⚠ v0.12.16 added same-day:** the live-install playtest caught that SMAPI's ReadConfig REPLACES
serialized list defaults — this machine's config.json had `ExcludedItemIds: []`, so config-default-only
excludes were inert on every existing install. All structural exclusions moved into code
(`ItemPoolBuilder.BuiltInExcludedItemIds` / `BuiltInExcludedLocationMarkers`, BundleSlotFiller's
built-in quality-ineligible set); tuning lists are pure extension points again; regression test empties
every tuning list and asserts the vetting holds. 724 tests pass.

**✅ PLAYTESTED 2026-08-24 (agent-driven, deployed 0.12.16, screenshots `test-output/pt-*.png`):**
- **AGO/1122619:** new-character → wrench → CCB dropdown shows TLY Custom/Normal/Remixed (default TLY
  Custom) → picked **Remixed** → **OK closed the screen** (old build soft-locked); log:
  `CC-bundles choice = VanillaRemixed (Game1.bundleType=Remixed)` — our replacement callback fired.
- **Pools/1122358:** pool counts dropped exactly on target after 0.12.16 (crops 49→46, fish 54→53
  [Slimejack], metals 14→11 [Radioactive×2+Cinder], cooking 84→78 [5 dishes+Piña Colada],
  geode-minerals 77→73, saplings 7→6). `tly_reset` on a throwaway clone (Reset #32) wrote 31 bundles;
  save-file parse: **0 gated-item violations, 0 algae quality asks** across all 31.
- **Weekly/1122423:** week-1 Fishing goals = Cockle/Snail/Mussel/Clam; Foraging goals =
  Hardwood/Common Mushroom/Mussel/Maple Seed — all Spring-obtainable, no out-of-season asks.
- **Shrine/1122027:** Shop Discount II clicked once with 903 JP (tiers II+III affordable) → exactly ONE
  purchase (175 JP, 728 left), no tier-chaining. (Gamepad double-dispatch itself can't be simulated
  without hardware; the same-tick guard covers it by construction.)
- Throwaway clone save deleted after (None_447267231); original `None_443632257` untouched.
- *Note (debug-only, pre-existing):* console `tly_select` while the week-1 hub is deferred still
  re-presents the hub afterward — the known quirk, not a regression.

**RELEASE 0.12.16 (2026-08-24, user-approved "do it") — mostly done:**
- ✅ master pushed; Nexus description + mod version synced to 0.12.16 via Claude-in-Chrome ("Mod saved
  successfully", verified on the public page); backup of the 0.12.11 description at
  `release-notes/nexus-description-0.12.11-backup.bbcode`.
- ✅ All 4 bug threads replied + status set to **Fixed** (1122358, 1122423, 1122619 via the wysibb reply
  form with status select; 1122027 via a plain-textarea reply + the Manage → Change status dialog —
  its reply-form status select did not submit).
- ✅ GitHub release v0.12.16 created (after Jeff added the permission rule via /permissions);
  publish-nexus.yml run 32761528028 succeeded; file live on Nexus 24 Aug 2:17PM (288KB).
- ✅ Nexus changelog for 0.12.16 pasted via the new editor (⋮ → Documents → Add changelog,
  file/version auto-matched, form_input on the dialog textarea) — "Successfully added changelog entry."
**RELEASE 0.12.16 FULLY CLOSED 2026-08-24.** Still open: the two PRIVATE bugs (1117543 muting —
reporter has nothing to send, candidate not-a-bug; 1113831 Day-3 crash — silent) and the parked
0.13.0 follow-ups.

**Comments:** gazumbrado — "I love the new update. Thanks for fixing all the bugs." + the gated-items
note above; SilencedLink — thanks (closed). **Private bugs:** 1117543 muting — IshoMoogoo replied
21 Aug: hasn't recurred, no log to send, will follow up if it returns (candidate: not-a-bug per the
ALSOFT device-loss diagnosis, or leave at Needs more info); 1113831 Day-3 crash — still silent.

### ✅ RELEASED v0.12.11 (2026-08-21 19:39) — GitHub + Nexus file/description/version/changelog/gallery all live
*Released via `release.ps1 -SkipNexusDesc` + Claude-in-Chrome (the new Nexus editor: Media → file input for the gallery; General → SCEditor `.val()` + a keystroke + Save; ⋮ → Documents → Add changelog, file/version auto-matched; only the CURRENT file is selectable, so the beta.1 changelog can't be backfilled). Still owed: replies on bug 1108030 (fixed at the root) and the ada113/ErraticPixel CCCB compat ask. All pre-0.12 handoff items shipped + smoked (see STATUS). Rulings taken: A4 all twelve ramps + trophy trim;
B6 starfruit removed / red cabbage 5k / Pierre's Special Order 10k (bus, rare-fish, Cart Stall untouched);
C7 TLY Custom stays the default dropdown entry, config flips apply at the next reset. Release docs written.
Release mechanics: `release.ps1 -SkipNexusDesc` → description/version sync + changelog paste via
Claude-in-Chrome → upload `release-notes/advanced-options-tly-custom.png` to the Nexus gallery and swap the
`NEXUS_IMAGE_URL_advanced-options-tly-custom` placeholder in the live description (and
`docs/nexus-description.bbcode`) → verify live → reply on bug 1108030 (root cause fixed: the game never
persisted the Remixed choice; the mod does now) and the ada113/ErraticPixel CCCB compat ask.*

*Follow-ups parked (0.13.0): category-ref ingredients stay unsupported (documented); `The Missing` never gates;
curated Chef's never fires in the engine era (RandomBundles Chef's is 6/6); `pull-logs.ps1` prunes three
TRACKED log archives on every deploy — `git checkout -- test-output/log-archive/` after deploying.*

### ✅ RELEASED 0.12.0-beta.1 (2026-08-21) — 6th sweep: 9 new bugs root-caused + fixed, all threads answered + marked Fixed
*Release closed 2026-08-21: GitHub release v0.12.0-beta.1 + Nexus file (workflow 32484136636) + description/version
synced via Claude-in-Chrome (the Playwright automation profile's Nexus session expired — `nexus-wait-login.mjs`
added but the regular browser was used instead). Replies posted on all 9 bug threads (status → Fixed), posts-tab
replies to faldans / CausticOptimist / SilencedLink / Bumblewyn, Reddit reply to Thrippalan. Nexus flood control
silently drops a bug-reply submit fired within ~30 s of the previous one — click again after a screenshot.
**Two PRIVATE bug reports the sweep can't see (logged-in only):** 1117543 "Game randomly muting" (IshoMoogoo,
13 Aug — random audio mute, 5 mods, log excerpt; likely a vanilla/Windows audio-device issue, needs a full log) and
1113831 "Day 3 1st year crash" (5 Aug — hard crash a few seconds after accepting Emily's Wild Horseradish help-wanted
post, no error log). **Both replied 2026-08-21 + set "Needs more info"** (asked for smapi.io log links; crash report also asked to retest on 0.12.0-beta.1 + offer a save). The mute log excerpt ends in `[ALSOFT] (EE) Failed to get padding: 0x88890004` = OpenAL lost the audio device (AUDCLNT_E_DEVICE_INVALIDATED) — Windows audio, not TLY; mark not-a-bug once the full log confirms.*

### ✅ SMOKED 2026-08-21 — v0.12.1 on master (unreleased): TLY Custom dropdown eyeballed + full loop-reset smoke PASSED
*v0.12.1 = new-game Advanced Options "Community Center Bundles" dropdown shows a single **TLY Custom** entry (+tooltip) while the mod is enabled (`BundleOptionPatch`, user ruling 2026-08-21: replace rather than remove). **Eyeballed 2026-08-21 on the deployed 0.12.1 build** (the Mods folder had still held the beta.1 release DLL — redeployed via `tools/deploy.ps1` first): the row shows only "TLY Custom", the tooltip renders on both the label and the box, the other AGO rows are untouched, and a brand-new farm starts normally (Lewis intro, `BundleEngine: wrote 31 bundles`, stash + shrine placed, `TLY_Intro` letter). No change to `BundleOptionPatch` needed.*

**Feedback sweep 2026-08-21 16:45** (`forum-sweeps/2026-08-21-16-45_*`): nothing new since the 12:00 sweep except our own replies — TLY posts 26→30 (all four = sonofskywalker3 replies), Reddit 61→63 (our Cart-Stall answer + Thrippalan's thanks: "figured out it was intentional… Great mod", now in Fall), AC/Nap Time/Cart Catalog unchanged. Both PRIVATE bugs still at 1 reply (ours), status **Needs more info**, no log/save delivered: 1117543 muting (IshoMoogoo) — leave until a full log confirms the `[ALSOFT] 0x88890004` device loss; 1113831 Day-3 crash (iWriteSins, full text: fishing at the town lake Spring 3 after passing out the night before; "doesn't crash until I take [Emily's Wild Horseradish help-wanted] request. It crashes a few seconds after, completely closing SMAPI / no crash report") — investigate `RatProblemQuestPatch` / `OnboardingMailService` / weekly-theme quest service only once a log or save arrives.

**Loop-reset smoke (2026-08-21, agent-driven on a clone of `None_443632257`, Run 31 → 32; console injection + screenshots; post-reset state verified in the written save file; game reloaded from title once):**

| Check | Result | Evidence |
|---|---|---|
| Junimo Stash deposit on day 28 survives | ✅ | Prismatic Shard put in the stash on Spring 28 → `restored 4/4 items` after reset; save chest = Copper Bar×5, Coal×87, Smoked Legend, Prismatic Shard |
| Kept coop comes back WITH hay hopper | ✅ | `Reset: kept building 'Coop' placed at (60,20)`; post-reset save Coop interior objects = `[99]` (Hay Hopper) |
| Kept rod keeps bait | ✅ | Fiberglass Rod + 25 Bait attached pre-reset → `Reset: transplanted tool state — fishing_rod:attachment[0]=Bait`; save shows Bait×25 on the rod |
| Re-donate an artifact → museum reward re-granted | ✅ | Run 31: donated Ancient Seed (114), collected Ancient Seeds pack + recipe. Reset: `cleared 1 museum donation(s)`, `specialItems`/`specialBigCraftables` empty, no `museumCollectedReward*`/`artifactFound` mail. Run 32: re-donated → "Collect Rewards" offered again, both rewards collected |
| Caroline's 2-heart tea event (719926) replays | ✅ (state) | Played via `debug ebi 719926` pre-reset (grants `mail CarolineTea`). Post-reset save: 719926 NOT in `eventsSeen`, `CarolineTea` NOT in `mailReceived`; `tly_dumpreplayable` flags it `grant='mail' excluded=False` and `FarmerReset` skips replayable ids when re-marking → eligible to re-fire naturally |
| Rain Totem → rain next day | ✅ | Totem used Spring 1 ("Clouds gather…"), schedule said Sun for day 2 → Spring 2 was Rain (HUD icon; save `Default` context `isRaining=true`, `Weather=Rain`); day 3 back on schedule (Sun) |
| A season has >2 wet days | ✅ | `WeatherScheduler.BuildSchedule(447004781, s)` computed offline for the post-reset seed: Spring 6 wet (R 4/7/9/14/16/21), Summer 9 (storms+rain+green rain), Fall 5, Winter 14 snow |
| Mixed Seeds in Summer roll Red Cabbage/Starfruit (cult upgrades owned) | ✅ | `debug season summer`, 29 Mixed Seeds planted on the farm, `debug growcrops 30` → ≥2 Red Cabbage + ≥2 Starfruit visible among wheat/hot pepper (10% each per seed) |
| CC win → Town ceremony plays | ✅ | `debug completecc` (TLY paid 6×63 JP room bonuses) → entering Town fired event 191393 (balloons, Lewis + crowd at the CC); `EventSuppressionPatch` only blocks 611439 |
| FAIL night with an overnight farm event still rewinds | ✅ | `debug setfarmevent owl` + sleep on day 28 (gate unmet): vanilla applies the override only when `pickFarmEvent()` returns null, so `SoundInTheNightEvent` played; driver logged `deferring the Fail scene until the overnight FarmEvent … finishes` → Fail scene opened → shrine → `In-place reset: complete. Spring 1 … Reset #31` → Run 32. (The postfix's own "suppressed tonight's overnight FarmEvent" branch wasn't hit — no natural event rolled — but the deferral fallback it guards was.) |
| Reload after reset | ✅ | `Requirements source: engine manifest (loop 31, 26 bundles)`, no mismatch WARN |
| Zero errors | ✅ | 0 game/TLY ERROR lines across both sessions (only ERROR = an invalid `debug action` I typed) |

*Notes, not bugs: (1) running `tly_select` from the console while the week-1 hub is still open and then clicking a card logs `WARN Farming is not offered this week` and re-rolls the offer — debug-only sequence, real players pick from the hub. (2) The in-place reset rotates the save folder (`None_<newUniqueId>`) and deletes the pre-reset folder, as designed; a clone must keep the `<Name>_<id>` shape. (3) Gunther's "Action" tile is the counter front, not his own tile — cost an hour of automation, not a mod issue.*

#### Original sweep table (2026-08-21)
*Sweep `AndroidConsolizer/release-notes/forum-sweeps/2026-08-21-12-00_*` + `tly-bug-bodies.json`.
Every report is against public 0.11.60. Nothing new on Nap Time / Cart Catalog; AC got two feature
asks (furniture-catalogue categories — Junimo3738299202; make LB row-switch rebindable for Grandpa's
Toolbelt — denipadliilaziim). Fixes are LOCAL commits on master, not released. Decision still open:
ship master as **0.12.0-beta.1** (recommended — already 55 commits past 0.11.60, reviewed, and the
engine makes #1 moot) vs. backport onto 0.11.60.*

| # | Bug (Nexus id / reporters) | Root cause | Fix |
|---|---|---|---|
| 1 | **Remixed bundles come back vanilla after reset** (1108030; theunderscore76, Alexrandia314, RosieMermaid7, jadster0, nothumanafterall, Bumblewyn + posts) | `Game1.bundleType` is a non-persisted static; 0.11.60's `loadForNewGame` → `GenerateBundles(Default)` always wrote the STANDARD board. | **Moot on master** — the bundle engine (v0.11.75+) overwrites the board every reset. ⚠ Open design point: the engine ignores the Standard/Remixed choice entirely, so Standard players silently get the authored hybrid set. Consider a `BundleSource: Engine|Vanilla` config. |
| 2 | **CC ceremony never plays; Joja open, Pierre closed Wed, no lightning** (1113630; GoddessSword, gazumbrado; newmaaly post) | `EventSuppressionPatch` suppressed **191393** believing it was the CC intro — it's the COMPLETION ceremony. Intro is 611439. | ✅ v0.11.102 — id swapped. Affected saves self-heal (ceremony fires next sunny Town entry). |
| 3 | **Museum rewards one-shot across loops** (1107194; RoseLightning05, RayAndRain, Sihara, Thrippa, IshoMoogoo) | 1.6 gates `RewardItemIsSpecial` rewards on `Farmer.specialItems`/`specialBigCraftables`, never cleared by the reset. Count-milestone seeds are mail-gated → they DID return. | ✅ v0.11.103 — both lists cleared in `FarmerReset`. Takes effect from the next reset. |
| 4 | **Caroline Tea Sapling event never replays** (1115192; RiseiJaku) | Event 719926 grants via `mail CarolineTea`; `mail` wasn't in `GrantCommandTokens`, so the replayable scan kept it in `SeenEventsEver`. | ✅ v0.11.104 — `mail`/`mailToday`/`hostMail` added (+test). Next reset. |
| 5 | **Mixed Seeds never give Red Cabbage/Starfruit; Summer Seeds DO** (1109718; painspinner, GatewayMidnight, Sihara, IshoMoogoo) | Patch targeted `Crop.getRandomWildCropForSeason(bool)` — the WILD-seeds path. Mixed Seeds go through `Crop.ResolveSeedId("770")`. | ✅ v0.11.105 — retargeted to `ResolveSeedId`, emits seed ids 485/486. |
| 6 | **Rain never occurs / totems do nothing / CJB "forces sun"** (1107279 Holdeborg; 1116791 gazumbrado) | `WeatherModificationsPatch` returned the schedule unconditionally every morning (clobbering totem/CJB/console), and the scheduler filled every unplaced day with Sun → exactly 2 wet days/season. | ✅ v0.11.109 — schedule written for TOMORROW in an `UpdateWeatherForNewDay` postfix (player overrides later in the day win); modifications postfix only neutralises the DaysPlayed≤4/==3/Summer%13 rules; minimums (2 rain / 2 storm+2 rain / 2 rain / 2 snow) guaranteed, every other day ROLLED per loop at vanilla-like odds (user ruling 2026-08-21: random per loop with a floor, not fixed counts). |
| 7 | **Junimo Stash loses day-28 deposits** (1111046; gazumbrado, gemscout, jadster0) | Stash only banked on `Saving`; reset restored from the snapshot without re-reading the live chest. | ✅ v0.11.106 — `BankToMeta()` immediately before `loadForNewGame`. |
| 8 | **Kept coop/barn has no hay hopper** (1110130; illuvitas, shadetheghost) | `Building.load()` → `InitializeIndoor(forConstruction:false)` skips `IndoorItems`. Upgrading re-ran it with `forUpgrade:true` — hence the workaround. | ✅ v0.11.107 — `Building.CreateInstanceFromId` + `InitializeIndoor(forConstruction:true)`. Existing hopper-less coops need a reset (or the upgrade workaround). |
| 9 | **Kept rod loses bait** (CausticOptimist post) | Keep-tool re-creates a blank registry instance; attachments/enchantments/water never copied. | ✅ v0.11.108 — `TransplantToolState` for kept tiers. |
| 10 | **Day-28 fail scene flashes → Summer 1, reset lost** (faldans post, reproducible w/ owl event) | Vanilla nulls `farmEvent` 1–2 ticks before the post-event warp runs `showEndOfNightStuff` (→ `SaveGameMenu` replaces our menu, no exitFunction). Owl's `pauseThenMessage` opens the window deterministically. | ✅ v0.11.110 — `pickFarmEvent` returns null on FAIL nights; driver defers on `locationRequest`, re-arms if its menu is replaced; shrine continuation watchdog. |

**Not bugs / answered:** one-item Traveling Cart (Thrippalan, Reddit) = Cart Stall cap by design →
✅ v0.11.101 adds `LimitTravelingCartStock` config/GMCM, a first-visit merchant line (Joja squeezing
suppliers), and README/Nexus docs. "Keep Coop" = basic coop unless `keep_big_coop`/`keep_deluxe_coop`
are bought (SilencedLink — reply owed). Empty weekly themes lift the drawback by design (Bumblewyn —
consider a clearer HUD line). Feature asks: multiplayer (CausticOptimist), Challenging CC Bundles
compat + rewards preset (ada113, ErraticPixel), difficulty toggle / JP spend on successful seasons /
befriending quests (newmaaly, ThornDennan).

**Still to do for the release:** human smoke of a reset on the deployed build (stash day-28 deposit,
kept coop hopper, rod bait, museum re-donate, tea event, totem → rain, mixed seeds in summer, CC win
→ ceremony); What's New + CHANGELOG + Nexus changelog paste; reply on each bug thread + set status;
answer SilencedLink / Thrippalan / CausticOptimist. **No push/release without explicit "yes, push."**


### ✅ Nexus upload v3 migration — VERIFIED LIVE by the 0.11.60 release (2026-07-14)
The v3 mod-file id IS the old `file_group_id` (probe run 29268259621). All three repos use
post-migration pin `f6e1e2ea` with `file_id` = TLY 7502657 / AC 7118491 / CartCatalog
7497950. TLY's flow verified end-to-end by release run 29331424115 (0.11.60); probe
workflow deleted. AC and CartCatalog flows remain unexercised until their next releases —
rollback if one fails = pre-migration pin `ee1af4be` + old input names (dead after
2026-09-09, so a failure then needs a real fix, not the rollback).

### ✅ DONE 2026-07-15 — Nexus changelog for 0.11.60 posted (browser-driven, verified live)
Entered via the new edit UI (Manage → Documentation → Add changelog, file/version auto-matched
0.11.60) and verified rendering on the public Logs tab. The 0.11.60 release is now fully closed.

<!-- RESOLVED 2026-07-14: description sync (Translations section + What's New 0.11.60 live
via release.ps1); Advanced Options screenshot uploaded to the gallery 2026-07-13
(release-notes/advanced-options-remixed.png); Fluxwb replied on the posts tab pointing at
docs/TRANSLATING.md — awaiting their updated zh translation to credit + link. -->

### 🐞 INVESTIGATE — 5th sweep (2026-07-09): Nexus posts 06-10 → 07-05 + xsansara log
*Full forum sweep 2026-07-09 (`forum-sweeps/2026-07-09-21-47_*`). Reddit: 4 new comments, all
praise/flavor — nothing actionable. Nexus bugs tab: no new bugs. All the new material is Nexus
POSTS. xsansara's awaited SMAPI log delivered
(`test-output/SMAPI-xsansara-0.10.0-broke.txt`, from https://smapi.io/log/f4734fe4799f4565bdcb5f0302eedb4e).
No new DMs since Jun 10 (VeggieGirl43 BC retest still unanswered).*

- **✅ FIXED v0.11.11 (3d7210e) — Remixed bundles that miss every classification rule were
  SILENTLY DROPPED from season checkpoints + weekly themes.** Root cause: `BundleClassifier`
  returned null for pick-X-of-Y bundles whose NAME isn't in `DefaultBundleQuotas` (only the 7
  vanilla names). Fix: unknown X<Y bundles now classify as Percentage with a derived cumulative
  ramp `floor(X * [0.25, 0.5, 0.75, 1.0])` (Winter demands full X; matches curated Chef's at
  X=3); curated quota entries still win by name; builder logs the derived ramp at INFO. Also
  covers SVE/custom-bundle-mod bundles (PokeTheSilver204's compat question). 499 tests pass.
  **✅ LOG-VERIFIED 2026-07-10 (unattended, v0.11.32):** the user's test save turned out to use
  STANDARD bundles, so the remix path was exercised in memory instead: `debug ShuffleBundles`
  (vanilla's own remix generator) + new `tly_classify` (re-runs the real builder, diagnostics
  only). Result: `26 classified (0 category-only skipped, 0 unclassified skipped)` with
  `using derived ramp` INFO lines for Brewer's [1,2,3,4], Wild Medicine [0,1,2,3], and Treasure
  Hunter's [1,2,3,5] — all ramps monotonic and ending at X. Nothing persisted (no save).
  *Curated per-name ramps for the remix pool = 0.12.0 balance-pass material.* Original report:

  **Remixed bundles that miss every classification rule are SILENTLY DROPPED from season
  checkpoints + weekly themes — undermines the gate on the RECOMMENDED config.** Log-confirmed in
  xsansara's log: `BundleCatalogBuilder: bundle 'X' didn't match any classification rule` ×6
  (Rare Crops, Brewer's, Wild Medicine, Treasure Hunter's, Children's, Winter Star) →
  "20 classified, 6 unclassified skipped." Corroborated by *khauser13 (13 Jun)*: remixed bundles
  (Home Cook's, Forest's, Quality Fish) "did not show up in the checkpoints for the season or in
  the weekly themes"; some weekly themes "came up blank on required donations." *xsansara (11 Jun)*
  likewise: Garden-bundle flowers / Chef / Fodder items missing from season requirements. **Fallout
  includes a premature WIN:** *khauser13 (12 Jun)* deliberately withheld the last CC item, hit all
  (classified-only) winter checkpoints, and the mod counted the loop complete — Chef's bundle was
  never in the checklist. **Fix direction:** ensure EVERY live bundle classifies (add rules or a
  safe fallback quota), so unclassified never silently shrinks the gate. Interim for 0.12.0; the
  0.13.0 owned-bundle engine retires the whole class.
- **✅ FIXED v0.11.12–0.11.19 + PLAYTEST CONFIRMED 2026-07-09 — weekly theme quantity bug +
  impossible themes + already-donated asks, all three killed by one redesign.** Playtest (user,
  live save): 1 regular parsnip into Spring Crops → no tick, no bonus (`+3 JP`, the exact move
  that faked the old code out); 5 gold parsnips into Quality Crops → goal ticked + `(bonus x1.5)`
  in the log. Test items RETIRED. Same session also shipped: v0.11.20 quest tip moved below the
  checklist (user feedback), v0.11.21 horse re-name-every-morning fix (✅ CONFIRMED 2026-07-13
  first morning with a stable; overnight warp-to-stable = vanilla `Stable.dayUpdate → grabHorse()`,
  not ours), v0.11.22 `tly_select` force-any-theme for playtesting, v0.11.23 ghost-picker fix
  (theme pick now consumes the week's offer; stale deferred offers dropped).
  Spec `docs/superpowers/specs/2026-07-09-slot-based-weekly-theme-checklist-design.md`, plan
  `docs/superpowers/plans/2026-07-09-slot-based-weekly-theme-checklist.md` (user-approved design:
  exact-slot goals, seeded-random slot choice, empty pool → no quest + drawback auto-lift).
  Weekly goals now point at SPECIFIC still-open bundle slots (`Parsnip x5 (gold) - Quality Crops`)
  and tick only when that exact slot flips complete in live CC state — vanilla enforces the full
  stack + quality, so 1x parsnip can no longer clear a x5 goal (khauser13). The pool samples only
  OPEN slots, so goals can't ask for already-donated items (Tutorem/emmainthealps/xsansara/
  Dusklight7 — the old "by design" ruling was overturned by the user 2026-07-09) and structurally
  impossible themes can't be generated (khauser13's mining case). Fewer open slots → shorter
  checklist; zero → no quest, drawback lifted, no JP. The 1.5× JP bonus is slot-strict. Hub
  preview uses the same sampler + shows per-slot stack/quality; new hub line "Banking items for a
  matching theme week pays 1.5x JP." + reworded quest tip. Mid-week saves migrate with a one-time
  goal re-roll. 498 tests pass; boot smoke on PC clean (46 patch classes, 0 errors); final holistic
  review = ready. **PENDING PLAYTEST:** pick a theme (goals name slot+bundle), donate a partial
  stack (no tick), complete the exact slot (tick + 1.5× in log), late-run short/empty weeks lift
  the drawback; hub tip renders without layout overlap.

*Original reports (for history):*
- *khauser13 (11 Jun)*: weekly quest wanted 5 crops (quality-crops slot); donating a single
  parsnip into the spring-crops slot ticked it. Reproduced with melons.
- *khauser13 (11 Jun)*: mining theme asked solar essence + void essence + slime/bat wings when
  the CC only has 2 matching turn-in slots — impossible regardless of play.
- **✅ FIXED v0.11.26 — Green rain never triggers in summer.** *khauser13 (11 Jun)*. Root cause
  confirmed: `WeatherModificationsPatch` returned the scheduler's choice unconditionally for
  non-festival days and the scheduler had no GreenRain concept, so vanilla's green-rain override
  (set inside `getWeatherModificationsForDate` before the postfix) was clobbered every summer.
  Fix: `GreenRainDay.VanillaSummerDay()` resolves vanilla's pick (seeded on year+uniqueID, so it
  moves each loop) and the scheduler reserves it like a festival day BEFORE placing storms/rain —
  ≥2-storm/≥2-rain minimums + week-1 rain guarantee unit-covered. Weather Sage previews show the
  1.6 green-rain icon + "Green Rain" hover. (Summer 13/26 fixed storms staying gone = WAI.)
  **✅ PLAYTEST CONFIRMED 2026-07-13:** shrine forecast showed Green Rain on Summer 15 (day 5 =
  plain rain, correctly distinct); the 15th actually ran green rain; a 2-day thunderstorm followed
  plus rain on the 5th and 24th — storm/rain season minimums met on a live schedule.
- **✅ FIXED v0.11.24-25 — Loop reset coverage gaps (Dusklight7's reset-leak audit).** All
  keep-vs-reset decisions user-approved 2026-07-09 (full reset on every surface). Root causes +
  fixes:
  - *Museum donations/rewards* (v0.11.25): `MuseumPieces` lives on `Game1.netWorldState` (same
    survival class as the CC bundles — `loadForNewGame` doesn't rebuild netWorldState dicts);
    reward mail WAS wiped, so persisting donations re-armed the reward ladder every loop. Now
    cleared in `PerformReset` — the museum rewinds with the year.
  - *Worn clothes + rings + trinkets* (v0.11.24): equipment slots are separate Farmer fields the
    p.Items wipe never touched. All slots (hat/shirt/pants/boots/rings/trinkets) now unequipped
    via vanilla's hooks (buff recompute included).
  - *Monster-slayer progress* (v0.11.24): `stats.specificMonstersKilled` persisted while `Gil_*`
    mail was wiped → instant ring re-claims each loop. Cleared.
  - *Mine milestone chests* (v0.11.24): `chestConsumedMineLevels` persisted → chests never
    respawned. Cleared — each loop's descent re-earns the gear ladder.
  - *Books read / mastery / prize tickets* (v0.11.24): run-scoped `Stats.Values` keys removed via
    new pure `StatResetRules` allow-list (`Book_*`, `mastery_*`, MasteryExp, masteryLevelsSpent,
    ticketPrizesClaimed, specialOrderPrizeTickets). Mastery was a found-in-audit leak: the floor
    was only SET for Keep Mastery owners, never wiped for non-owners. Lifetime cosmetic counters
    (steps, shipped, …) deliberately stay.
  - *Day-1 parsnips*: already fixed 2026-05-30 (`RemoveStarterGiftBox`, shipped in 0.10.0) —
    likely a stale observation from an older build; run 1 keeps the box by design.
  - *Ancient seed*: WAI per 2026-06-10 decision (unchanged).
  **✅ PLAYTEST CONFIRMED 2026-07-13 (one `tly_failreset` pass, Run 27→28):** museum wiped (6
  donations in the log) + lost-book pages reset (0.11.37), all equipment slots emptied + trinket
  slot gone (0.11.28), Gil offers nothing + guild door re-locked (guildMember wiped; vanilla
  re-invite arrived on schedule after floor 5), floor-10 chest respawned, book buff gone, and
  maxHealth/stamina rewound 500→100 (0.11.40).
  **⚠ RULING REVISED v0.11.41 (user, 2026-07-13, after seeing the wipe live):** hat/shirt/pants
  now STAY WORN across resets — cosmetic-only slots, and the character-creation outfit is recorded
  nowhere, so wiping strips the player's look irreversibly. Boots/rings/trinkets still wipe.
  **PENDING next reset (on 0.11.41):** clothes stay on, boots/rings/trinkets still wipe.
- **✅ DONE v0.11.27 — JP upgrade request ×2 — `keep_silo`.** *khauser13 (11 Jun)* + *Dusklight7
  (05 Jul)*. Buildings category, 150 JP, gated on `building:Silo` reach (evaluator gained an
  exact-match fallback for non-chain buildings); rebuilt each loop at (60,9) between the coop and
  barn tiles. Hay does not carry over. **✅ PLAYTEST CONFIRMED 2026-07-13:** with a silo built
  this run the shrine shows the row (150 JP, Buildings) — user bought it. **PENDING next reset:**
  silo rebuilds at (60,9).
- **✅ 0.12.0 ECONOMY/CLARITY HALF SHIPPED v0.11.61–68 (2026-07-14, deployed to PC Mods, boot
  smoke 49 patches/0 failed).** Specs `docs/superpowers/specs/2026-07-14-tly-0.12.0-*.md`; plan
  `docs/superpowers/plans/2026-07-14-tly-0.12.0-economy-clarity.md`; SDD ledger
  `.superpowers/sdd/progress.md`. Landed: donation-JP double-pay removed (AwardInterimJp),
  season-checkpoint JP award (100 × entering-season mult → 150/250/400), xp_mult upgrade family
  (5 skills × x2..x5 at 100/200/350/550 + 3000 JP "Junimo Insight" x10 capstone, capstone-only
  touches Mastery), hub line advertising the season multiplier (VERIFIED rendering), empty-week
  "Junimos are overwhelmed" reword. **PENDING user playtest:** checkpoint-award HUD toast
  visibility at day-end fade (log line is authoritative), xp_mult rows in the shrine + the
  "  (insufficient)" leading-space eyeball (next natural shrine visit), a real donation single-pay
  log check. Cult repricing (red cabbage/starfruit) DEFERRED pending engine-baseline playtest.
- **✅ 0.12.0 ENGINE PLAN 1 of 3 (SKELETON) SHIPPED v0.11.69–80 (2026-07-14, deployed to PC
  Mods, live-smoked end-to-end on a cloned save).** Plan
  `docs/superpowers/plans/2026-07-14-tly-0.12.0-engine-1-skeleton.md`; final review READY;
  606/606 tests. TLY now WRITES its own bundle set (vanilla + remix pools) at run-create and
  every reset: seed = `player.UniqueMultiplayerID ^ CompletedResets·prime` (spec amended — the
  save's uniqueID is wall-clock re-rolled, do not "correct" back), vanilla's own absolute
  bundle-index space (migration write overwrites the legacy board — ghost-bundle merge + the
  CC-creation crash were caught and fixed in smoke), no duplicate bundles per room,
  value-strict manifest check with legacy fallback, season-gate ramp clamp, `tly_genbundles`
  diagnostic, requirements-source INFO line on every load. Pre-engine saves stay on the legacy
  read path until their next reset (no migration code).
- **✅ 0.12.0 ENGINE PLAN 2 of 3 (EXPANDED POOLS) SHIPPED v0.11.81–91 (2026-07-20, deployed
  to PC Mods, vanilla-smoked end-to-end on a cloned save; final review READY after 1 fix).**
  Plan `docs/superpowers/plans/2026-07-17-tly-0.12.0-engine-2-expanded-pools.md`; ledger
  `.superpowers/sdd/progress.md`; 642/642 tests. Picked bundles now RE-ROLL their slot
  contents from pools derived from the game's own data (SVE-proof by construction): seasonal
  crops/foraging keep their season but draw ANY season-valid item, fish re-roll within their
  original habitat (spawn-location overlap), Adventurer's from pure monster loot
  (price-banded stacks), Engineer's from metals (iridium bar = rare roll), Brewer's from
  artisan goods; curated harder forage additions + large-quantity (x40–99) forage asks; all
  review-carried requirements landed (seeded Pick-trimming v0.11.82, slash-guard v0.11.81,
  pool-provider unit tests v0.11.83); data-derived season pins feed the season-gate clamp
  (merged UNDER curated/user pins). Tuning knobs in one `GameplayConfig.PoolTuning` block
  (Plan 3 tunes to the Normal bar). Smoke caught + fixed: junk in fish spawn tables made
  Construction classify as Fish, and drop-table bars/gems made Geologist's classify as
  MonsterDrops → v0.11.90 type-pure pools (Type "Fish" / category −28); final review caught
  negated GSQ season clauses pinning items to their CLOSED season → v0.11.91 negation guard.
  Fresh-process disk reload = engine manifest byte-identical (no save-scum reroll). NOTE:
  unattended resets must use `tly_reset` — `tly_failreset` queues a cutscene that blocks
  without a player. **✅ SVE COMPAT PASS DONE 2026-07-20 (v0.11.92, user-authorized
  self-serve):** SVE 1.15.11 temporarily installed from the Vortex downloads zip (staging
  folder was an empty skeleton), verified live — pools widen with SVE content (crops 48→52,
  fish 54→56, artisan 20→23), SVE items landed in gate bundles (Gold Carrot, Butter,
  Nectarine, Pear), Gar confirmed year-1 (Forest West), 0 TLY errors, classify 26/0,
  determinism OK, and the value-strict manifest-mismatch guard fired live for the first
  time (changed pools → legacy fallback → self-healed at reset). Fix shipped v0.11.92:
  `ExcludedLocationMarkers` config list (Island / FableReef / CrimsonBadlands) — SVE's
  Shark (Fable-Reef-only) had slipped past the old "Island" substring into a year-1 board;
  it now appears only in SVE's own post-CC "The Missing" (non-themed, correct). SVE
  removed after the pass; Mods folder verified back to baseline (no Vortex purge); all
  scratch saves deleted.
- **✅ 0.12.0 ENGINE PLAN 3 of 3 (AUTHORED BUNDLES) SHIPPED v0.11.93–100 (2026-07-20,
  deployed to PC Mods, smoked on cloned saves, final review READY after 2 trivial fixes).**
  Plan `docs/superpowers/plans/2026-07-20-tly-0.12.0-engine-3-authored-bundles.md`; spec
  `docs/superpowers/specs/2026-07-20-tly-0.12.0-engine-3-authored-bundles-design.md`;
  662/662 tests. **Eleven authored bundles** join the room pools as remix candidates
  (Artifact, Mineral, Book→Book of Stars reward, Tapper's, Four Seasons Sampler→Tea
  Sapling x2, Orchard (NEW, saplings ex-Banana/Mango), Preserver's, Home Cook's Feast,
  Weatherman's, **Gil's Trophies** (11 eradication rewards incl. Warrior Ring — donate 2
  of 4 shown; trophies re-earn each loop so no uniqueness exclusions), Recycler's), each
  composed once per generation from a per-def-name seeded stream, slots FINAL (exempt from
  the domain filler, v0.11.100). **Weapon/hat donation enabled** by a two-patch cluster
  (inventory-highlight wrapper + ingredient-icon gate; everything else in 1.6 is already
  type-agnostic) behind `EnableNonObjectDonations` (config.json; off = rings-only compose;
  mid-loop flip can strand an in-flight trophy bundle until next reset — documented).
  **Vault engine-owned +25%** (3,125/6,250/12,500/31,250g, names match; multiplier is a
  tuning knob; vanilla's own asset stuffs the amount in the quality field — preserved,
  not a bug). Tea Leaves in the crops pool (Spr/Sum/Fall) + Green Tea via artisan.
  `tly_trophytest` proves (W)13/(H)8/(O)520 match+accept programmatically.
  **PENDING USER PLAYTEST:** live CC click-through of a weapon/hat donation (the one
  check automation can't do), authored-bundle feel, Vault +25% feel, Normal-bar
  difficulty impressions → drives the PoolTuning pass, then the cult repricing decision.
  **REMAINING for 0.12.0: the Normal-bar tuning playtest loop (knobs all in
  `GameplayConfig.PoolTuning`) + cult repricing decision — then 0.12.0 releases.** (Artifact/Mineral/Book + 7 surveyed authored bundles w/ rewards —
  keep authored names slash-free (Uniquify runs before Sanitize), Vault +25%, baseline tuned
  to the Normal bar: a very skilled player cannot 1-loop it; flavor note: Garden currently
  re-rolls from ALL crops — consider a flowers filter), then the cult repricing decision.
- **⚖️ Balance (0.12.0/0.13.0 fodder — difficulty too low for strong players).** *khauser13
  (12-13 Jun)*: finished the year first try on BOTH standard and remixed (sleeping idle days);
  suggests harder bundles / permanent debuffs; winter weekly themes felt pointless (everything
  already donated); keep-XP prices feel too high vs. an XP-multiplier upgrade idea. *PokeTheSilver204
  (13 Jun)*: red cabbage is the only year-1 blocker → red-cabbage-cultivation JP upgrade trivializes
  run 2; asks for custom-bundle-mod support or challenge modes / 1-year-perfection mode. *xsansara
  (11 Jun)*: breezed through spring at 25% profit. *Dusklight7 (05 Jul)*: deliberate spring-reset
  JP farming is a cheese path — suggests mid-run JP spending (at a premium) or a JP incentive for
  season checkpoints to reward progressing. All feeds the 0.13.0 difficulty engine; author already
  replied to khauser13 (14 Jun) promising rebalancing.
- **✅ REPLIED 2026-07-10 — Chinese translation approved (Fluxwb, Nexus mod 47926).** User's
  reply (on the TLY posts tab, in Chinese + English): thrilled to have another language, only
  asks for credit + a link to the original in their mod description, and offered to make future
  translations easier. **Follow-through parked pending explicit go-ahead: i18n support** (string
  extraction to `i18n/` JSON so translations stop requiring DLL edits — Fluxwb's copy is frozen
  at 0.11.0 for exactly this reason, and the user's public offer implies it). Large pass; do NOT
  start unprompted.
  **✅ DONE 2026-07-13 — i18n string extraction complete (pre-0.12 queue item 4).** Every
  player-visible string now lives in `src/TheLongestYear/i18n/default.json`; guard tests cover
  literal-key scanning, catalog resolution, and orphan/token checks (v0.11.55-58). Translator
  docs added at `docs/TRANSLATING.md`; README gained a matching "Translations" section. Fluxwb
  (and any future translator) can now ship a `<locale>.json` with no DLL edits. In-game
  verification pass (Advanced Options screenshot aside) still pending per the task-14 brief.
- **📱 Android: can't buy from the Junimo shrine.** *Stardewlover87 (09 Jun)*. Android port is
  deferred; capture for the port task. (Likely the same Android ShopMenu/ISalable landmine class
  documented in AC memory.)
- **ℹ️ xsansara "money reset to 0 / drained on load" after 0.10.0 upgrade — log inconclusive.**
  The delivered log is a clean 3-minute session on a fresh Run 1 (Spring 5), zero errors; the
  broken save was abandoned and re-rolled, and xsansara can't reproduce. Keep an eye out for any
  other money-on-load report; nothing actionable now.

### 📄 Mod page: surface the remixed-bundles recommendation (promised to khauser13 2026-06-10)
khauser13: "Noticed in the change logs that it is recommended to use remixed bundles. You may want
to include a picture of recommended settings or note that in the mod description." Replied on Nexus
(2026-06-10) promising to add it. **Repo-side DONE (266d259):** Install step added to README +
nexus-description (content-identical). **Remaining:** a screenshot of the new-game Advanced Options
panel for the page, and the live-description sync (rides the next release's nexus-update run).

### ✅ VERIFIED + REFIXED v0.11.35 (2026-07-10) — Better Chests × Junimo Stash (was v0.11.3)
Live-tested with the real reporter mod: BC 2.18.6 + FauxCore 1.2.2 (BC is HIDDEN on Nexus since
Dec 2024 — author's own uploads still live on CurseForge). **The 0.11.3 Priority.Last capacity
postfix did NOT fix it** — VeggieGirl43's 70-slot grid reproduced, because BC sizes the chest
MENU via an ItemGrabMenu transpiler (`GetMenuCapacity` reads BC's per-chest ResizeChest OPTION,
never `GetActualCapacity`). Actual fix (v0.11.35): stamp BC's supported per-chest modData keys
on the stash at placement — `furyx639.BetterChests/{ResizeChest,StashToChest,AutoOrganize,
CarryChest}` = `"Disabled"` (same keys BC's configure-chest UI writes; also stops BC bulk-stash
dumping into the stash and carry-away). Screenshot-verified: 4-slot grid with BC active.
Inert without BC. Ask VeggieGirl43 to retest on the release that ships 0.11.35.

### ✅ FIXED v0.11.39 (2026-07-10) — Unlimited Storage (BC's successor) inflated the stash GRID
Cross-check with LeFauxMatt's live successor, Unlimited Storage 1.2.0 (Nexus 30323): its
transpiled `ItemGrabMenu` helper returned `BigChestMenu ? 70 : 36` for ANY chest context —
even `SpecialChestType.None`, even at default config — so neither the capacity postfix
(0.11.3) nor the SpecialChestType pin (0.11.36, kept as inert defense) could reach it.
**Fix (v0.11.39): `JunimoStashMenuContextPatch`** — vanilla keys the menu LAYOUT on
`sourceItem` while both BC's and US's transpiled helpers key on the ctor's `context` arg
(`context as Chest`); `Chest.ShowMenu` passes the chest as both, so a prefix that nulls
`context` for our tagged stash keeps vanilla 4-slot geometry and makes BOTH helpers fall
through. Generic against the whole menu-inflater class. Screenshot-verified vs US 1.2.0 with
BigChestMenu=true: 4-slot grid, 48 patches 0 failed, 0 errors. Known cosmetic residue: US's
search-bar + scroll-arrow chrome floats detached at the screen edge when the stash is open
(any US user — US keys its chrome on `menu.sourceItem ?? menu.context` and the stash is
ItemId 130 in its default EnabledIds). Suppressing would require nulling the menu's
sourceItem FIELD, which vanilla's window-resize rebuild and our color-picker draw guard both
need intact — disproportionate; leave unless a US user reports it.

### 🧾 SYSTEMATIC — one-time complete netWorldState keep/wipe audit (user request 2026-07-10)

> **NEXT UP (2026-08-26): Jeff wants a fresh agent to run this. Full brief, including the reset
> philosophy, where everything lives, how to drive the game, and the definition of done:
> `docs/superpowers/HANDOFF-2026-08-26-networldstate-audit.md`. Say "run the audit" and start there.**
Bundles, museum pieces, and lost books were all caught ONE REPORT AT A TIME from the same
survival class: fields on `NetWorldState` that the reset's loadForNewGame path never rebuilds.
`NetWorldState` is a finite class — enumerate every field once (decompile
StardewValley.Network/NetWorldState.cs), rule each keep/wipe against the full-reset
philosophy, implement the wipes, and this class of leak is closed permanently instead of
reactively. Pairs with the 0.11.38 StatResetRules wipe-by-default flip (same philosophy:
enumerate the exemptions, not the leaks).

### ✅ DONE v0.11.1 (2026-06-10) — event-hygiene pass: cave re-choice prompt replaces replaying Demetrius scene
The Demetrius cave cutscene (65) no longer replays every loop: it plays once (Spring-5 hold kept),
then `FarmerReset` clears `caveChoice` each loop and the new `CaveChoicePrompt` offers
mushrooms / fruit bats / decide-later on cave entry whenever unchosen (applies vanilla's
`hostActionChooseCave` effects). Furnace teach (992553) keeps replayable + recipe-known gating;
Lewis CC (191393) stays suppressed. ✅ PLAYTEST CONFIRMED 2026-07-13: post-reset cave entry showed
the picker, user: "nice cave picker, worked well" — wording approved.

### 🐞 INVESTIGATE — beta bug/UX reports (re-scrape 2026-06-08)
*Third scrape (Reddit 53 / Nexus 19). Concrete things to investigate, highest-value first.
New 2026-06-08 reports are tagged **[3rd scrape]**:*

**✅ NOT A TLY BUG — "SVE and Longest Year clash" (Nexus bug 1089299, TheFirstBanana, 0.9.6, 7 Jun 2026) — RESOLVED 2026-06-10.**
**Confirmed via pure-SVE test (no TLY, Odin, slept to day 5 / normal landslide timing): the Adventurer's Guild
door has NO lock.** So SVE itself removes the vanilla "proven adventurers only" gate — TLY is not involved.
Reply to the reporter that it's SVE's guild rework, not a TLY conflict. (Bug can be marked "Not a bug" on Nexus.)
Investigation detail below.


**Symptom (exact):** "The Longest Year appears to have eaten the SVE Adventurer's Guild cutscene. It never
happened, and I could go into the guild before killing 10 slimes."

**Findings (PC repro, SVE 1.15.11 + TLY 0.10.0, real log `test-output/SMAPI-sve-clash-day1.txt`):**
- Reproduced on **day 1, loop 1, no reset.** Log shows **zero** TLY event suppression / reseed that day, so
  the cross-loop reseed is NOT the cause of this report.
- Vanilla locks the guild: `Mountain.checkAction` tile 1136 → "proven adventurers only" unless
  `mailReceived "guildMember"` OR `hasQuest("16")` (user confirmed the lock on Switch).
- Warp order in the log: Mountain → `Custom_AdventurerSummit` (SVE area) → **AdventureGuild (10:20:56)** →
  Mine (10:21:38, *after*). So the guild was entered with no quest 16 and no prior mine visit.
- **TLY source never touches the guild** — no `guildMember`, no quest 16, no patch on that gate (grep-confirmed).
- **SVE rewrites the guild** — edits `Maps/Mountain`, adds `Custom_AdventurerSummit`, ships a `BeforeGuildMember`
  Marlon schedule, gates its own initiation cutscene (event `1000034`, precond `/j 10`) on `HasFlag guildMember`.
- **Conclusion:** the day-1 walk-in is SVE's own guild rework (SVE removes the vanilla "proven adventurers"
  lock), not a TLY effect. Confirm with the pure-SVE test (disable TLY → guild still enterable day 1) and then
  reply to the reporter that it's SVE's design.

**The reseed is WORKING AS DESIGNED, not a bug (user 2026-06-10):** repeat loops SHOULD skip already-seen
cutscenes (déjà-vu); only cutscenes "needed to get something" (furnace recipe, Demetrius bat/mushroom cave)
should replay. That's exactly what `EventGatingTables.Default.ReplayableEventIds` does.

**The ONE real generalization worth considering — mechanic-gating MOD cutscenes:** the `replayable` set is
hardcoded to 2 **vanilla** ids (furnace `992553`, cave `65`). A MOD cutscene that grants a per-loop mechanic
the reset wipes — e.g. SVE's `1000034` sets `guildMember` (cleared each loop by FarmerReset's `mailReceived.Clear()`)
— gets marked seen and never replays, so the player can't regain that unlock on loop 2+. By the user's own rule
("replay ones needed to get something") those SHOULD replay. **Possible general fix:** instead of a hardcoded
vanilla id list, detect mechanic-granting cutscenes by scanning the event script for unlock commands
(`addCraftingRecipe`/`addCookingRecipe`/recipe/mail/flag grants) and auto-add them to the replayable set — so
any mod's "teach/unlock" cutscene replays each loop automatically, while purely-narrative ones stay skipped.
Only do this if per-loop guild/mod-mechanic access actually matters in a run (guild combat rewards are optional
to CC restoration — user to decide). If it doesn't matter, there is nothing to fix here.

Also still worth checking under SVE (separate): whether `VaultBundleMap`/bundle-index derivation survives SVE's
remixed/expanded bundles, and whether world-reset warps land correctly on SVE maps.

### ✅ DONE v0.10.1 (2026-06-10) — generalized "replayable" cutscene detection so MOD unlock cutscenes replay each loop
Scope confirmed YES (regaining mod-mechanic access each loop matters for completeness across all mods), then
built max-recall per the user's choice. Spec `docs/superpowers/specs/2026-06-10-generalized-replayable-cutscene-detection-design.md`,
plan `docs/superpowers/plans/2026-06-10-generalized-replayable-cutscene-detection.md`. Commits `354b15d..a9c2a54`.
**Shipped:**
- Pure Core (`EventGatingTables`): `MatchedGrantToken` / `ScriptGrantsUnlock` (boundary-aware "/"-segment scan
  for `addCraftingRecipe`/`addCookingRecipe`/`addMailReceived`/`mailReceived`/`addQuest`) + `CollectReplayableIds`
  (flags grants, subtracts the exclusion set, unions the vanilla furnace/cave ids). Unit-tested.
- `ReplayableEventScan` (impure shell): at `SaveLoaded`, scans every live `Game1.locations` `Data/Events/*`
  (covers mod-added locations like SVE's `Custom_AdventurerSummit`), feeds the pure collector, caches the set;
  cleared on deactivate. `FarmerReset` reseed now OR's it with `EventGatingTables.Default`.
- Safety: exclusion set = `EventSuppressionPatch.SuppressedEventIds` (e.g. Lewis CC `191393`) ∪
  `RelationshipEventIndex.Ids`; config kill-switch `AutoDetectReplayableUnlockCutscenes` (default on) + GMCM;
  `tly_dumpreplayable` debug audit command. 489 tests pass; final code review APPROVED.
- **PENDING: in-game smoke test** — deploy, then `tly_dumpreplayable` on a TLY save should show furnace `992553`
  + cave `65` flagged, `191393` excluded=true, `config enabled=True`; SaveLoaded log shows the scan count.
- *Known limitation (documented): grants nested in `quickQuestion` `\`-delimited choice branches aren't matched
  — no vanilla teach uses that shape; revisit only if a choice-gated mod teach is reported as not replaying.*

**Fixed 2026-06-09 (4th scrape — khauser13 08 Jun 4:50PM + emmainthealps 09 Jun), bound for the v0.10.0 release:**
- **✅ FIXED v0.9.38 — Mine elevator did not lock on loop reset.** *khauser13*: "could still get down to
  floor sixty and I didn't buy the elevator unlocks." `WorldResetService` cleared only
  `LowestMineLevelForOrder`, but `MineShaft.lowestLevelReached` falls back to `LowestMineLevel` (never
  reset) + `deepestMineLevel` was only bumped up. Now all three pin to the kept floor (cap-not-grant).
- **✅ FIXED v0.9.43 — Weekly goal "Large Egg" didn't say which color it wanted.** *khauser13*: "needed a
  large brown egg, the white egg didn't count." Investigation (decompile + live save) confirmed the two
  large-egg slots in the Animal bundle are **vanilla** (6 animal products / need 5; 174 white + 182 brown
  are distinct CC items) — NOT a TLY bug, and not interchangeable. Tried an equivalence fix (v0.9.39-40,
  reverted); the right fix is to **name the color** in the quest log: `ResolveDisplayName` appends
  "(Brown)"/"(White)" for 174/182/176/180. Matching stays exact-id (vanilla-faithful).
- **✅ FIXED v0.9.41 — Stale vanilla "Rat Problem" quest appeared in a run.** *khauser13 / niki_m_m3*.
  `RatProblemQuestPatch` prefixes `Farmer.addQuest` to skip id 26 mid-run + strips it from existing
  saves on load; gated on `RunActivation.IsActive`.

### ✅ DONE v0.11.2 (2026-06-10) — reset paths consolidated into RunController.FinalizeReset
`ContinueAfterResetSpend` delegates to the shared finalizer; `ModEntry.FullResetAndPresentOffer`
(tly_reset/tly_resetif) is a thin alias, gaining the missing `ActiveEffectsProvider.Clear` (debug
resets leaked theme effects), `ForceFullSave`, and the real day-start flow — so `tly_reset` is now a
faithful stand-in for a real reset. `ApplyKeepPlaying` intentionally NOT routed (not a reset; shares
only the persist + day-start tail). Original notes below:

### 🔧 Tech debt — consolidate the three reset paths (found 2026-06-09)
There are **three** near-identical "reset world → BeginNewRun → persist → present week-1 offer"
sequences, each maintained by hand:
- `RunController.ContinueAfterResetSpend` — the real loop reset (fail-day-28 / win→new-loop / `tly_failreset`).
- `RunController.ApplyKeepPlaying` — the win→keep-playing branch.
- `ModEntry.FullResetAndPresentOffer` — the debug `tly_reset` / `tly_resetif` path.

Every cross-cutting fix has to be applied to all three and one always gets missed: the JP-refund guard
and the double-pick `OfferPresentedWeek` re-save (v0.9.25) both landed in `ContinueAfterResetSpend` but
NOT in `FullResetAndPresentOffer` (double-pick on `tly_reset` caught 2026-06-09, patched v0.9.44). They
also diverge behaviorally — the debug path calls `PresentOffer()` directly while the real path calls
`DoDayStartSeasonAndHub()` (season setup + more), so **`tly_reset` is not a faithful stand-in for a real
reset** (this muddied the v0.9.38 mine-elevator test). **Fix:** extract ONE shared "finalize reset"
routine (reset → drain picker → BeginNewRun → clear effects → save → ForceFullSave → day-start hub →
re-save offer marker) and route all three callers through it, so fixes land once and `tly_reset`
exercises the real path. **Refactor — do it AFTER the v0.10.0 release** (no refactor mid-bugfix).

- **✅ FIXED v0.9.28-29 — Bus-repair (Vault) goal renders inconsistently in the Season Goals menu.**
  User (2026-06-08): "the bus repair in the season goals is completely different from all the other
  goals — make it consistent." Restyled the vault/bus-repair entry in `SeasonGoalsMenu.cs` into a
  real list row matching the bundle goals (commits `dd41590` + `e161727`).

- **✅ FIXED v0.9.26 (confirmed) — Vault bundle indices were wrong on REMIXED saves (gate could never satisfy).**
  Now derived from live `BundleData` (room == "Vault"), remix-aware — verified via the v0.9.27 load
  diagnostic (`Vault bundles: 23=2,500g … 26=25,000g`). `VaultBundleMap` is the single source of truth;
  `VaultPaymentSync`/`DonationObserver`/`DonationService` all read it. Original investigation below:

  **🔴 INVESTIGATE — Vault bundle indices may be wrong on REMIXED saves (gate could never satisfy).**
  Found 2026-06-08 inspecting a live remixed save: the Vault money bundles are at indices **23–26**
  (`Vault/23`=2500g … `Vault/26`=25000g), but `VaultRules` hardcodes **34–37** (those are actually
  Bulletin's Dye/Fodder + Joja's "The Missing" in this save). Consequences if confirmed: `IsVaultIndex`
  rejects 23–26 → `DonationObserver` misclassifies a real vault payment as a normal bundle completion
  (`OnBundleCompleted`, never `OnVaultBundlePaid`) → `VaultBundlesPaid` stays empty →
  `IsVaultGateSatisfied` is false → the season gate can NEVER pass (unless `keep_bus_unlocked`). And
  `VaultPaymentSync.Reconcile` checks `isBundleComplete(34..37)` = the wrong bundles. **Verify:** is
  34–37 only correct for NON-remixed bundles (vanilla renumbers the vault under Remixed)? TLY
  *recommends* remixed, so this would hit the recommended config. If real, derive the vault indices
  from the live `BundleData` (room == "Vault") instead of hardcoding. Worked around in the 2026-06-08
  playtest with `tly_payvault spring` (count-based, index-agnostic). Confirm against a non-remixed save.

- **✅ FIXED v0.9.25 (confirmed) — Loop reset presented the weekly theme offer TWICE; first pick discarded.**
  Added a second `_store.Save()` right after `DoDayStartSeasonAndHub()` in `ContinueAfterResetSpend`
  (`RunController.cs`) so the deferred reload reads `OfferPresentedWeek` as set and the day-start guard
  skips the re-present. Playtest `tly_failreset` (Run 17): exactly one offer + one Selected line, pick
  stuck. Original diagnosis below:

  **🔴🔴 TODO (FIX NEXT) — Loop reset presents the weekly theme offer TWICE; first pick is discarded.**
  **CONFIRMED in playtest 2026-06-08 via `tly_failreset`** (picked Farming → forced Fishing/Mixed →
  Fishing overwrote it). **Safe fix confirmed:** the reset-time hub SURVIVES the reload (player picks
  from it), so persist `OfferPresentedWeek` BEFORE the deferred reload — add `_store.Save()` right
  after `DoDayStartSeasonAndHub()` (`RunController.cs:~368`) so `MetaStore.Load()` reads the marker as
  set and the day-start guard skips the re-present. No "no-picker" risk (hub survives). See
  HANDOFF-2026-06-08. Details below:
  Found 2026-06-08 during playtest (Summer 28 fail → Spring 1). The reset opens the Week-1 hub, but
  `OfferPresentedWeek` is set in `DoDayStartSeasonAndHub()` (`RunController.cs:368`) AFTER the
  post-reset save (`_store.Save()` / `ForceFullSave()` at 365-366), so the deferred
  `SaveLoaded → MetaStore.Load()` reloads `Run` with `OfferPresentedWeek = -1` and the day-start
  guard re-presents the offer. Because the first pick is now in `SelectedThemesThisMonth`,
  `SelectionService.OfferForWeek` re-rolls a different pair, so the **second pick overwrites the
  first** (log: picked Farming, then forced to pick again → Fishing). **NOT caused by the v0.9.19-21
  fixes** (those are all day-ending code); pre-existing, fires on every reset.
  **Fix carefully:** the naive "re-save after presenting" risks the OPPOSITE failure (no picker at
  all) if the reset-time hub is closed by the reload — first VERIFY whether that hub survives the
  reload, then either persist `OfferPresentedWeek` before the deferred reload OR present the offer
  only on the post-load path (once). This offer flow has a history of the "no picker" regression
  (see the win→keep-playing comments at `RunController.cs:714-719`). Confirm with a real reset.

- **🔴 Day-28 loop gate is unreliable — wrong reset/advance + JP-spend menu flashes away. [3rd scrape]**
  Two reports, opposite symptoms; root-caused 2026-06-08 to TWO defects (not one):
  - ✅ **FIXED v0.9.20** — *khauser13 (Nexus)*: **completed every** donation goal but it **reset to
    Spring instead of advancing**. Cause: the item-donation ledger was observer-only (live
    JunimoNoteMenu watcher) and could miss a deposit, so the gate read "failed." Added
    `ItemDonationSync` (item analogue of `VaultPaymentSync`) to reconcile the ledger from vanilla CC
    slot state at day-end before the gate eval.
  - ✅ **FIXED v0.9.37 (confirmed faithful repro 2026-06-09)** — *emmainthealps (Nexus)*: **failed** the
    28th yet the JP-spend menu flashed away and the game **advanced to Summer with progress intact**.
    The earlier "whole-CC-completion / `eventUp`" theory was WRONG. Real cause: finishing **just the
    Vault** on day 28 queues `ccVault` in `mailForTomorrow`, so that night vanilla plays the bus-repair
    **`WorldChangeEvent(7)`** — a **`Game1.farmEvent`, which is a SEPARATE flag from `eventUp`** and
    runs with `newDay==false` AND `eventUp==false` but `farmEvent!=null` (`Game1.cs:9340` clears newDay
    before `:9361` assigns farmEvent). TLY's day-28 driver + `MenuLauncher` guarded only on `eventUp`,
    so the shrine opened DURING the bus scene and the event's end-of-play warp (`Game1.cs:4977-4989`)
    tore it down without firing `exitFunction` → reset dropped. (That's also why the prior
    "defer until `!eventUp`" never fired — wrong flag.) **Fix, 3 parts:** (1) FAIL loop strips the
    rewind-doomed CC mail at day-end (`SuppressResetDoomedRoomScenes`) so the bus scene never plays;
    (2) day-28 driver + launcher now also wait on `Game1.farmEvent` for the PASS path; (3)
    `PerformReset` purges CC mail from `mailForTomorrow` (fixes the "bus fixed, 0 bundles done"
    carryover). Log proof: `Fail loop: suppressed 1 reset-doomed CC restoration scene ([ccVault%&NL&%])`
    with `farmEvent=none` through the reset. **PASS path also confirmed** (2026-06-09): with the gate
    passed on Spring 28 + Vault done, the log shows `Day-28 cutscene: deferring the Continue scene until
    the overnight FarmEvent (WorldChangeEvent) finishes` → opens 14s later with `farmEvent=none` → clean
    advance to Summer. Both branches verified.
- **✅ FIXED v0.9.19 — Kept smoked/preserved fish loses its inner-fish identity through the carry chest.
  [3rd scrape]** *emmainthealps (Nexus)*: a **Smoked Legend** carried back as a blank 57g smoked fish.
  Cause: the Junimo Stash serialized items to a lossy (ItemId, Quantity, Quality) record. Now captures
  + re-applies `preservedParentSheetIndex` / `preserve` / `price.Value` (covers all flavored goods —
  wine, jelly, aged roe, honey, bait, …). *Known remaining gaps: weapon enchantments/forged gems and
  colored-item tint not yet round-tripped — log if reported. Verify via a value-preservation playtest.*
- **✅ CONFIRMED NOT A BUG (2026-06-08) — "Keep tool upgrades" missing from the JP purchase screen.**
  The earlier diagnostic caught a *grant artifact* (a granted duplicate pickaxe in the bag, which
  `ToolLevel` resolved to the L0 copy). A real Clint upgrade replaces the tool in place, so the keep
  rows show in BOTH the planner and the spend menu and persist into the next run — faithfully retested
  2026-06-08. khauser13's report does not reproduce on a genuine upgrade. Original notes below:

  **⏳ "Keep tool upgrades" missing from the JP purchase screen. [3rd scrape]** *khauser13 (Nexus)*: shows
  in the planner but not the spend menu. **Root-caused 2026-06-08 — NOT catalog drift** (both menus call
  identical code). The tool-keep rows carry a `tool:<kind>:<tier>` reach requirement read LIVE from
  `Game1.player.Items` at menu-build time; the spend menu opens at the fragile day-28 morning where the
  tool may be absent/perturbed → reach reads 0 → rows filtered out. **Fix (pending, same boundary-race
  area as the emmainthealps half above):** gate the boundary reach on a pre-wipe peak snapshot
  (`WorldResetService.CapturePeaks`) instead of live inventory. Confirm with the same day-28 log.
- **~~Weekly task can request an already-donated item → penalty locked for the whole week.~~ BY DESIGN
  (user decision 2026-06-08). [3rd scrape]** *emmainthealps (Nexus)*: weekly tasks may ask for items
  already donated, locking the penalty for the week. **Intended** — part of the challenge; not a bug.
  No change.

- **✅ ADDRESSED — JP-spend confusion (Dusklight7 / TheFirstBanana / emmainthealps).** The planning
  view (`ShrinePreviewMenu`) already prints, near the top, "Planning view — you spend JP when a loop
  resets or you win, not here." That's the wording the author told testers was "in place." Ships in
  this release. (Further clarity will come from the Junimo intro in the story update.)
- ~~**Weather/luck desync (*u/Tutorem*, day 6).**~~ **INVESTIGATED 2026-06-07 — not an internal bug.**
  Traced the 1.6 flow: the in-game TV (TV.cs:326/589) AND the actual applied weather both resolve
  through the patched `getWeatherModificationsForDate` (Game1.cs:9594 → Default LocationWeather →
  `UpdateDailyWeather` sets real IsRaining), so both land on the same deterministic
  `WeatherScheduler` value per date — internally consistent. Vanilla's nightly "tomorrow" re-roll is
  overwritten by the override next cycle. The only thing that disagrees is the **external predictor
  tool** (recomputes from vanilla RNG, unaware of the mod) — user doesn't care about it. Day-3 rain
  intentionally removed by the scheduler; luck untouched (FarmerReset zeroes only the defunct Luck
  *skill*); 0 rain by day 6 is fine (≥2/season guarantee, not early). No fix. *Optional: confirm
  empirically from one playtest log (TV forecast at night vs next-day actual).*
- **✅ FIXED v0.9.21 (NPC routing) — CC reads as "restored" from day 1 — NPCs route into it (3 reports).**
  *u/Tutorem*: "needed Clint on day 5… he went to the CC instead." **[3rd scrape]** corroborated by
  *dm_me_your_kindness (Reddit)* (Granny/Gus/Clint) and *khauser13 (Nexus)* ("townspeople entering the
  community center… confused me"). Root cause: `CcLocationAccessiblePatch` forces
  `isLocationAccessible("CommunityCenter")` true for the player's door, but vanilla NPC scheduling reads
  the SAME flag — so it un-cancelled every villager's CC schedule. Fix: `NpcCcScheduleStayOutPatch`
  postfixes `changeScheduleForLocationAccessibility` to always cancel the CC entry during a TLY run
  (villagers use their default schedule), door stays open for the player. **Confirm villagers stay out
  via playtest.** *Note: the "looks visually restored" reports are NOT a TLY-flag bug — restoration
  keys on `areasComplete[]`, which TLY never sets; a player who finishes bundles sees genuine vanilla
  restoration. The NPC-dialogue idea (feedback-triage) is moot now that they stay out.*
- **Double-forage buff feels weak + double-XP question (*u/Tutorem*).** Buff "probably worthless past
  week 1-2 unless it affects truffles"; also asks whether double-forage grants double XP. Balance +
  a behavior question to answer.
- **✅ VERIFIED CLOSED — Greenthumb without purchase.** *khauser13 (Nexus, 05 Jun)*. `GreenThumbPatch`
  is correctly gated (`UpgradeChecker.GetTier("green_thumb",5)==0 → return`), so the mod's passive never
  fires unpurchased; the report was the day-1 junimo-notes-unlock gate (fixed alongside the bulletin
  board, v0.9.5). khauser played extensively on later versions (07-08 Jun) without re-reporting it.

### 📣 Community feedback triage (beta, 2026-06-06) — ideas/inspiration (replies are the user's)
*Mined from the r/StardewValley beta thread (1txuhfb) + Nexus mod 47192 posts.
**Replies are the user's to write** — idea/inspiration capture with attribution only.
Already-captured elsewhere: u/dcempire's "give the CC purpose after completion" → `mod-ideas.md` #3;
u/Khajiit-ify→Emmalution and u/petraliten→Poxial → `marketing/youtuber-outreach.md`; u/Gribbleby's
déjà-vu → the [1.0.0] entry below. Remaining items:*

- **Balance — early difficulty may be too low.** *u/Tutorem*: CC is "very doable in Y1" (often done by
  early Fall with seed-picking/resets); worried the challenge is soft at the start. Watch during the
  difficulty-tuning pass.
- **Balance — Traveling Cart RNG.** *u/jneedham2*: a lucky Cart buy (red cabbage / truffle / sandfish)
  can trivialize a run. TLY currently does nothing with the Cart; author is open to revisiting if it
  becomes the dominant win path. Decide whether to constrain/handle the Cart.
- **Compatibility — big-CC-content mods.** *ErraticPixel (Nexus)*: how does the 1-year gate interact
  with CC-overhaul mods whose bundles need >1 year to finish? Also asked about mid-save install
  (the per-save dormant gate covers that now). Worth a documented compat stance for large-CC mods.
- **Cutscene presentation.** *Dusklight7 (Nexus)*: the opening cutscene should show ALL the talking
  Junimos, not just the one recolored sprite. Fold into the cutscene overhaul above.
- **NPC-in-CC dialogue (turn the bug into flavor). [3rd scrape]** *khauser13 (Nexus)*: if townsfolk are
  going to be in the (abandoned) CC, give them dialogue explaining what they're doing there. Secondary
  to actually fixing the schedule routing (see the 🔴/CC-restored bug above) — capture as flavor only.
- **Design inspiration (reference, not a request).** *u/jneedham2*: vanilla "Prank Grandpa's Ghost —
  Glorious Victory" challenge (complete the remixed CC in five seasons) as a kindred framing.
- **Community art offer.** *triangulummortis (Nexus)*: offered a drawn banner / fan art; connected via
  Discord (Sonofskywalker3). No action needed beyond the user's own follow-up.

### ☆ TODO: brainstorm + write the "one-continuous-save trilogy architecture" spec
*Captured 2026-06-06. User decision: TLY1/2/3 all run **continuously on one save** (one evolving
campaign, not three independent runs/mods). This is a SEPARATE design from the story/cutscene pass —
needs its own brainstorm → spec. **User explicitly asked to be reminded to do this — surface it; don't
let it slip.*** Scope to cover:
- Save continuity spanning three "years"/stages; a year/stage state machine and how you advance TLY1→2→3.
- **Escalating win bar:** TLY1 = restore CC; TLY2 = CC + (if too easy) basic Perfection; TLY3 = ultimate Perfection.
- A **new layer of Junimo upgrades each year** to keep pace with the higher seasonal goals.
- How TLY2 (Ginger Island / Joja resort) and TLY3 (valley annexation + Morris redemption at Perfection) hang off it.
- Companion to the story brainstorm notes at
  `docs/superpowers/notes/2026-06-06-story-cutscene-brainstorm-notes.md`.

### ★ NEXT NON-BUG-FIX UPGRADE: animated loop cutscene + real ending cutscene
*Captured 2026-06-05. User-flagged as the priority once bug fixes are clear —
the next feature upgrade, not a polish afterthought.*

Two distinct cutscene pieces:

1. **Animated loop (reset) cutscene.** What we have now is *OK but static* — the
   user wants it **animated, not a still frame**. This is the transition the
   player sees when a loop resets (Winter 28 → next Spring 1). Make it feel like
   the year actually rewinding rather than a placeholder card.

2. **Real ending / victory cutscene.** The current 0.9 `VictoryMenu` is a
   placeholder (see the deferral note below + the `VictoryMenu` class comment).
   The real 1.0 ending should be a proper cutscene that shows:
   - **Joja giving up and closing the store** — the narrative payoff for
     restoring the CC and beating the loop.
   - **A Junimo party / celebration** (or similar) — the joyful button on the
     whole run.

Ties together with the already-deferred items below: the "Win screen → JP shrine
transition is jarring" entry explicitly defers transition polish into *this* real
ending work, so fold them together when this gets spec'd. Not yet spec'd —
needs an event-script design pass (custom `Data/Events`, Junimo sprite reuse from
`Characters/Junimo`, Joja-store staging at JojaMart).

**Known cosmetic to design OUT in the revamp (user decision 2026-06-10, do NOT fix in place):**
the Lewis day-1 intro cutscene renders a **black bar along the right side of the screen**
(xsansara's "black block" report; Jeff sees it too). Whatever causes the current intro's
viewport/letterboxing to come up short, the rebuilt 1.0 intro should avoid the same approach.

### [1.0.0] Déjà-vu villager dialogue — meta tracks (but doesn't preserve) relationships
**Source / credit: u/Gribbleby** on the r/StardewValley beta announcement thread
(https://www.reddit.com/r/StardewValley/comments/1txuhfb/ — 98 upvotes, 20k+ views). Their seed:
*"I assume relationships will also be reset? If somehow the villagers retained some memory it could
make for some fun Groundhog Day dynamics!"* — **credit u/Gribbleby if this ships.** (The specific
example lines below were the author's elaboration of that idea.)
*Captured 2026-06-05. Not yet spec'd. Corroborating interest 2026-06-06: **wolfseas** (Nexus) also
asked how heart events behave across loops — a second data point that relationship-across-loops is a
wanted direction.*

The loop wipes friendship every reset (villagers don't remember you) — but the **meta layer should
silently track cumulative interaction** per villager across loops, *without* preserving the actual
heart/relationship level. Once cumulative interaction with a villager is **significant**, occasionally
intercept their conversations to inject a faint subconscious-familiarity line — the loop bleeding
through. Examples the commenter gave:
- *"I swear we've met before — do you have a twin?"*
- *"I don't know why, but I feel very comfortable with you."*

Why it's great: it's the perfect thematic payoff for a time loop — the villagers can't *remember*, yet
something *lingers*. Rewards long-term players narratively without giving a mechanical head-start
(hearts still reset, so no day-1 gifting/marriage exploit).

Design seeds (needs a real spec):
- **New MetaState field:** per-villager cumulative-interaction counter (talks + gifts + heart events
  summed across *all* loops). This is the only thing that persists — the live `friendship` value keeps
  resetting via the existing reset path. Explicitly do NOT preserve hearts (same boundary as the
  barn-animal upgrades: track the meta, reset the mechanical level).
- **"Significant" threshold** gates eligibility for the déjà-vu lines (tune so it kicks in after a
  villager you've genuinely invested in across several loops, not someone you said hi to once).
- **Injection:** low random chance to prepend/substitute a déjà-vu line when an eligible villager
  starts a conversation — via a `Dialogue`/`NPC.CurrentDialogue` intercept or a `Characters/Dialogue/<name>`
  asset edit. Keep it **rare** so it stays uncanny, not spammy.
- **Line pool** (start with the two above, add more); could escalate tone with the cumulative counter
  (mild "have we met?" → warmer "I trust you for some reason").
- Keep it mysterious — never explain the loop in these lines; that's the intro/Junimo's job.

### Win screen → JP shrine transition is jarring (defer to the real 1.0 ending)
Playtest 2026-06-05: dismissing the 0.9 `VictoryMenu` cuts straight into the
JP shrine store with no easing — visually abrupt. **Deliberately deferred** —
the 0.9 win screen is a placeholder; the elaborate payoff cutscene is a 1.0
item (see `VictoryMenu` class comment). Fold the transition polish into that
real-ending work rather than patching the placeholder. No fix needed for the
0.9.x beta.

### Small playtest carryovers (from STATUS.md)
Picked up during the 2026-05-29 audit; STATUS.md was stale (last update
2026-05-27) so these were drifting:

- ~~**Festival exit to host map**~~ — closed 2026-05-29. The TODO
  entry described a behaviour that conflicted with what was already
  shipped. `FestivalTimeFlow.cs` (Plan 06A) handles festival exits
  end-to-end via `SkipExitFestivalPromptPatch` (skips the "are you
  ready?" prompt and runs `forceEndFestival` directly) +
  `EndBehaviorsPatch` (preserves `timeOfDayAfterFade` to the actual
  in-game time at exit, undoing vanilla's hard-coded 2200 jump).
  Result: walk into a map-edge warp → festival ends silently, player
  lands at the Farm porch, time stays whatever it was when you walked
  out — which is the behaviour the user identified as the desired one:
  "I'd walk out it would work me to the farm it would be the same time
  it was when I walked out and I could walk all the way back to town
  and be at the festival again." Briefly tried an alternative (host-map
  exit / direct warp through edge / two-patch combo) in `b49bf5b`; the
  user pushed back to restore the FestivalTimeFlow behaviour, so
  reverted. No new patch needed.
- ~~**Indicator `?` source rect**~~ — closed 2026-05-29. User feedback:
  "you never got the indicator right, so just remove it and close it."
  `IndicatorRegistry` deleted; `Dismiss` calls in CookbookMenu /
  CraftbookMenu / JunimoStashShowMenuPatch now write directly to
  `MetaState.DismissedIndicators` to preserve the one-time intro-quest
  gating. `WorldResetService.RegisterIndicators` + the SMAPI
  RenderedWorld hook + `JunimoStashService.RegisterIndicator` all gone.
- ~~**`forage_off` over-suppression (JC-4)**~~ — closed 2026-05-29.
  User: "it's not an issue." Current behaviour (Mining liability also
  blocks weeds/stones overnight via spawnObjects) stays.
- ~~**`fortune_rare_fish` is a 0.75× bite-rate multiplier (JC-2)**~~ —
  stale note, closed 2026-05-29. The 0.75× bite-rate wiring was already
  replaced by the Curiosity Lure piggyback in `FishRareLurePatch` per
  the 2026-05-28 audit. The "true rarity intercept" follow-up is the
  canonical implementation by design — Stardew has no abstract "rare
  fish" concept, rarity lives inside per-spawn `SpawnFishData.GetChance`
  thresholds (GameLocation.cs:13797). Curiosity Lure IS vanilla's
  rare-fish boost pathway; any further rewire would require
  reimplementing the spawn table. See expanded comment block on
  `FishRareLurePatch` for the full design rationale.

### (closed — moved here from "Open" 2026-05-29 audit)
### ~~Continue-after-victory mode~~ — SHIPPED 2026-05-29 as `5959de0`
Source: 2026-05-29 playtest spec. After the win condition fires (CC restored,
year complete, all bundles), the player should have the option to keep
playing the same run instead of being forced into a reset. Currently the
reset-trigger fires automatically at year-end on a completed CC.

Implementation notes:
- New flag in `MetaState` or `RunState` — `VictoryAcknowledged` — set when the
  player picks "continue" on the post-win screen.
- `WorldResetService` checks the flag before scheduling a reset; if set, the
  current run keeps going indefinitely (next month, next season, no roll-over).
- Acknowledgement UI: the existing JunimoShrineMenu or a one-off "you won"
  modal with "New loop" / "Keep playing" options.
- The player can still trigger a manual reset later via the shrine — the
  acknowledgement isn't permanent, just defers the auto-reset.
- JP banking can keep accruing during the continued run; donations after win
  still award JP at the usual season-multiplier, no special bonus.
- **JP-spend dialog at the end of the win scene.** Whether the player picks
  "New loop" or "Keep playing", surface the same Junimo Shrine purchase menu
  one more time so they can dump their banked JP on whatever upgrades they
  want active for the infinite run (or for the next loop) before the choice
  finalises. Reuses the existing `JunimoShrineMenu` — no new UI to design.
  Important: the menu has to fire AFTER the victory cutscene is fully closed,
  not stack on top of it, or controller focus + drawing layer get fighting.
- **JP-spend dialog ALSO pops on every natural loop reset** (Winter 28 → next
  Spring 1). User clarification 2026-05-29: "it's going to pop when you reset
  the loop or when you complete it, that's it." Same menu, two trigger paths.
  Important: must fire BEFORE `WorldResetService.PerformReset` commits, since
  the reset zeroes run-state (but MetaState.JunimoPoints survives the reset,
  so the spending CAN happen here — the constraint is purely UX, not data).
- **~~Remove the in-world JP shrine tile interactable.~~** Audited
  2026-05-29: no tile interactable was ever shipped. Plan 05 docs reference
  it as a design intent, but `JunimoShrineMenu` is only opened by
  `MenuLauncher.OpenShrineShop()`, which is in turn only called by the new
  reset/win popup paths and by the `tly_openshop` debug command. No tile
  removal needed.

Status: spec'd, not planned. Tagged as v1.x polish (the auto-reset isn't a
blocker — the player can manually save before the auto-reset hits if they
want to keep their post-win state preserved on a backup save).

### ~~Co-opted day-1 intro cutscene (replaces vanilla 191393)~~ — SHIPPED 2026-05-30 as `85b029b`, PENDING PLAYTEST
Shipped this session: `tly_intro_porch` (Lewis on the Farm porch, Joja
threat + Winter 28 deadline + landmark protection + hands over key) and
`tly_intro_cc` (Junimo loop-explainer inside the CC), injected via
`IntroEventInjector` asset edits and prepended to win first-match. Per-run
mail-flag chaining + cross-run `MetaState.HasSeenIntro` (set in OnSaving,
promoted to `tly_intro_done` on every load) gate the events. Retest via
`tly_replayintro` + `tly_reset`. Dialogue is one-pass, unreviewed — polish
deferred unless the user comments. Original spec preserved below for history.

(original spec)
Source: 2026-05-29 playtest. User saw vanilla event 191393 (Demetrius +
Lewis CC intro) fire on Spring 5 of a TLY loop. Suppressed for now via
`EventSuppressionPatch` (returns `-1` from `checkEventPrecondition` for
the 191393 key). The eventual replacement is a TLY-specific intro that
RE-USES the 191393 staging (Lewis at Town near the CC) with new
dialogue:

1. Plays the FIRST time on a new save, on day 1 — **before** the
   weekly-theme picker opens. (Currently the picker opens immediately
   on `SaveLoaded` if it's a new run.)
2. Lewis explains the Joja takeover threat in TLY terms (the year-loop
   stakes — Junimos rewinding the year if the CC isn't restored).
3. Lewis walks off; the player walks into the CC; a Junimo pops up to
   explain the loop mechanic (themes, donations, Junimo Points).
4. Must fire on a new save **even if the player skips the intro on the
   first try** — track a `MetaState.HasSeenIntro` flag, set only after
   the intro completes OR after the picker is shown post-intro.
5. Skippable on first run (vanilla `Esc` / B). Auto-skipped on every
   subsequent loop (the meta-state flag is preserved across resets).

Implementation surface:
- New cutscene script in a custom `Data/Events/TLYIntro` or appended to
  Town events.
- Hook `OnSaveLoaded` (existing TLY entry point) to play the intro
  before the picker the first time only.
- `WeeklyThemeQuestService` should know to wait for the intro to finish.
- Junimo NPC sprite already in `Characters/Junimo` (used by hub menu);
  reuse for the loop-explainer beat.

Status: spec'd, not planned. Will be one of the v1.1 narrative tasks.

### ~~JP upgrade: `keep_pet`~~ — SHIPPED 2026-05-29
See Resolved section below. Cost landed at 75 JP, sentimental tier.

### (closed) JP upgrade: `keep_pet` — pet persists with hearts
Source: 2026-05-29 playtest. New JP upgrade in the Animals / Buildings
category that preserves the player's pet (cat / dog / turtle) AND its
friendship hearts across loops, so a long-tenured pet stays maxed out
between runs.

Implementation notes:
- Pet is a `Pet` instance hanging off `Game1.player.activePet` (or the
  per-Farm `Farm.characters`). On reset (`loadForNewGame`) the pet is
  typically wiped along with the rest of the world.
- Need to snapshot in MetaState: pet kind (which species), name, water
  bowl state, and `friendshipTowardFarmer.Value`.
- On reset, re-instantiate the pet of the saved kind, set hearts, place
  in the farmhouse / on the porch the way vanilla day-1 adoption does.

**Critical contrast — barn/coop animals (the existing `keep_*_animal`
upgrades) must continue to start each loop at 0 hearts.** User spec:
"the 'keep 1 cow' should still start over with 0 hearts so they can't
be getting large milk day 1. same for all barn/coop animals." The
existing `WorldResetService.ApplyStartingAnimals` builds fresh
`FarmAnimal` instances each reset (friendshipTowardFarmer defaults
to 0), which already matches this requirement — but call this out in
the `keep_pet` design so future cleanup doesn't accidentally unify the
two paths and start propagating animal hearts too.

JP cost ballpark: 50–100 JP. User: "they can't do much for a run, it's
mostly for feelings." Pet doesn't gate a measurable progression vector
(no Large Milk, no shipping value) so the cost should reflect that
sentimental-only payoff rather than a typical run-saver price.

Status: spec'd, not planned.

### ~~JP upgrades: keep kitchen / keep basement / keep shortcuts~~ — SHIPPED
Audited 2026-05-29: all three are wired end-to-end. Catalog entries:
`keep_kitchen` (800 JP), `keep_basement` (1800 JP, requires keep_kitchen),
`keep_shortcuts` (900 JP). Effects:
- `RunBaselineBuilder` reads them into `KitchenOnDay1` / `BasementOnDay1`
  / `ShortcutsUnlocked`.
- `FarmerReset` forces `HouseUpgradeLevel = 1` or `3` accordingly.
- `WorldResetService` step 7b adds the `communityUpgradeShortcuts` mail
  flag (vanilla reads it in Forest/Mountain/Town/Beach for the five
  shortcut tile overrides). Step 7c creates the `Cellar` location for
  L3-house resets so the FarmHouse warp doesn't dead-end.

(Original spec preserved below for design history.)

### (original spec, kept for design history) JP upgrades: keep kitchen / keep basement / keep shortcuts
Source: 2026-05-28 playtest. User correction after a first-pass sketch
that bundled all Robin-related kept-state into one upgrade: "NO don't
bundle robin's upgrades, I want one for keeping the kitchen, one for
keeping the basement, and one for keeping the shortcuts, that's it."

Three separate JP upgrades. All three are independent of CC completion
(Robin sells them for gold in vanilla without any CC dependency).

**1. `kept_kitchen`** — preserve farmhouse upgrade level 1 across runs
   - Vanilla: 10,000g + 450 wood, 3-day build. Adds the kitchen room
     (cooking + fridge) and bumps `Game1.player.HouseUpgradeLevel` 0→1.
   - Reset behaviour: `FarmerReset` currently wipes HouseUpgradeLevel
     back to 0 every run. When this upgrade is owned, skip that wipe
     (or restore L1 after `loadForNewGame` rebuilds the FarmHouse so
     `resetForPlayerEntry` lays out the kitchen-tier interior).
   - The current cookbook unlock (`cookbook_1`) needs review for
     interaction — cookbook is meta-state, kitchen is run-state, but
     the player would expect both to feel "I have a kitchen this run."

**2. `kept_basement`** — preserve farmhouse upgrade level 3
   - Vanilla: 100,000g, requires L2 first. L3 adds the cellar (basement
     with 33 cask slots — the aging infrastructure for wine/cheese).
   - This upgrade should imply L2 (kids' room) as a side effect since
     L3 can't exist without L2 in vanilla data — the menu shouldn't
     even offer `kept_basement` until `kept_kitchen` is owned.
   - On reset, restore HouseUpgradeLevel = 3 (or use the highest owned:
     3 if `kept_basement`, else 1 if `kept_kitchen`, else 0).

**3. `kept_shortcuts`** — preserve Robin's 5 map shortcuts (one upgrade
   for all five, NOT five separate upgrades — user spec)
   - Vanilla: each shortcut purchased separately from Robin
     post-`Mountain_Shortcuts_Spoke_Robin` mail flag.
   - The five shortcuts (1.6):
     - Forest south fence → south Town path
     - Bus stop tunnel north
     - Forest tree stump bridge → Backwoods
     - Mountain → quarry path
     - Mountain → Town side route
   - Each unlocks via a mail flag like `OpenedTreeStumpShortcut` plus
     a passable-tile property toggle on the Mountain/Forest map.
     Need to verify exact flag names against the 1.6 PC source —
     check `Mountain.cs`, `Forest.cs`, `Town.cs` for `mailReceived.Add`
     calls keyed to shortcut tile properties.
   - On reset: `WorldResetService.PerformReset` re-adds all five mail
     flags after `loadForNewGame` (similar pattern to `landslideDone`
     in MountainUnlock).

JP cost ballpark (relative to bus repair = 100 JP):
- `kept_kitchen`: 75 JP (adds cooking ability + fridge — meaningful)
- `kept_basement`: 200 JP (skips two L3 prerequisites + 100k gold)
- `kept_shortcuts`: 100 JP (saves Robin's 15k×5 = 75k gold per run)

Status: spec'd, not planned. Out of scope for the current playtest
batch; queue as its own commit chain.

## Resolved / closed

- **Vault/money gate invisible + unpayable** — fixed 2026-06-06 (v0.9.8–0.9.16, master).
  Spec `docs/superpowers/specs/2026-06-06-vault-payment-gate-design.md`, plan
  `docs/superpowers/plans/2026-06-06-vault-payment-gate.md`. Changes:
  - **Gate reworked to count-based, tier-agnostic:** a season is satisfied when
    `VaultBundlesPaid.Count >= season ordinal` (Spring 1 … Winter 4), any tiers, or
    `keep_bus_unlocked`. Pay all four in Spring → every season pre-satisfied. Replaces the old
    exact-tier match.
  - **Payable in normal play:** `DonationObserver` routes vault-bundle (34–37) completions to a new
    `DonationService.OnVaultBundlePaid`, and `VaultPaymentSync.Reconcile` (vanilla
    `CommunityCenter.isBundleComplete` = source of truth) additively backfills the ledger at
    day-end / journal-open / shrine-open — also covers the mid-run-upgrade migration (already-paid
    bundles have no false→true transition to observe).
  - **JP scales with gold paid** (`JpSettings.VaultGoldPerJp` default 1000 → 2500g=3, 5000g=5,
    10000g=10, 25000g=25), no completion bonus, not season-multiplied.
  - **`keep_bus_unlocked` now needs run-reach `bus:4`** (all four), and the `bus` metric returns
    the paid count — resolves the old `bus:1`-only-via-debug deadlock.
  - **Green journal** (`SeasonGoalsMenu`) shows a pinned per-season "Vault (bus repair): X of N
    paid — MET/NOT MET" line. 462 tests pass. **PENDING: in-game playtest** (Task 11 step 4 of the
    plan) — not yet deployed/tested.

- **Continue-after-victory mode** — shipped 2026-05-29 as commit `5959de0`.
  JP-spend popup pops on both reset AND win paths; post-win choice
  dialog ("Start a new loop" / "Keep playing this run") sets
  `MetaState.VictoryAcknowledged` on Keep, which suppresses the popup on
  subsequent Winter 28 wins. Manual `tly_reset` stays raw (debug path).
  Plan-05 in-world shrine tile was never actually shipped — no removal
  needed.

- **`keep_kitchen` / `keep_basement` / `keep_shortcuts`** — shipped
  earlier; audit 2026-05-29 confirmed all three are wired end-to-end
  (RunBaselineBuilder → FarmerReset HouseUpgradeLevel + WorldResetService
  cellar/mail-flag step). TODO entry above kept for design history.

- **`keep_pet` upgrade** — shipped 2026-05-29 as `PetCarryoverService`
  + `MetaState.PetState` + `PetSnapshot` record. 75 JP, Buildings
  category. Snapshots kind / breed / name / friendship before
  `loadForNewGame`, restores at the Farm porch after starting-animal
  placement, sets the `MarniePetAdoption` mail flag to suppress
  vanilla's day-1 adoption offer. Barn/coop animals still start fresh
  (0 hearts) per spec — only the pet carries hearts.

- **Seed-driven weather scheduler** — shipped 2026-05-28 as
  `WeatherScheduler` + `WeatherModificationsPatch`. Per-season minimums
  (≥2 rain Spring/Fall, ≥2 storm + ≥2 rain Summer, ≥2 snow Winter),
  deterministic from `(uniqueIDForThisGame, seasonIndex)`. Subsumes
  the prior day-3 forced-rain bypass + Summer 13/26 hardcoded storms.
  Commit 14322d4.

- **`tly_wipemeta` debug command** — shipped 2026-05-28 as
  `MetaStore.WipeMeta()` + `CmdWipeMeta`. Replaces State with a fresh
  MetaState() and persists immediately. Commit 61ab125.

- **UX6 — always-on JP HUD** — shipped 2026-05-28 as `DrawJpHud` on the
  existing `Display.RenderedHud` hook. Top-right corner, 2 lines (banked
  JP + active theme + 1.5×/lifted suffix). GMCM toggle. Commit 1a8e2b2.

- **Plan 06 effects layer (UX5)** — ALL ten modifier ids wired with real
  Harmony patches: `forage_yield_up` (ForageYieldPatch), `mines_closed` +
  `mine_drops_up` (MineDropsPatch), `crop_growth_up/down` (CropGrowthPatch),
  `fish_bite_up/down` (FishBiteRatePatch), `forage_off` (ForageOffPatch),
  `all_drops_up` + `all_sell_prices_down` (AllDropsPatch). Liability/bonus
  mapping table preserved in design-spec docs.

- **Weekly Theme Journal entry** — shipped 2026-05-28 as `WeeklyThemeQuestService`.
  Creates a vanilla Quest on theme select with a 4-item checklist; each CC donation
  ticks a box; on completion awards +N JP (season-scaled) and suppresses the week's
  liability via `ActiveEffectsProvider.SuppressLiability`. Bonus stays active.
  Persisted via `RunState.LiabilitySuppressedThisWeek`. Commits 5bdb8f6 + 13776ed.
