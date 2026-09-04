# Fish catch rates: what a level-10 player can actually land per day

Jeff, 2026-09-04, for the per-domain ask bands (TODO item E): "a little more math behind it".
Method: replicate the game's own fish pick in Python over the real data tables, then multiply by
a catch rate worked out from the rod's timing constants. NOT a measurement in the running game
(the forage ruling still stands for that; this is the modelled first pass Jeff asked for, and it
should be spot-checked in game before it is trusted for a ceiling).

## What the game does per cast (Stardew Valley.dll, 1.6, decompiled with ilspycmd)

`GameLocation.GetFishFromLocationData`: takes the `Default` rows (Qi Beans, secret note, the trash
row at precedence 2000) plus the location's `Data/Locations` fish rows, sorts by Precedence with a
random shuffle inside each precedence, and walks the list. Each row is filtered (fish area,
season, level, distance from shore, bobber/player rectangles, magic bait), then rolls its own
`Chance`, then its `Condition` query, then `CheckGenericFishRequirements` against `Data/Fish`:
time window, weather, min level, and the depth chance
`chance - max(0, maxDepth - waterDepth) * depthMult * chance + level/50`, capped at 0.9. The first
row that passes everything is the catch. Plain Bait runs the walk once; any better bait runs it
twice before falling through to trash.

`FishingRod.calculateTimeUntilFishingBite`: uniform random between 0.6 s and
`30 s - 0.25 s * FishingLevel - tackle`, x0.75 on the first cast after a cast, x0.5 with any bait,
x0.66 more with Deluxe Bait. Real seconds. `Game1.shouldTimePass` explicitly keeps the clock
running during the `BobberBar` minigame, so every second of the cycle costs game time. Ten game
minutes are 7 real seconds, so one game hour is 42 s.

## Catch rate: the hole in "40 a day"

Cycle for a level-10 player with plain Bait, no bubbles:

| Step | Real seconds |
|---|---|
| Wait for bite (mean of 0.6 to 27.5 s, halved by bait) | 7.0 |
| Nibble, hook | 0.8 |
| Bobber bar (progress 0.3 to 1.0 at 0.002 per frame = 5.8 s perfect; real play 7 to 15) | 9.0 |
| Pull-up animation, item popup, dismiss | 3.0 |
| Recast (charge and throw) | 2.0 |
| **Total** | **~22 s = 31 game minutes** |

So about **2 catches per game hour** with bait, about 1.5 without, about 2.3 with Deluxe Bait,
and 4 to 5 on a bubble spot while it lasts. 40 a day therefore means fishing every one of the 20
hours from 6am to 2am with no walking, eating, selling or sleeping earlier. It is a ceiling, not
a day. A dedicated fishing day that still allows for travel and a sale is nearer 10 hours on the
water, which is 20 catches, and the fish's own time window (Albacore is morning and night only,
Catfish rain only) eats into that further.

Roughly a quarter to a third of catches at a good spot are trash or algae at level 10 with bait.
The per-fish numbers below already have that baked in, since the simulation returns whatever the
walk actually lands on.

## Simulation

`tools/fish-sim/`: `dump-Program.cs` dumps Data/Locations fish rows, Data/Fish and object names
to JSON through the game's own reader DLLs (see user memory `stardew-xnb-data-dump`); `sim.py`
replays the pick 600 times per (place, season, weather, hour) for level 10, bait, water depth 5,
daily luck 0, and keeps the best place per fish per hour; `daily.py` / `table.py` turn that into
expected catches per day for a player who goes wherever that fish is best that hour.

Places: Town (both areas), Beach, Forest river and lake, Mountain, Sewer, the mine's data rows,
Standard farm, Desert (both ponds), Secret Woods, Railroad, Backwoods, Bus Stop.

**Caveats, read before using a number:**
- Mine floor fish (Stonefish, Ice Pip, Lava Eel) are NOT here: `MineShaft.getFish` hard-codes them
  by floor band outside the data tables. Ghostfish / algae numbers come from the generic
  `UndergroundMine` rows only.
- Legendaries are shown at their raw per-cast odds; the game's CatchLimit 1 and TLY's
  LegendaryFishRules make the count 1 regardless.
- Rain-only fish are weighted by a flat rain chance (Spring 18%, Summer 15%, Fall 18%, Winter 0).
  The "rainy day" column is the honest number for a player who waits for rain.
- Night Market, Trout Derby, SquidFest, Ginger Island, festival and Qi rows are excluded.
- Fish areas without a data row (Forest with no area id) draw the location-wide rows only.

## Expected catches per day, level 10, plain Bait, 2 catches per game hour

"20h" = fishing the entire 6am to 2am day. "Best 10h window" = the ten consecutive hours that
suit this fish, weighted by the season's rain chance. Multiply by the days you are willing to
grant a bundle to get a basis; Jeff's forage rule (80% of the basis is the hard ceiling,
20-50 / 50-80 bands) applies on top.

| Fish | Season | Best spot | Sunny day, 20h | Rainy day, 20h | Weighted day, 20h | Weighted day, best 10h window |
|---|---|---|---|---|---|---|
| Albacore | fall | Beach | 5.2 | 3.9 | 5.0 | 3.7 |
| Albacore | winter | Beach | 3.6 | 3.3 | 3.6 | 2.5 |
| Anchovy | spring | Beach | 6.0 | 5.3 | 5.9 | 3.2 |
| Anchovy | fall | Beach | 7.1 | 5.3 | 6.8 | 4.0 |
| Bream | spring | Forest/River | 5.4 | 3.8 | 5.1 | 5.1 |
| Bream | summer | Town | 5.0 | 4.5 | 4.9 | 4.9 |
| Bream | fall | Forest/River | 5.2 | 3.0 | 4.8 | 4.8 |
| Bream | winter | Town | 3.5 | 2.7 | 3.5 | 3.5 |
| Bullhead | spring | Mountain | 8.1 | 7.9 | 8.1 | 4.3 |
| Bullhead | summer | Backwoods | 6.8 | 7.4 | 6.9 | 4.0 |
| Bullhead | fall | Backwoods | 8.1 | 7.2 | 7.9 | 4.1 |
| Bullhead | winter | Mountain | 6.3 | 5.7 | 6.3 | 3.5 |
| Carp | spring | Woods | 18.4 | 15.3 | 17.8 | 9.0 |
| Carp | summer | Woods | 18.2 | 14.9 | 17.7 | 9.0 |
| Carp | fall | Woods | 18.2 | 15.2 | 17.7 | 8.9 |
| Carp | winter | Woods | 18.1 | 15.0 | 18.1 | 9.2 |
| Catfish | spring | Woods | 0.0 | 11.2 | 2.0 | 1.2 |
| Catfish | summer | Woods | 0.0 | 10.6 | 1.6 | 0.9 |
| Catfish | fall | Woods | 0.0 | 10.6 | 1.9 | 1.1 |
| Catfish | winter | Woods | 0.0 | 10.7 | 0.0 | 0.0 |
| Cave Jelly | spring | UndergroundMine | 1.9 | 1.8 | 1.9 | 1.0 |
| Cave Jelly | summer | UndergroundMine | 1.9 | 1.9 | 1.9 | 1.0 |
| Cave Jelly | fall | UndergroundMine | 1.9 | 1.8 | 1.9 | 1.0 |
| Cave Jelly | winter | UndergroundMine | 1.9 | 1.8 | 1.9 | 1.0 |
| Chub | spring | Forest/River | 13.2 | 11.6 | 12.9 | 6.5 |
| Chub | summer | Forest/River | 8.9 | 10.6 | 9.2 | 5.1 |
| Chub | fall | Forest/River | 12.2 | 9.0 | 11.6 | 6.0 |
| Chub | winter | Backwoods | 8.2 | 7.1 | 8.2 | 4.4 |
| Dorado | summer | Forest/River | 2.6 | 2.9 | 2.6 | 2.1 |
| Eel | spring | Beach | 0.0 | 4.6 | 0.8 | 0.8 |
| Eel | fall | Beach | 0.0 | 5.5 | 1.0 | 1.0 |
| Flounder | spring | Beach | 2.9 | 2.7 | 2.8 | 2.1 |
| Flounder | summer | Beach | 3.4 | 3.0 | 3.3 | 2.6 |
| Ghostfish | spring | UndergroundMine | 11.1 | 11.5 | 11.2 | 5.7 |
| Ghostfish | summer | UndergroundMine | 11.2 | 11.2 | 11.2 | 5.7 |
| Ghostfish | fall | UndergroundMine | 11.1 | 11.2 | 11.1 | 5.6 |
| Ghostfish | winter | UndergroundMine | 11.6 | 11.6 | 11.6 | 5.8 |
| Green Algae | spring | Forest | 19.0 | 19.3 | 19.0 | 9.6 |
| Green Algae | summer | Forest | 15.4 | 15.5 | 15.4 | 8.4 |
| Green Algae | fall | Forest | 19.4 | 16.7 | 18.9 | 9.5 |
| Green Algae | winter | UndergroundMine | 13.5 | 13.6 | 13.5 | 7.9 |
| Halibut | spring | Beach | 5.4 | 4.4 | 5.2 | 3.5 |
| Halibut | summer | Beach | 7.1 | 6.8 | 7.1 | 5.2 |
| Halibut | winter | Beach | 4.2 | 4.0 | 4.2 | 3.0 |
| Herring | spring | Beach | 9.4 | 8.1 | 9.2 | 5.0 |
| Herring | winter | Beach | 7.4 | 7.2 | 7.4 | 4.3 |
| Largemouth Bass | spring | Backwoods | 5.4 | 5.1 | 5.4 | 4.2 |
| Largemouth Bass | summer | Backwoods | 3.9 | 4.7 | 4.0 | 3.1 |
| Largemouth Bass | fall | Mountain | 5.4 | 4.9 | 5.3 | 4.1 |
| Largemouth Bass | winter | Mountain | 4.0 | 3.9 | 4.0 | 3.1 |
| Legend | spring | Backwoods | 0.0 | 1.6 | 0.3 | 0.2 |
| Lingcod | winter | Town | 7.0 | 5.5 | 7.0 | 3.6 |
| Midnight Carp | fall | Forest/Lake | 2.1 | 1.6 | 2.0 | 2.0 |
| Midnight Carp | winter | Forest/Lake | 1.7 | 1.3 | 1.7 | 1.7 |
| Mutant Carp | spring | Sewer | 0.8 | 0.8 | 0.8 | 0.4 |
| Mutant Carp | summer | Sewer | 0.8 | 0.9 | 0.8 | 0.4 |
| Mutant Carp | fall | Sewer | 0.8 | 0.8 | 0.8 | 0.4 |
| Mutant Carp | winter | Sewer | 0.8 | 0.7 | 0.8 | 0.4 |
| Octopus | summer | Beach | 1.2 | 1.0 | 1.2 | 1.2 |
| Perch | winter | Forest | 14.0 | 11.9 | 14.0 | 7.1 |
| Pike | summer | Forest/Lake | 17.9 | 17.7 | 17.9 | 9.0 |
| Pike | winter | Forest/Lake | 12.8 | 10.8 | 12.8 | 6.4 |
| Pufferfish | summer | Beach | 1.5 | 0.0 | 1.3 | 1.3 |
| Rainbow Trout | summer | Town | 5.7 | 0.0 | 4.9 | 3.8 |
| Red Mullet | summer | Beach | 5.7 | 4.9 | 5.6 | 4.5 |
| Red Mullet | winter | Beach | 3.9 | 3.3 | 3.9 | 3.1 |
| Red Snapper | summer | Beach | 0.0 | 5.1 | 0.8 | 0.6 |
| Red Snapper | fall | Beach | 0.0 | 4.5 | 0.8 | 0.6 |
| Red Snapper | winter | Beach | 0.0 | 3.7 | 0.0 | 0.0 |
| River Jelly | spring | Desert | 4.1 | 4.1 | 4.1 | 2.1 |
| River Jelly | summer | Desert | 4.2 | 4.2 | 4.2 | 2.1 |
| River Jelly | fall | Desert | 4.0 | 4.1 | 4.0 | 2.1 |
| River Jelly | winter | Desert | 4.2 | 4.2 | 4.2 | 2.1 |
| Salmon | fall | Forest/River | 6.9 | 4.7 | 6.5 | 5.1 |
| Sandfish | spring | Desert/TopPond | 14.6 | 14.3 | 14.6 | 10.4 |
| Sandfish | summer | Desert/TopPond | 14.3 | 14.5 | 14.4 | 10.3 |
| Sandfish | fall | Desert/TopPond | 14.3 | 14.7 | 14.4 | 10.3 |
| Sandfish | winter | Desert/TopPond | 14.4 | 14.4 | 14.4 | 10.2 |
| Sardine | spring | Beach | 7.6 | 7.3 | 7.6 | 6.0 |
| Sardine | fall | Beach | 8.2 | 6.4 | 7.9 | 6.3 |
| Sardine | winter | Beach | 6.2 | 5.1 | 6.2 | 5.0 |
| Scorpion Carp | spring | Desert/TopPond | 4.2 | 4.4 | 4.2 | 3.0 |
| Scorpion Carp | summer | Desert/TopPond | 4.5 | 4.3 | 4.5 | 3.2 |
| Scorpion Carp | fall | Desert/TopPond | 4.5 | 4.3 | 4.4 | 3.2 |
| Scorpion Carp | winter | Desert/TopPond | 4.3 | 4.3 | 4.3 | 3.2 |
| Sea Cucumber | fall | Beach | 3.8 | 2.9 | 3.7 | 2.9 |
| Sea Cucumber | winter | Beach | 2.7 | 2.4 | 2.7 | 2.2 |
| Sea Jelly | spring | Beach | 1.2 | 1.1 | 1.2 | 0.6 |
| Sea Jelly | summer | Beach | 1.4 | 1.4 | 1.4 | 0.9 |
| Sea Jelly | fall | Beach | 1.4 | 1.0 | 1.3 | 0.7 |
| Sea Jelly | winter | Beach | 1.1 | 1.0 | 1.1 | 0.6 |
| Seaweed | spring | Beach | 6.7 | 6.3 | 6.7 | 3.7 |
| Seaweed | summer | Beach | 8.5 | 8.0 | 8.4 | 5.3 |
| Seaweed | fall | Beach | 7.8 | 6.0 | 7.5 | 4.4 |
| Seaweed | winter | Beach | 5.5 | 5.0 | 5.5 | 3.1 |
| Shad | spring | Town | 0.0 | 7.3 | 1.3 | 0.8 |
| Shad | summer | Town | 0.0 | 8.8 | 1.3 | 0.9 |
| Shad | fall | Town | 0.0 | 5.2 | 0.9 | 0.6 |
| Smallmouth Bass | spring | Forest/Lake | 18.7 | 18.7 | 18.7 | 9.4 |
| Smallmouth Bass | fall | Forest/Lake | 17.8 | 14.9 | 17.3 | 9.1 |
| Squid | winter | Beach | 2.8 | 2.8 | 2.8 | 2.8 |
| Sturgeon | summer | Mountain | 3.7 | 4.2 | 3.8 | 2.9 |
| Sturgeon | winter | Backwoods | 3.8 | 3.4 | 3.8 | 2.9 |
| Sunfish | spring | Forest/River | 8.8 | 0.0 | 7.2 | 5.6 |
| Sunfish | summer | Town | 7.0 | 0.0 | 5.9 | 4.6 |
| Super Cucumber | summer | Beach | 2.6 | 2.4 | 2.6 | 2.6 |
| Super Cucumber | fall | Beach | 2.1 | 1.6 | 2.0 | 2.0 |
| Tiger Trout | fall | Forest/River | 4.5 | 2.9 | 4.3 | 3.3 |
| Tiger Trout | winter | Town | 3.6 | 2.6 | 3.6 | 2.9 |
| Tilapia | summer | Beach | 3.0 | 2.6 | 3.0 | 3.0 |
| Tilapia | fall | Beach | 2.9 | 2.4 | 2.8 | 2.8 |
| Tuna | summer | Beach | 3.1 | 2.8 | 3.0 | 2.4 |
| Tuna | winter | Beach | 2.2 | 2.0 | 2.2 | 1.8 |
| Walleye | fall | Forest | 0.0 | 11.8 | 2.1 | 1.6 |
| Walleye | winter | Forest | 0.0 | 6.8 | 0.0 | 0.0 |
| White Algae | spring | UndergroundMine | 11.3 | 11.4 | 11.3 | 5.8 |
| White Algae | summer | UndergroundMine | 11.4 | 11.3 | 11.4 | 5.7 |
| White Algae | fall | UndergroundMine | 11.5 | 11.4 | 11.5 | 5.8 |
| White Algae | winter | UndergroundMine | 11.4 | 11.3 | 11.4 | 5.9 |
| Woodskip | spring | Woods | 9.3 | 7.2 | 8.9 | 4.6 |
| Woodskip | summer | Woods | 9.4 | 7.3 | 9.1 | 4.6 |
| Woodskip | fall | Woods | 9.5 | 7.3 | 9.1 | 4.6 |
| Woodskip | winter | Woods | 9.6 | 7.5 | 9.6 | 4.9 |
