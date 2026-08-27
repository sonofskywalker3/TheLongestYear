# Handoff: walk the TODO list with Jeff and pick the next piece of work

Copy this whole file as the prompt for a fresh agent.

---

You are working in `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear` (SMAPI mod
"The Longest Year", PC). Read `STATUS.md`, `TODO.md` and the workspace `.claude/CLAUDE.md` first.
Work on `master` (0.16.18 local and pushed, last public release 0.16.17). Do not push or release
anything; Jeff says "yes, push" himself. No em dashes in anything you write for Jeff or players.
Jeff is the designer, not a programmer: explain what a thing IS in plain words before asking him
to choose, and never ask him to run commands or write code.

## The job

This is a conversation, not a build. Go through `TODO.md`'s `## Open` section (1758 lines total;
the open items sit above the "(closed ...)" marker at line ~1459, the rest is history) with Jeff
and come out with ONE agreed next task, written down. Nothing in this session should touch code
unless Jeff explicitly changes his mind and says "build it".

Do this in order:

1. **Summarise the open list for him in one screen.** Group by state, newest first. As of
   2026-08-27 late the groups are roughly:
   - RULED by Jeff, NOT BUILT: keep wallet items + Stardrops via JP (line ~9); the Egg Hunt runs
     once per loop (~210).
   - BRAINSTORM NEEDED: additional weekly themes (~40).
   - BUILT, NOT RELEASED: keep power books 0.16.9 to 0.16.12 (~50) and deja-vu dialogue
     0.16.13 to 0.16.17 (both live-smoked, see STATUS.md); Shop Discount 0.14.2 (~121).
   - FOUND, NOT ACTED ON: SVE board audit (~26); Nexus bug 1123181 JP perk screen never opens on
     reset (~317, check whether a later release already covered it before presenting it as open);
     rose1729 no pet offer after declining Keep Pet (~409).
   - NEXT SESSION markers still standing: smoke the difficulty modifiers in game (~1648).
   - Long-tail: tech debt (three reset paths, ~1131), animated loop cutscene + real ending (~1318),
     trilogy architecture spec (~1306), mod page remixed-bundles note (~958), old sweeps.
   Verify each line number and state against the file before you show it; entries move.

2. **Flag anything stale or contradictory** you notice while reading (an item marked open that a
   later entry says shipped, a "NEXT SESSION" that already happened, duplicate items). Propose the
   edit, get a yes, make it. Keep this to a short list; do not rewrite the file.

3. **Ask Jeff what he wants next**, one question at a time. Give a recommendation first: the
   obvious candidates are (a) cut a release so the four built-but-unreleased features stop
   piling up (books, deja-vu, wallet/Stardrops if he wants it built first, Shop Discount), or
   (b) build one of the two RULED items, or (c) brainstorm the weekly themes. Say which you would
   pick and why in two sentences, then let him choose. If he picks a build, use the brainstorming
   skill before any spec, and stop after the spec is approved unless he says to keep going.

4. **Write the outcome down.** Update the chosen entry in `TODO.md` with a `▶ NEXT SESSION:`
   heading at the top of `## Open`, note the decision and date in `STATUS.md`'s top section, and
   if the next task is big enough, write a handoff file
   `docs/superpowers/HANDOFF-YYYY-MM-DD-<topic>.md` in the shape of this one. Commit the docs
   locally (`docs: ...`, no version bump for docs-only). Tell Jeff plainly what is committed and
   that nothing is pushed.

## Things that will trip you up

- `TODO.md` mixes open, done and historical entries; the heading emoji and the words RULED /
  BUILT / RELEASED / SMOKED carry the state. Read the first paragraph of each open entry, not
  just the heading, before you summarise it.
- Version bumps happen only on `master` and only for code changes; docs commits do not bump.
- Release mechanics (if he picks a release): `gh release create vX.Y.Z "<zip>"` uploads the file
  via the workflow; the Nexus version, description and changelog are driven in Jeff's own Chrome
  with the Claude-in-Chrome tools. README and the Nexus description must stay content-identical.
  See `docs/RELEASE_TOOLING.md` and the workspace CLAUDE.md before starting one, and get an
  explicit "yes, push" first.
