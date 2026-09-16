# Repeatable chance: proposal for Jeff (no model change yet)

> **Superseded in part (2026-09-16).** Bone Fragment is no longer promoted through Lava Lurk: only
> drops from monsters placed on a mine floor count as repeatable (commit 181ef6c). And mine items now
> land after the days it takes to reach their floor (the mine-depth ruling), so not every promoted
> item lands in week 1.

Your ruling: "a chance route the player can retry many times a day with a decent chance each try
counts as dependable." This note is the threshold and the item list to sign off on before Task 2
touches any code. Nothing in the model changed for this note; it's checked against the decompile
and against the obtainability comparison report generated today (`obtain-compare-2026-09-16.md`,
mod version 0.18.11).

## The threshold

A chance family gets promoted to dependable when a normal day's worth of tries makes it very
likely (roughly 90%+) that at least one try lands. In practice that comes out to two shapes:

- **Many tries (hundreds) at a low chance each** - mine stones. A day of mining breaking hundreds
  of stones is a working assumption, not a figure pulled from the code, but it's the right order
  of magnitude for a normal play session, and even a fraction of a percent per stone adds up.
- **Dozens of tries at a real chance each** - monster kills and fishing casts. "Dozens of kills"
  and "about 30 casts" are the same kind of working assumption. Here the per-try number has to be
  a meaningful slice, not a lottery ticket.

Diamond nodes are the one exception in the starting proposal that turns out to hold up: the
chance per stone is far under 1% even at the deepest floors, so hundreds of stones a day still
isn't good enough odds.

| Family | Where in the PC 1.6 decompile | Tries a day | Chance per try | Chance of at least one that day | Verdict |
|---|---|---|---|---|---|
| Mine node rows in `MineSources.NodeTable` marked Chance: coal, amethyst, topaz, jade, aquamarine, ruby, emerald, geode, frozen geode, magma geode, omni geode | `MineShaft.cs` 3642 (geode/frozen geode/magma geode, 2.2% per stone), 3656 (omni geode, 0.5% per stone), 3665+3669 (coal, 5% ore roll x 25% coal-within-that = ~1.25% per stone), 4612 (gem nodes, ~0.3% per stone, base `gemStoneChance = 0.003` set at 1307) | hundreds of stones (working assumption for a mining day, not a figure from the code) | 0.3% to 2.2% per stone (coal ~1.25%) | roughly 45% to over 99%, depending on the gem | **PROMOTE** |
| Diamond node | `MineShaft.cs` 4607: `0.00025 + mineLevel/120000.0 + 0.0005 * chanceModifier / 2.0`, floor 51+ only. At baseline luck/mining (`chanceModifier` 0), that's `0.00025 + mineLevel/120000` | hundreds of stones (same working assumption), very rare node | about 0.0675% at floor 51, up to about 0.125% by floor 120 | well under 1% even with 200 stones (roughly 13% to 22%) | **KEEP** - stays a lottery ticket even with hundreds of tries |
| Monster drops (`MineSources.MonsterDrops`), from `Data/Monsters` field 6 | data-driven, not code; the model reads `row.Chance` directly (`GameObtainabilityData.cs:151`) | dozens of kills (working assumption) | varies by monster/item; promote at `chance >= 0.25` | at 0.25 chance and 20 kills, about 99.7% | **PROMOTE when `row.Chance >= 0.25`** |
| Fishing trash (`SpawnSources.FishingTrash`) | `GameLocation.cs` 13685 (driftwood is the universal fallback when no fish spawn resolves anywhere, mines included) and `MineShaft.cs` 1188-1197 (only the lava lake mine area, `getMineArea() == 80`, rolls trash directly - about 95% trash after a 5%+luck Cave Jelly chance; every other mine area falls through to `base.getFish(...)`, the ordinary location fish table, same as surface fishing); confirmed in game by the `fish-catch-rates-2026-09-04.md` simulation, which measured roughly a quarter to a third of casts landing trash for a level-10 angler with bait | about 30 casts on a fishing day (working assumption, not a figure from the code; measured: closer to 10-20 realistic catches once bite time and the bobber minigame are counted, see the same note) | roughly 15-33% per cast on ordinary water, and about 95% while fishing the lava lake (mine area 80) specifically | at even the low end (15%, 15 casts), about 91% | **PROMOTE** |
| Garbage cans, artifact spots, fish ponds, cart, fishing treasure, geode contents, festival chance rewards | various | at most a handful (garbage cans: 1/day per can and there are 7; cart is a fixed daily roster; a geode is opened once per geode item you have, not a repeatable "try") | varies, several are actually high (e.g. a Town artifact spot can be 100% per dig) | not applicable - the limit is tries, not odds | **KEEP** - confirmed against the report; see also two more families below that the brief's starting table didn't list |

## Two chance families the brief's table doesn't cover

**`Fish` (an ordinary species catch, not trash).** This is a big family (11 LuckOnly/NewLater rows
in today's report use it, and it underlies most of the game's fish list). It doesn't fit a single
family-wide rule the way monster drops or mine nodes do: per-cast odds and realistic catches per
day vary hugely by fish. `fish-catch-rates-2026-09-04.md` measured actual expected catches/day for
a level-10 angler - Carp and Green Algae come out around 17-19/day (easily "many tries, decent
odds"), while Legend or Mutant Carp are under 1/day even on a dedicated fishing day (a lottery
ticket, same shape as the diamond node). A blanket PROMOTE would be wrong for the low end and a
blanket KEEP would be wrong for the high end. Recommend leaving `Fish` out of this repeatable-
chance promotion and reviewing it separately, fish by fish, using that note's numbers.

**`Machine` (111 Chance rows in today's report, mostly the crystalarium and the recycling
machine).** This family also doesn't fit one rule: a crystalarium loaded with an unbanned mineral
produces on its own schedule (about 3/day) and a recycling machine needs trash fed into it one
piece at a time, so "tries a day" depends on what the player is doing anyway, not a die roll on a
fixed action like a stone or a cast. Recommend leaving `Machine` out of this pass too.

Neither family is included in the item list below; both need their own look before touching them.

## The item list

Every item is a `(O)` object id from `Data/Objects` unless noted. "Current" is what
`obtain-compare-2026-09-16.md` already shows (`none` means the item is LuckOnly today - no
dependable route at all). "Proposed" is the week that family's own source would land under the
model once it's flagged Dependable instead of Chance - the model is "blind" to location/skill
gating when it lands a Chance source (same DayTable either way), so promoting a source doesn't
move its landing day, only which table it counts toward. All fourteen promoted LuckOnly items and
the one promoted NewLater item land week 1, because none of their promoted routes carry a season
or skill gate beyond reaching floor 1 (always true from day 1).

### LuckOnly items that get a first dependable route (14)

| Item | Current dependable week | Proposed dependable week | Promoted by |
|---|---|---|---|
| (O)60 Emerald | none | 1 | MineNode (gem node, mines floor 80) |
| (O)62 Aquamarine | none | 1 | MineNode (gem node, mines floor 40) |
| (O)64 Ruby | none | 1 | MineNode (gem node, mines floor 80) |
| (O)66 Amethyst | none | 1 | MineNode (gem node, mines floor 1) |
| (O)68 Topaz | none | 1 | MineNode (gem node, mines floor 1) |
| (O)70 Jade | none | 1 | MineNode (gem node, mines floor 40) |
| (O)168 Trash | none | 1 | Trash (fishing) |
| (O)169 Driftwood | none | 1 | Trash (fishing) |
| (O)170 Broken Glasses | none | 1 | Trash (fishing) |
| (O)171 Broken CD | none | 1 | Trash (fishing) |
| (O)172 Soggy Newspaper | none | 1 | Trash (fishing) |
| (O)535 Geode | none | 1 | MineNode (mines floor 1) and MonsterDrop (Duggy, chance 0.25) |
| (O)537 Magma Geode | none | 1 | MineNode (mines floor 80) |
| (O)881 Bone Fragment | none | 1 | MonsterDrop (Skeleton chance 0.5/0.4; Lava Lurk chance 0.5/0.4) |

Note: (O)749 Omni Geode and (O)536 Frozen Geode use the same mine-node mechanic but weren't in
today's LuckOnly list - they already have a dependable route from something else, so promoting the
mine node doesn't change their outcome.

### NewLater item whose dependable week moves earlier (1)

| Item | Current dependable week | Proposed dependable week | Promoted by |
|---|---|---|---|
| (O)684 Bug Meat | 3 (Desert Festival Vincent barter) | 1 | MonsterDrop (Grub chance 0.6, Fly chance 0.9, Bug chance 0.76, all mines floor 1/15) |

### Agree section: not enumerated here

The Agree section of today's report (146 items) only lists item/name/week, not the per-source
detail the other sections show, so I can't tell from the report alone which of those already-
matching items also carry a promotable Chance route underneath. Promoting a source can only pull a
week earlier or leave it the same (the model takes the earliest Dependable route), never push it
later, so nothing in Agree gets worse from this change. If you want the exact list of Agree items
that move earlier, that needs a re-run of the model with the promotion applied (a scratch test,
not committed) - happy to do that as part of Task 2 once you've said yes to the threshold above,
rather than block this note on it.

## Bottom line for the yes/no

15 items move: 14 LuckOnly items get a dependable week (all week 1) and 1 NewLater item
((O)684 Bug Meat) moves from week 3 to week 1. The diamond node stays a lottery ticket. `Fish` and
`Machine` are flagged as families that need their own pass rather than folding into this one.

Task 2 (the actual model change) waits for your yes on this threshold and list.
