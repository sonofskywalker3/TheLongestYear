# Keep-bundles hold (0.13.0) design

**Date:** 2026-08-24
**Status:** approved in brainstorm, awaiting spec review

## Why

Feedback from the 0.12.x sweeps shows the loop itself lands (players accept being rewound),
but the *size* of some season asks does not. A 68-daffodil forage slot reads as "broken" to a
Stardew player, because nothing tells them the ask is meant to span loops, and the board
reshuffles every rewind so there is no point planning across loops.

The intended play was always: see a big ask, stash toward it over several loops, then make a
real run at it. This feature makes that explicit and possible:

1. The Junimos say up front, in the day-1 Community Center speech, that impossible-looking asks
   are expected and that the same wishes can be held across a rewind.
2. On every Fail night the player chooses, before spending at the shrine, whether to keep the
   current board for the next loop (first hold free, then an escalating JP price) or let it
   reshuffle (today's behavior).

Not in scope: the DerivePins obtainability clamp for artisan goods, dishes and geode tiers.
That gets its own brainstorm (ideas parked there: escalating per-season likelihood instead of
hard pins, and a pity counter that eases boards after N consecutive fails).

## Player-facing flow

**Day 1, CC intro event.** New line between `event.intro.junimo-9` and `-10`:

> Some of what a season asks may look beyond your reach. That is no mistake. It is the shape of
> the work.#$b#Gather what you can, and keep for later what you cannot yet use. When the year
> unwinds, we can even hold the town's wishes steady, so the next spring asks the same of you.$h

No mail echo. The speech is the only place this lives.

**Fail night.** After the day-28 Fail cutscene closes and before the shrine opens, a question
dialogue:

> Should we hold the town's wishes steady for your next spring?
> - Keep these bundles (free) / Keep these bundles (N JP)
> - Let time reshuffle them

- Picking keep with insufficient JP plays the "not enough" cue and re-shows the dialogue.
- If the dialogue is clobbered before an answer, the result is reshuffle.
- Not offered on Win or Continue nights.
- `cutscene.day28.fail` gains a closing sentence pointing at the choice.

**After reset.** With a hold, the season-goals board is the same set of bundles. Weekly theme
offers still roll fresh (they use `RunState.Seed`, unrelated). The Season Goals title ends in
"held Nx" while `ConsecutiveHolds > 0`.

## Cost curve

First hold free, then 50, 100, 200, 300 (cap). The counter resets to zero whenever the player
reshuffles, so the price is for *consecutive* holds. Story: holding the town's wishes steady
gets harder the longer they are held.

Curve lives in `GameplayConfig.BundleHoldCosts = [0, 50, 100, 200, 300]`; the last value
repeats for further holds. Tunable without code.

## Core rules and state (pure, unit-tested)

`MetaState` gains, next to `BundlesGeneratedForReset`:

| Field | Type | Default | Meaning |
|---|---|---|---|
| `BundleSeedLoop` | int | -1 | Loop number the board seed derives from. -1 means "same as `CompletedResets`" (legacy saves). |
| `ConsecutiveHolds` | int | 0 | Holds taken in a row; drives the price. |

The choice is applied at the prompt and persisted by the `_store.Save()` `FinalizeReset`
already performs. One bool, `HoldChoiceMadeForReset`, marks that a choice was made so a reset
that arrives without the prompt (console `tly_reset`, the post-win new-loop path) behaves as a
reshuffle; `PerformReset` clears it.

`BundleHoldPricing.CostFor(consecutiveHolds, config)` returns the price for the *next* hold.

`BundleHold.Apply(meta, keep, config)` returns `Kept | Reshuffled | NotEnoughJp`:
- keep: requires `JunimoPoints >= cost`; deducts; `ConsecutiveHolds += 1`; `BundleSeedLoop`
  unchanged (materialized from `CompletedResets` first if it was -1).
- reshuffle: `ConsecutiveHolds = 0`; `BundleSeedLoop = CompletedResets + 1`.

Both callers of `BundleEngineSeed.For(...)` on a loop board switch from `CompletedResets` to
the effective `BundleSeedLoop`:
- `WorldResetService.PerformReset` step 11a (generation on reset).
- `ModEntry.ResolveRequirements` (manifest re-check on save load). This is the correctness
  trap: if only the reset path changes, a held board is flagged as a mismatch on the next load
  and rebuilt.

Reset ordering: `PerformReset` increments `CompletedResets` before generating. `Apply` runs
before that, so "reshuffle" sets `BundleSeedLoop` to the *upcoming* loop number and "keep"
leaves it at the current one.

## Mod wiring

- `RunController.OnCutsceneEnded`, Fail branch: open the hold question dialogue; each answer
  calls `BundleHold.Apply` then continues into the existing `TryOpenShrineThenContinue` chain.
- Clobber guard: same idea as `TickShrineWatchdog`; if the question menu is replaced before an
  answer, apply reshuffle and continue.
- `SeasonGoalsBoard`: "held Nx" title suffix when `ConsecutiveHolds > 0`.
- Console: `tly_hold keep|reshuffle` forces the choice for smoke testing.

## Text (`i18n/default.json`)

New keys: `event.intro.junimo-9b`, `dialog.hold.prompt`, `dialog.hold.keep`,
`dialog.hold.keep-free`, `dialog.hold.reshuffle`, `dialog.hold.not-enough-jp`,
`menu.goals.title-held`. `cutscene.day28.fail` amended.
`IntroEventInjector` gets the matching `speak` entry between 9 and 10.

Separate commit in the same release: rewrite the 29 existing player-facing lines that contain
em dashes, meaning preserved. No em dashes in any new text.

## Testing

Core (xUnit, `tests/TheLongestYear.Tests`):
- `BundleHoldPricingTests`: curve values, cap repeat, config override.
- `BundleHoldTests`: keep deducts and increments; reshuffle resets counter and advances loop;
  `NotEnoughJp` leaves state untouched; legacy -1 materializes correctly.
- `MetaStateTests`: round-trip both fields, legacy defaults.
- Seed test: a held loop re-derives the identical `GeneratedBundleSet` on "load" (same inputs
  through `EngineModeDecider` / manifest path).
- I18n guard picks up new keys automatically.

Live smoke on a throwaway clone (never `None_443632257`):
1. Fail Spring, hold (free): board identical, JP unchanged, title shows held 1x.
2. Fail again, hold (50 JP): deduction visible, board still identical.
3. Reload from title after step 2: no manifest mismatch WARN.
4. Fail again, reshuffle: different board, counter back to 0, next hold offered free.

## Release

Patch bumps per commit on `master`; user decides when it becomes 0.13.0. No push or Nexus
update without explicit approval. README and Nexus "What's New" updated together at release.
