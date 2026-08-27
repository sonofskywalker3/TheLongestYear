# Handoff: a bundle-difficulty modifier (the 11th knob)

**For a fresh agent with no prior context. Read this whole file before touching anything.**

Note on numbering: Jeff called this "a 10th knob", but there are already ten. This is the
**eleventh**. Say so once, then use the right number.

---

## 1. What Jeff asked for, in his words

> "I think maybe we need a 10th knob for bundle difficulty. some of the available bundles like the
> four seasons or the book bundles will be REALLY hard, and should be left off of normal."

So: a difficulty modifier that governs **which bundles are allowed to appear on the board at all**,
so the punishing ones are excluded at the easier steps and only show up as difficulty rises. This
is different from every existing modifier: the ten that exist change what a bundle *asks for* or
what you *carry between loops*. None of them changes *which bundles exist*.

**This is a design question before it is a coding question. Brainstorm it with Jeff first.** He
rules on design; do not pick the shape yourself. Concrete things he has not decided:

- Is it a whitelist by hardness rating, a hard cap on how many "hard" bundles a board may carry, or
  a per-room rule?
- Where does the hardness rating come from? It is a judgment call about obtainability, not
  something derivable from item price. Almost certainly a curated per-bundle table, which means
  someone has to rate roughly 60 bundles, and that someone is probably Jeff.
- What do Easy / Normal / Hard / Extreme each allow?
- Does the mod's own authored bundles participate, or only vanilla-pool ones?

## 2. The state of the branch

- Repo: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`
- Branch: **`feat/difficulty-modifiers`**. Not merged to `master`. **Never pushed** and must not be.
- Tests: **1038 passing, 0 failing.** `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
- Build: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`
- The mod is **not released** with any of this. Difficulty modifiers are new, unreleased work.

Read these first, in order:

1. `docs/superpowers/specs/2026-08-26-difficulty-modifiers-design.md` - the design the ten existing
   modifiers were built to, including Jeff's rulings and the reasoning behind them.
2. `docs/superpowers/plans/2026-08-26-difficulty-modifiers.md` - the implementation plan.
3. `STATUS.md` - current state, what is verified in game and what is not.
4. `docs/engine-bundle-catalogue.md` - **the most useful file for this task.** Every bundle the
   engine can produce, what each can ask for, and how quantities are decided. Regenerate it any
   time with the `tly_dumpbundles` console command.

## 3. How the existing difficulty system is built

Copy this pattern exactly; do not invent a parallel one.

- `DifficultyStep` - the enum `{ Easy, Normal, Hard, Extreme }`. There is no Off.
- `DifficultySettings` - the ten configured steps, serialized into `GameplayConfig.Difficulty`.
  Every one defaults to `Normal`, and **Normal must always resolve to today's shipping balance.**
- `DifficultyProfile` - the resolved concrete values.
- `DifficultyResolver.Resolve(settings, config)` - a pure function holding the ENTIRE balance
  table. This is the one place any number lives. Your new knob's numbers go here too.
- `MetaState.Difficulty` - the profile **stamped onto the save** when a loop begins. Consumers read
  the stamp, never live config, which is what makes a GMCM change apply at the NEXT loop.
- `MetaState.BoardDifficulty(config)` - the profile that produced the board **already on disk**.
  Board re-derivation must use this, not `EffectiveDifficulty`. Getting this wrong demotes healthy
  saves to a legacy path; it was a real bug, found in game and fixed.

The four ask-side modifiers are applied as pre-transforms on the generator's inputs rather than as
edits to generation logic (`DifficultyTuning` for quality, `RarityBias` for pools, `StackScaling`
and `RequiredSlots` for finished specs). Follow that instinct: prefer filtering the candidate pool
over teaching the generator about difficulty.

Non-negotiable constraints, all learned the hard way:

- **All-Normal must be byte-identical to today.** There is a test asserting the resolver reproduces
  every config value at Normal. Do not weaken it.
- **Never let a difficulty step create an impossible ask.** Quality eligibility vetting exists
  because of Nexus bug 1122358 (gold-star Fiber). Same discipline applies here.
- No em dashes anywhere. No `/sdcard/` paths.
- Do not bump `manifest.json`'s `Version` on this branch. The release line owns version bumps.
- Small, single-purpose commits. TDD.

## 4. Where boards are actually composed

`src/TheLongestYear/Loop/BundleEngine.cs`, method `Generate`:

1. `VanillaBundlePool.BuildRoomPools()` returns, per room, a list of POSITIONS, each holding a list
   of CANDIDATE bundles drawn from the game's `Data/Bundles` and `Data/RandomBundles`.
2. `WidenWithAuthoredBundles` appends the mod's own authored bundles (`AuthoredBundleCatalog.All`)
   as extra candidates to every position of their room.
3. `RemixSelector.PickForRoom` picks ONE candidate per position, seeded.
4. Themed picks get their slots re-rolled from item pools; unthemed picks keep vanilla's slots.

**Your filter almost certainly belongs between steps 2 and 3**, removing disallowed candidates
before the pick.

### The hazard that will bite you

**Every position must keep at least one eligible candidate.** Some positions have exactly one
candidate (check the catalogue: several show "1 possible bundle"). If your filter empties a
position, the board loses a bundle, and the write-key space changes. Read the class comment at the
top of `BundleEngine.cs` about why the key space must be identical across generations: a shrinking
room leaves stale bundles behind and can throw inside `Game1.AddLocations`, which surfaces to the
player as "Couldn't create the CommunityCenter location".

So the filter must be a **preference, not a guarantee**: drop hard candidates only while at least
one remains, and log when a position could not be eased. There is an existing precedent for exactly
this shape in `ItemHardness.Trim`, which takes a `minKeep` and refuses to go below it. Mirror it.

### It is Engine-only, and the UI must say so

A vanilla Normal or Remixed board is generated by the game itself; the mod does not choose its
bundles. So this modifier, like **Item rarity**, cannot do anything there.

Precedent to copy exactly: the item-rarity GMCM row says "(TLY Custom bundles only)" **in its
option name**, not just the tooltip, because a setting that silently does nothing is a bug report
waiting to happen. Do the same. Also check `DifficultySettings.AsksAllNormal()` - it deliberately
excludes `ItemRarity` so a modifier that cannot apply to a vanilla board does not drag the vanilla
post-pass into running. Your knob needs the same treatment.

## 5. Bundles Jeff named, and how to think about the rest

He called out **Four Seasons Sampler** and **the book bundles**. Do not stop at those two; use the
catalogue and the existing curated quota table as evidence of what is already known to be harsh.

`GameplayConfig.DefaultBundleQuotas` already carries a curated list with per-bundle reasoning in
comments, from a 2026-08-21 pass over exactly this problem (run-bricking and harsh bundles). Read
those comments before rating anything: Winter Star, Forager's, Gil's Trophies, Brewer's,
Preserver's, Mineral, Home Cook's Feast, Fish Farmer's, Artifact, Four Seasons Sampler, Rare Crops
and Garden all already have notes explaining why they are hard. That is most of your rating table
already argued out, and the design doc behind it is
`docs/superpowers/specs/2026-08-21-curated-quota-ramps-design.md`.

Note the tension worth raising with Jeff: those bundles were made fair by **easing their quotas**.
A new knob that instead **removes** them is a second, overlapping answer to the same problem. Ask
him how the two should relate before building.

## 6. Verifying it

Unit tests carry most of it, since candidate filtering is pure. In game:

- `tly_genbundles [seedLoop]` regenerates a board for diagnostics and prints each room's picks. It
  also runs a determinism self-check. Use the same seed loop under two different steps to compare.
- `tly_dumpbundles` regenerates the catalogue.
- `tly_difficulty` prints configured vs in-force steps and every resolved value.

**Driving the game (this cost a lot of time today, so read it):**

- Deploy with `pwsh -NoProfile -File tools/deploy.ps1`. It archives the log, kills the game, builds,
  and relaunches. A running game LOCKS the DLL, so a plain `dotnet build` fails to deploy.
- The live log is `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. The `SMAPI-latest.txt` in
  the repo root is a stale copy. Do not read that one.
- Send console commands with `tools/send-smapi-command.ps1`, which injects into SMAPI's console
  without stealing focus.
- **An unfocused Stardew is a PAUSED Stardew.** Commands queue and never run. `tools/game.ps1
  -Focus` aborts when Windows refuses the foreground. The workaround that does work is a real mouse
  click on the window; there is a working script at
  `<scratchpad>/focus-by-click.ps1` from today's session, and the technique is: find the window by
  title containing "Stardew Valley" AND class `SDL_app`, `SetCursorPos` over an empty part of it,
  then `mouse_event` down/up.
- **A TLY reset RENAMES the save folder** (the folder name embeds `uniqueIDForThisGame`, which the
  reset re-seeds). Re-read the folder list after any reset before loading.
- Do not try to open .md files on Jeff's desktop; it silently fails. See user memory
  `opening-md-files-marktext.md`.

## 7. Workflow Jeff expects

1. **Brainstorm with him first** (superpowers:brainstorming). This is creative work with real design
   choices; do not design it alone. Explain mechanics in plain terms before asking him to pick
   between options.
2. Write a spec to `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`, get his review.
3. Write a plan to `docs/superpowers/plans/`.
4. Build with TDD, small commits, no version bump on the branch.
5. Update `README.md` AND `docs/nexus-description.bbcode` in the same task. They must stay
   **content-identical**, differing only in markup. Verify by stripping markup from both and
   diffing the prose.
6. **Never push, never release.** Both need Jeff's explicit "yes, push".

## 8. Open items already on the board, so you do not trip over them

- The difficulty work is unreleased and **largely unplayed**. Eight of the ten existing modifiers
  have never run in game; only stack size and required slots have. Balance numbers are unvalidated.
- Open question Jeff has not ruled on: at Hard, every single-item ask becomes 2 (1 x 1.5 rounds away
  from zero), which makes Hard and Extreme identical for one-stack slots. Rounding down instead
  would separate them. Do not change it unilaterally.
- `STATUS.md` and `TODO.md` carry the rest, including a parked post-1.0 "Impossible mode" that also
  concerns which bundles may appear. Read that entry: your knob and Impossible mode overlap
  conceptually, and Jeff may want them designed as one family.
