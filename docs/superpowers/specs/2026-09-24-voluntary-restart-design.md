# Voluntary restart at the Junimo Shrine: design

Status: DRAFT, awaiting Jeff's approval (2026-09-24).
Origin: tanky24u, Nexus posts tab, 23 Sep. Decisions: Jeff, 2026-09-24 (TODO.md, top entry).
Branch: `master` (a gameplay feature, not story content).

## What it is

Players can start the year over whenever they want, from the Junimo Shrine (the Junimo statue on the
farm, `ShrinePreviewMenu`). A voluntary restart works like a failed season, with two differences: there
is no Junimo fail cutscene, and it happens as soon as the player confirms.

Why players want it: to chase long-haul goals (Key to the City, How to Win Friends) partway through a
loop or after finishing the Center, without risking the win. They can also give up early on a loop that
went badly.

## Player flow

1. The player opens the Junimo Shrine. The planning/buffs view has a new **Restart the year** button.
   The final wording goes through the game-writing skill.
2. Clicking it opens a vanilla yes/no box. Jeff's draft: "Are you sure? This resets all progress, just
   like a failed season." (Final wording also goes through game-writing.) Choosing No closes the box
   and changes nothing.
3. Choosing Yes runs the fail-night chain with the cutscene removed:
   - the **bundle hold question** (keep this board / let time reshuffle it), with the same prices and
     the same rules as a fail night. It is skipped on a Vanilla board, exactly as on a fail night;
   - the **upgrade menu** (JP spend);
   - the **Cookbook / Craftbook banking** step;
   - the reset. The player wakes on Spring 1 of the next loop.

## Rules

- **JP:** nothing is paid out. JP is banked as it is earned (donations, weekly quests, season
  checkpoints), so the whole bank carries into the upgrade menu, as it does on a fail night.
- **Counts as a fail.** It is a normal loop reset: the loop number goes up, the next loop gets a new
  board unless held, and the rule "each hold in a row costs more" applies. Season pity is being
  removed in the same round (Jeff, 2026-09-24), so the restart feeds no pity counter.
- **After "Keep playing":** also offered. It behaves like choosing "Start a new loop" on the win screen:
  it clears the won-run flag (`VictoryAcknowledged`) so the next loop can be won again. On the `story`
  branch it also clears `Year2WallArmed`. This will be resolved when master is merged into story.
- **When the button is hidden:** during festivals, events and cutscenes, on the day-28 night itself (the
  real fail or continue night owns that slot), and whenever another reset chain is already running.

## Timing (implementation note, to settle in the plan)

The fail chain runs at a day boundary today (the morning after the fail night). A reset that runs in
the middle of the day has never been tested. The plan should first try to reuse the existing
morning-time path: on Yes, queue a `Day28Branch.Fail`-equivalent with the cutscene turned off, then
end the day at once (fade out, no pass-out penalty) so the existing hold -> upgrade menu -> banking ->
`FinalizeReset` chain runs where it always has. Calling the chain mid-day is the fallback, and only if
a live test on the Rodger throwaway save proves that it is clean.

## Out of scope

- A keep for Key to the City or other late-game items (Jeff: thinking ahead, not this change).
- Year 2 content.

## Testing

- Core: a pure rule that decides whether the button is shown (dates, festival and event flags, won-run
  flag, reset in progress), with unit tests.
- Live, on the Rodger throwaway save, loaded through `tly_loadsave`:
  - restart in the middle of Spring: No does nothing; Yes runs hold -> menu -> banking -> Spring 1, JP
    is kept, and the loop number goes up;
  - Keep on the hold question carries the board over;
  - restart after `tly_win` and Keep playing: the flag is cleared, and the next loop can be won;
  - the button is hidden during a festival and on day 28.
