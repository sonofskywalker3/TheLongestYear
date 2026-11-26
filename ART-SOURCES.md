# Art sources

Every piece of artwork The Longest Year adds or changes, and how it was made. Any AI art here is only there because I can't
make it myself. If you would like to draw a replacement for anything here, I would love to use real art and
will credit you in full (see the end of this file).

Art the game draws unchanged from your own copy of Stardew Valley is not listed. Screenshots in `docs/images`
are pictures of the mod running, not artwork, and are not listed either.

## Drawn by people

| File | What it is | Made by |
|---|---|---|
| [books.png](src/TheLongestYear/assets/books.png) | The book covers: red (the Keeping Book, which holds the Cookbook, Craftbook and Herd Book), blue (the old Craftbook) and green (the Bundle Log, used on its Collections side tab) | Drawn by **supercam19**, contributed to the mod |
| The banner on the Nexus Mods page (not in this repository) | The mod's title banner | Drawn by **cwybabiesucks** |

## Shipped files changed from the game's own sprites

These two files are the game's own sprites, recolored by a script written with AI (Claude, by Anthropic):
[extract_sprites.py](tools/extract_sprites.py).

| File | What it is | Game sprite it starts from | Change |
|---|---|---|---|
| [shrine.png](src/TheLongestYear/assets/shrine.png) | The Junimo Shrine on the farm | Stone Junimo | palette swap: the Junimo body green, the star bright gold |
| [junimo_stash.png](src/TheLongestYear/assets/junimo_stash.png) | The Junimo Stash chest, with its five lid frames | Junimo Chest | palette swap: purple and black instead of green |

## Drawn with AI

| File | What it is | How |
|---|---|---|
| [circle_of_warding.png](src/TheLongestYear/assets/circle_of_warding.png) | The Circle of Warding rug | drawn as pixel art by AI (Claude, by Anthropic); a placeholder |

## Made on your computer while the game runs

These are built from the game's own art while you play, or drawn in code, by code written with AI (Claude, by
Anthropic). None of the game's art is in this repository. Each row links the code that makes it, so you can see
exactly what it does.

| What you see | Where it is made | How |
|---|---|---|
| The Bundle Log's side tab on the Collections page | [BundleLogSideTab.cs](src/TheLongestYear/Integration/BundleLogSideTab.cs) | supercam19's green cover set inside the game's own side-tab frame |
| Morris with the darkness on him at the end of the year | [MorrisDarkSprite.cs](src/TheLongestYear/Integration/MorrisDarkSprite.cs) | the game's Morris sprite darkened to a third, his eyes recolored red |
| Morris's dark portrait for his last line | [MorrisDarkPortrait.cs](src/TheLongestYear/Integration/MorrisDarkPortrait.cs) | the game's Morris portrait darkened, the lenses of his glasses lit red |
| The Junimos' portrait when they speak in a scene | [JunimoPortrait.cs](src/TheLongestYear/Integration/JunimoPortrait.cs) | the game's Junimo sprite scaled up 3x and tinted Junimo green |
| Pierre's shop boarded up | [JojaPierreBoards.cs](src/TheLongestYear/Integration/JojaPierreBoards.cs) | the game's town art with Pierre's building darkened and greyed, the closed JojaMart's boarded door pasted over his, and planks cut from that door nailed over his windows |
| The figure at work in the Community Center's lit windows | [SceneWindowGlow.cs](src/TheLongestYear/Scenes/SceneWindowGlow.cs) | the game's Shadow Brute, blacked out to a silhouette; the window light is drawn in code by AI |
| The dark cloud over the map in the tampering scene | [CloudScene.cs](src/TheLongestYear/Scenes/CloudScene.cs) | the game's own map art, under soft cloud blobs drawn in code by AI |
| The glowing eyes in the overnight scenes | [SceneGlow.cs](src/TheLongestYear/Scenes/SceneGlow.cs) | a soft glow drawn in code by AI |
| The black tentacles in the rewind scene | [RewindTentacles.cs](src/TheLongestYear/UI/RewindTentacles.cs) | drawn in code by AI |
| The purple glow under tampered items | [TaintedAuraPatch.cs](src/TheLongestYear/Loop/TaintedAuraPatch.cs) | drawn in code by AI |
| The brackets in a lowered goal's count, like "10 (13)" | [TinyCount.cs](src/TheLongestYear/UI/TinyCount.cs) | two bracket glyphs built from the colors of the game's own tiny "0" digit |

## Contributing art

Replacements from artists are welcome and get full credit here, in the README and in the release notes. The
shrine, the stash chest and the Circle of Warding are the biggest wants. For a sprite, a PNG the same size as
the file it replaces is all it takes. Open an issue or a pull request on GitHub, or leave a comment on the
Nexus Mods page.
