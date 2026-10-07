# Follow-up: replacements new to the board; counted asks read naturally (2026-10-07)

Status: DONE. Branch `story`. Code commit `bf7f4a4`; this report is the next commit. Manifest version untouched
(0.18.15). No live run.

## 1. A tamper replacement skips anything on the board

`TamperRule.PickReplacement` (src/TheLongestYear.Core/Sabotage/SabotageRules.cs) takes a new optional
`board` (the requirements list). Every slot of every bundle in it adds its id, normalised (`192` =
`(O)192`), to a set; candidates whose id is in the set are dropped before the theme / effort / five-closest
steps. Filled or open does not matter (the slot list is the ask, not the ledger). Flavour is ignored on
purpose: the replacement is written unflavoured, so a Dried Apples slot on the board bars every Dried
Fruit. The tainted bar is unchanged and applies alongside. `SabotageService.PlanTamper` passes the same
`requirements` it builds targets from (`BoardRequirements.Build`, so the Vault and non-themed rooms are
not in it, as with the target rule; they never ask for items). An empty pool takes the existing
no-fair-replacement path; its log line now says why ("a replacement must be new to the board and never a
tainted item").

Tests: `tests/TheLongestYear.Tests/TamperNewToBoardTests.cs` (6 tests, written first; they failed to
compile: no six-argument `PickReplacement`). 80 seeds each: an open slot elsewhere (and a bare-id
spelling) keeps Beer and Potato out; a filled slot keeps Wine out; a Dried Fruit slot keeps Dried Fruit
out; board plus tainted bar together leave only the free item; a pool of only board items gives null
(and the same pool without the board gives a pick). A 300-seed random-board test asserts the pick is never
an id asked anywhere.

## 2. Counted asks

New `AskPhrases` (src/TheLongestYear.Core/Sabotage/AskPhrases.cs). `Ask(count, itemId, name, gamePlural)`:
count 1 (or less) is the bare name; otherwise, by base id:
1. a container: `"{count} {container} of {name}"`, the full display name, so a flavoured good reads
   "3 bottles of Blueberry Wine" / "3 jars of Pickled Beets";
2. the same word in the plural (bulk, or already plural): `"{count} {name}"`;
3. countable despite a mass-noun last word (the three jellyfish): the game's plural ("3 Sea Jellies");
4. anything else (including modded ids) falls back to `ItemPlurals.Plural`, the old name rules.

`ItemPlurals.Ask` is gone (its theory moved to `AskPhrases`); `ItemPlurals.CountedPlural` is the game
plural without the mass-noun check. `ItemPlurals.Plural` and `Tainted` are unchanged, so the {{old}} line
("all the Wild Honey", "all the Dried Apples", "all the Sea Jelly") is exactly as before. `AskIsPlural`
is unchanged (count > 1), so every container phrase takes `tamper-3-plural` ("They remain pure.").

**Dried / Smoked names.** A tamper writes its replacement unflavoured, and the game's own name for an
unflavoured Dried Fruit, Dried Mushrooms or Smoked Fish is the bare word "Dried" / "Smoked" (the same
Nexus bug 1137151 the bundle menu label fixes). The ask would have read "Bring us 3 Drieds instead". It
now uses new i18n names (`item-name.dried-fruit` "Dried Fruit", `item-name.dried-mushrooms`,
`item-name.smoked-fish`), looked up through `FlavorlessBundleSlots.AskNameKeyFor`; the i18n orphan guard
walks `AllAskNameKeys`. No designer line changed; only the {{new}} token's value.

Note on the replacement's name: a replacement is unflavoured, so the ask today says "3 jars of Honey",
"3 bottles of Wine", "3 jars of Pickles" (the game's base display names; an unflavoured Honey object
reads "Honey", not "Wild Honey", per `Object.loadDisplayName`). The flavoured forms in the designer's
examples are covered by the tests and would read right if a replacement ever carried a flavour.

Tests: `tests/TheLongestYear.Tests/AskPhrasesTests.cs` (79 cases), written first (failed to compile: no
`AskPhrases`, `AskNameKeyFor`). The game pluralizer in tests is `ItemPluralsTests.VanillaPlural` (now
`internal`), the 1.6 `makePlural` copy.

## How I judged containers

Names, categories and sprite indexes come from the PC 1.6 `Data/Objects` (dumped with a scratch .NET app
on the game's `ContentManager`, as in the project notes). Sprites come from the PC `Maps/springobjects`
and `TileSheets/Objects_2` textures: I decompressed the XNBs with MonoGame's own `LzxDecoderStream`
(reflection, scratch app), cropped each 16x16 sprite, scaled it 6x to 14x and looked at it. (The Android
copy of springobjects has a different layout and gave wrong crops; not used.) The game itself already
says "bowls of Rice Pudding" and "bowls of Algae Soup" in `Lexicon.makePlural`, which set the pattern for
soups. Rows marked **D** are the designer's confirmed calls; **S** are my sprite calls for him to check.

## Final table (count 3)

| Item (id) | Ask | Basis |
| --- | --- | --- |
| Beer (346) | 3 mugs of Beer | D (sprite: beer mug) |
| Pale Ale (303) | 3 glasses of Pale Ale | D (pilsner glass) |
| Coffee (395) | 3 cups of Coffee | D (cup) |
| Caviar (445) | 3 tins of Caviar | D (tin) |
| Roe (812), e.g. Salmon Roe | 3 clusters of Salmon Roe | D (loose eggs) |
| Aged Roe (447) | 3 jars of Aged Salmon Roe | D (jar) |
| Wine (348) | 3 bottles of Blueberry Wine / Wine | D (bottle) |
| Honey (340) | 3 jars of Wild Honey / Honey | D (jar) |
| Milk, Large Milk, Goat Milk, L. Goat Milk (184, 186, 436, 438) | 3 bottles of Milk (etc.) | D |
| Maple Syrup (724) | 3 bottles of Maple Syrup | D (bottle) |
| Oil (247), Truffle Oil (432) | 3 bottles of Oil / Truffle Oil | D (bottle) |
| Sea Jelly, River Jelly, Cave Jelly | 3 Sea Jellies / River Jellies / Cave Jellies | D (Sea Jelly); same fish kind |
| Juice (350) | 3 bottles of Apple Juice / Juice | S: bottle |
| Mead (459) | 3 jugs of Mead | S: a jug with a handle and spout, not a bottle (the first brief said bottles; please check) |
| Green Tea (614) | 3 cups of Green Tea | S: teacup (the first brief said bottles; the sprite is a cup) |
| Jelly (344) | 3 jars of Blueberry Jelly / Jelly | S: jar |
| Pickles (342) | 3 jars of Pickled Beets / Pickles | S: jar |
| Mayonnaise, Duck, Void, Dinosaur (306, 307, 308, 807) | 3 jars of Mayonnaise (etc.) | S: jars |
| Vinegar (419) | 3 bottles of Vinegar | S: bottle |
| Oak Resin (725) | 3 bottles of Oak Resin | S: corked flask |
| Pine Tar (726) | 3 jars of Pine Tar | S: squat lidded pot |
| Mystic Syrup | 3 bottles of Mystic Syrup | S: potion flask |
| Squid Ink (814) | 3 bottles of Squid Ink | S: ink bottle |
| Ginger Ale (903) | 3 bottles of Ginger Ale | S: bottle |
| Life Elixir (773) | 3 bottles of Life Elixir | S: potion flask |
| Oil of Garlic (772) | 3 bottles of Oil of Garlic | S: flask |
| Triple Shot Espresso (253) | 3 cups of Triple Shot Espresso | S: cup |
| Joja Cola (167) | 3 cans of Joja Cola | S: can |
| Dried Fruit | 3 jars of Dried Fruit / Dried Apples | S: jar |
| Dried Mushrooms | 3 bags of Dried Mushrooms | S: tied pouch |
| Raisins | 3 boxes of Raisins | S: box (game plural would be "Raisinses") |
| Sugar (245), Wheat Flour (246), Rice (423) | 3 bags of Sugar / Wheat Flour / Rice | S: bags; not liquids, but they have a container (see concerns) |
| Bread (216) | 3 loaves of Bread | S: a loaf (game: "Breads") |
| Soups: Parsnip, Tom Kha, Trout, Pumpkin, Algae, Moss Soup; Pale Broth, Chowder, Lobster Bisque, Fish Stew, Bean Hotpot | 3 bowls of Pumpkin Soup (etc.) | S: bowls; matches the game's "bowls of Algae Soup" |
| Rice Pudding (232) | 3 bowls of Rice Pudding | the game's own phrase |
| Cranberry Sauce (238), Artichoke Dip (605) | 3 bowls of ... | S: bowls |
| Stuffing, Coleslaw, Poi, Tropical Curry, Fiddlehead Risotto, Squid Ink Ravioli | 3 bowls of ... | S: bowls; mass-noun dishes ("Coleslaws", "Curries" read wrong) |
| Spaghetti, Stir Fry, Mango Sticky Rice, Fried Calamari, Eggplant Parmesan, Sashimi, Escargot | 3 plates of ... | S: plates; mass-noun dishes |
| Clay (330) | 3 Clay | bare (the game leaves it) |
| Coal (382) | 3 lumps of Coal | the game's own phrase |
| Hay, Wool, Cloth, Fiber, Sap, Moss, Seaweed, Green Algae, White Algae, Bug Meat, Slime, Wood, Hardwood | 3 Hay (etc.) | bare bulk, no container in the sprite (Cloth's sprite is a bolt; "bolts of Cloth" is possible) |
| Copper, Iron, Gold, Iridium, Radioactive Ore; Refined Quartz | 3 Copper Ore (etc.) | bare bulk (game: "Ores", "Quartzes") |
| Smoked Fish (any flavour), Baked Fish, Dish O' The Sea | 3 Smoked Fish (etc.) | same word in the plural (game: "Fishes", "Seas") |
| Cookies (223) | 3 Cookies | already plural (game: "Cookieses") |
| Everything else (crops, fish, forage, minerals, countable dishes, puddings, Piña Colada, Ice Cream, Cheese) | 3 Parsnips, 3 Piña Coladas, 3 Ice Creams | the game's plural, as before |

## Specs

- `docs/superpowers/specs/2026-09-15-darkness-obtainability-wiring-design.md`, Tampering: the replacement
  pool drops every id on the board.
- `docs/superpowers/specs/2026-09-21-darkness-agents-and-gate-scenes-design.md`, Junimo scene: the
  replacement is new to the board; the {{new}} rules with examples, pointing at `AskPhrases`.

## Verification

```
dotnet build TheLongestYear.sln                     -> Build succeeded.
dotnet test tests/TheLongestYear.Tests --no-build   -> Passed!  Failed: 0, Passed: 3997, Skipped: 0, Total: 3997
```

## Concerns

1. Sprite calls for the designer: Mead as jugs and Green Tea as cups go against the first brief's
   "bottles"; Pine Tar jars; the bowls and plates for mass-noun dishes; bags for Sugar / Flour / Rice
   (bulk with a container in the sprite; the brief's bare rule is for bulk with none). All one-line
   table edits in `AskPhrases`.
2. Containers are English words in Core, like the rest of `ItemPlurals`; in another language the game
   pluralizer returns the word unchanged but a container phrase would still be English. The mod ships
   only `default.json` today.
3. A smaller board-wide pool means more nights with no fair replacement (the existing skip and retry
   path); not measured.
4. `SabotageService.cs` is well past 400 lines (it was before this change).
