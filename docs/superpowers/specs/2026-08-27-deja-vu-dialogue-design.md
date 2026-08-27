# Deja-vu villager dialogue

**Date:** 2026-08-27
**Status:** design approved in brainstorm (Jeff); dialogue lines pending Jeff's review (see
`2026-08-27-deja-vu-dialogue-lines.md`), then build
**Credit:** u/Gribbleby (r/StardewValley beta thread) for the idea; credit in README Credits when it ships
**Story spec:** `2026-06-06-tly1-story-and-cutscenes-design.md` §3 ("the town forgets, faint deja-vu") and §9 item 2

## Problem

The rewind wipes every friendship, on purpose. Nothing in the town remembers the player, so a long-term
player builds the same bonds every loop and gets no narrative payoff for it. The idea: the meta layer
silently tracks how much the player has interacted with each villager across every loop, without
preserving hearts, and once that is significant the villager occasionally says something uncanny.
Never an explanation of the loop, only a feeling that lingers.

## Non-goals

- No mechanical benefit. Hearts, gifts, heart events all reset exactly as today.
- No loop explanation in any line. The intro and the Junimos own that.
- Phase 2 (not this spec): memories tied to events and festivals ("Did you come to town for the
  dance last year?", the Luau pot: "Just don't put that in the pot this year. Wait... why did I think
  that?" or "For some reason I think X would be amazing, if you have any" when the previous loop's
  soup was good). Recorded in TODO.md so the counters built here can feed it.

## How vanilla picks a line, and where this fits

`NPC.checkAction` (NPC.cs 2832) calls `checkForNewCurrentDialogue(hearts)` on every talk, and again
with `noPreface: true` if the first call finds nothing. Inside (NPC.cs 4009), step one walks
`Farmer.activeDialogueEvents` (`Introduction` lives there, re-seeded by 0.16.8 for six days after a
rewind): a matching unplayed line clears the stack, is pushed, and is stamped played with the
`<Name>_<key>` mail flag. Step two tries location and day specific lines. When both miss, the
morning's ordinary line already in `CurrentDialogue` plays.

`CurrentDialogue` is a stack, so a line pushed on top plays first and the villager's own line stays
underneath for the next talk that day (vanilla pops one Dialogue per conversation; verified live
2026-08-27 with George). The deja-vu line is therefore a **prepend**, never a replacement, and it is
skipped whenever the top of the stack came from an `activeDialogueEvents` key (the Introduction line
always plays untouched; vanilla clears the rest of the stack when it plays one, so nothing else plays
that day).

## Design

### Familiarity counter (Core, pure)

`MetaState.VillagerFamiliarity : Dictionary<string,int>` (villager internal name -> points), the only
thing that persists. Filled by a **nightly rollup** in `RunController.OnDayEnding`, no Harmony patches
on talk or gift:

| Signal (read from live `friendshipData` / `eventsSeen`) | Points |
|---|---|
| `Friendship.TalkedToToday` | +1 |
| each `Friendship.GiftsToday` | +3 |
| a relationship event seen today (`RelationshipEventIndex` gains `NpcFor(eventId)`, parsed from the "f <npc> <points>" precondition it already reads) | +10 |

`FamiliarityRollup.Apply(meta, IEnumerable<(string npc, bool talked, int gifts, int heartEvents)>)`
is the pure Core function; the glue gathers the tuples. Heart events seen today are found by diffing
`eventsSeen` against a per-day snapshot kept in `RunState.EventsSeenAtDayStart`.

### Eligibility and rarity (Core, pure: `DejaVuRules`)

A villager is eligible when all of these hold:

- `meta.CompletedResets >= 1` (never in loop 1: there is nothing to half-remember);
- `VillagerFamiliarity[npc] >= DejaVuThreshold` (config, default **60**);
- the villager has not had a deja-vu line this loop (`RunState.DejaVuShownTo : List<string>`);
- no deja-vu line has played anywhere in the last 7 days (`RunState.DejaVuLastDay`, days played);
- not a festival day, not marriage dialogue, and the top of the dialogue stack is not an
  `activeDialogueEvents` line.

On an eligible talk the chance is `DejaVuChancePercent` (config, default **6**) rolled on
`Game1.random`. Tier: familiarity below `3 * DejaVuThreshold` (180) draws from the villager's tier-1
pool ("have we met?"); at or above it, tier 2 ("I trust you, somehow").

Expected feel: a villager you have talked to most days for about two loops starts saying these in
loop 3; with the weekly cap a full year surfaces roughly ten lines across the whole town.

### Lines (i18n, per villager, reviewed by Jeff first)

Keys `dejavu.<npc>.1.<n>` / `dejavu.<npc>.2.<n>` in `i18n/default.json`, plus a neutral fallback pool
`dejavu.default.1.<n>` / `dejavu.default.2.<n>` used for any villager without a pool (mod-added NPCs,
or anyone Jeff leaves out). Lines are short, in each villager's voice, and never explain the loop.
`DejaVuLines.Pick(npc, tier, rng)` resolves the key list from the i18n map at load (keys enumerated
once, so adding a line is a JSON edit). The full draft is in
`2026-08-27-deja-vu-dialogue-lines.md`; nothing is coded until Jeff approves it.

### Injection (glue, one Harmony postfix)

`DejaVuDialoguePatch`: postfix on `NPC.checkForNewCurrentDialogue(int heartLevel, bool noPreface)`.
Runs only when `noPreface || __result` (the last call `checkAction` makes for this talk, so the
two-call pattern cannot double-inject), only when `CurrentDialogue.Count > 0` (a line is about to
play), and only when `Enabled` (mirrors `GameplayConfig.EnableDejaVuDialogue`, GMCM "Features",
default on) and `RunActivation.IsActive`. It asks `DejaVuRules.TryPick(...)`; on a hit it pushes
`new Dialogue(npc, "TLY.dejavu", text)` on the stack and stamps `RunState.DejaVuShownTo` and
`DejaVuLastDay`. The villager's own line follows on the next click.

### Reset

`FarmerReset` already clears `friendshipData`; `VillagerFamiliarity` is meta and survives.
`RunState` is rebuilt every loop, so `DejaVuShownTo`, `DejaVuLastDay` and `EventsSeenAtDayStart`
reset with it.

### Debug

`tly_dejavu status` prints every villager's familiarity and eligibility; `tly_dejavu set <npc> <n>`
sets a counter; `tly_dejavu force <npc>` makes the next talk with that villager inject a line (bypasses
chance and caps, not the Introduction guard); `tly_dejavu reset` clears the loop caps.

## Testing

Core: rollup arithmetic (talk, gifts, heart event, several days accumulate; unknown villager
creates the entry); eligibility table (loop 1 never; below threshold never; per-villager cap; weekly
cap; tier boundary at 3x); pick honours the chance via an injected RNG; line resolution falls back to
the default pool; i18n guard passes for every key.

Live (Rodger save): `tly_dejavu set Pierre 200`, `tly_reset` (loop count >= 1 already), `tly_dejavu
force Pierre`, walk to Pierre and talk: the tier-2 line plays, then his normal line. Then talk on
the Introduction day of a fresh loop with `force` set: the Introduction line must play and the
deja-vu line must not. `tly_dejavu status` shows the per-loop stamp.

## Docs

README + Nexus: a Features bullet ("The town half-remembers.") and Gribbleby in Credits; CHANGELOG
Unreleased. Identical content in both.
