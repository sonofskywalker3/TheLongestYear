# Handoff: keep wallet items and Stardrops across a reset (per-item JP keeps)

Copy this whole file as the prompt for a fresh agent.

---

You are working in `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear` (SMAPI mod
"The Longest Year", PC). Read `STATUS.md`, `TODO.md` and the workspace `.claude/CLAUDE.md` first.
Work on `master` (0.16.18 local, last public release 0.16.17). Do not push or release anything;
Jeff says "yes, push" himself. No em dashes in anything you write for Jeff or players. Jeff is the
designer, not a programmer: explain what a thing IS in plain words before asking him to choose,
and never ask him to run commands or write code. Bump the patch version on every code commit.

## The job

Jeff chose this on 2026-08-27 late as the next build. Ruling already given: **wallet items and
Stardrops are keepable, bought with JP at the Junimo Shrine, per item**, the same shape as the
keep-power-books feature that shipped in 0.16.17. Order of work:

1. ~~Brainstorm~~ DONE 2026-08-27 late with Jeff.
2. ~~Spec~~ WRITTEN and approved in brainstorm:
   `docs/superpowers/specs/2026-08-27-keep-wallet-stardrops-design.md`. Read it first; it
   supersedes the design notes below (which are kept for the reasoning).
3. **Plan** (superpowers:writing-plans) from the spec, then build only if Jeff says to keep going.

## What already exists (reuse it)

- Book keeps: `UpgradeCatalogGenerators.CarryoverBookKeeps()` builds nineteen
  `UpgradeCategory.Carryover` rows with `runReachRequirement: "book:<Book_Id>"` so a row only shows
  once the book was read this loop; `KeepShopFilter.IsBuyable` hides the rest. Prices in three bands
  (150 / 350 / 500 to 750 JP). `RunBaseline.KeptBookStats` carries the bought flags across the wipe
  and `FarmerReset` re-sets them (FarmerReset.cs ~line 132). `RunReachEvaluator` resolves the
  `book:` token; add the new tokens there.
- `FarmerReset` clears `mailReceived` outright (line ~151) and resets `maxStamina` to 270
  (line ~224) precisely so Stardrops do not stack. Any keep has to re-add after those two lines.

## The state to keep (verified in the decompile, Farmer.cs 1278 to 1400)

Wallet items are all `mailReceived` flags behind Farmer getters:
`HasRustyKey`, `HasSkullKey`, `HasClubCard`, `HasSpecialCharm`, `HasDarkTalisman`, `HasMagicInk`,
`HasDwarvishTranslationGuide`, `HasTownKey`, `HasUnlockedSkullDoor` (the Skull Cavern door state,
separate from the key). Bear's Knowledge and Spring Onion Mastery come from `Data/Powers`; check
their `UnlockedCondition` mail names in the game data before naming rows (the xnb is at
`decompiler/stardew-valley-android/content/assets/Content/Data/Powers.xnb`; unpack or read the
PC `Data/Powers` via SMAPI).

Stardrops: eating one adds +34 to `maxStamina`. The availability mails are `CF_Fair`, `CF_Fish`,
`CF_Mines`, `CF_Sewer`, `CF_Spouse`, `CF_Statue` (six); the seventh (museum) is an item reward
with no CF_ mail, so "which Stardrops were eaten" is not directly recoverable from mail. The
honest signal is `maxStamina` itself: `(maxStamina - 270) / 34` eaten this loop.

## Open design points for the brainstorm

- **Pricing bands.** A Skull Key or a Stardrop is worth more than Bear's Knowledge. Propose three
  bands like the books and let Jeff move items between them.
- **Stardrops: one row each, or a tiered chain?** The mail cannot tell which one was eaten, so a
  chain ("Keep 1 Stardrop" ... "Keep 7 Stardrops", each reach-gated on stamina >= 270 + 34n and
  chained on the previous) fits the data; per-source rows would need a new tracker.
- **Skull Key vs Skull Cavern door.** Keeping the key without `HasUnlockedSkullDoor` means the
  player walks to the door once; keeping both skips that. Decide whether the door rides on the key.
- **Rusty Key + Dark Talisman + Magic Ink** are a story chain (sewers -> witch's hut -> Wizard's
  buildings). Chain them with `prerequisiteId` or leave independent?
- **Town Key** (Qi's Walnut Room) and **Club Card**: does keeping them across loops trivialise
  anything the loop is meant to take away? Jeff's earlier framing: bought keeps compound, so price
  them rather than block them.
- **Where the reach shows.** Rows should appear on a Fail night only once the item was obtained
  this loop, exactly like the books.

## Things that will trip you up

- `mailReceived.Clear()` runs before any re-grant; ordering inside `FarmerReset` matters.
- `HasRustyKey`, `HasSkullKey`, `HasDwarvishTranslationGuide` read from `Game1.MasterPlayer`, not
  the local player (multiplayer plumbing; single-player they are the same Farmer).
- The netWorldState audit (0.14.8) ruled a lot of shared state; do not undo any of it.
- README and `docs/nexus-description.bbcode` must stay content-identical when the feature is
  documented; CHANGELOG `## Unreleased` gets the entry.
