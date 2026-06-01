# The Longest Year

> Roguelite time-loop for Stardew Valley: restore the Community Center within a single year — or the Junimos rewind the seasons and you begin again, a little stronger.

**The Longest Year** turns Stardew Valley's first year into a roguelite loop. Each season asks you to give back enough of the land's bounty to the old Community Center hall. Fall short by a season's end and the Junimos turn time back to Spring 1 — the world resets, but the strength you've earned (and the power your offerings bank) can carry forward. Restore the whole Center inside one year to break the loop for good.

This is a **beta** (`1.0.0-beta.1`). It is feature-complete for v1 and stable in testing; what it most needs now is feedback on **difficulty, pricing, and pacing**. See [Giving feedback](#giving-feedback).

---

## Requirements

- **Stardew Valley 1.6+** (PC — Windows/Linux/macOS)
- **SMAPI 4.0.0** or newer
- A **new save** on the **Standard farm** (see [Limitations](#limitations))

## Install

1. Install [SMAPI](https://smapi.io/) (4.0.0+).
2. Download the latest `TheLongestYear` release and unzip it into your `Stardew Valley/Mods` folder, so you have `Mods/TheLongestYear/TheLongestYear.dll`.
3. Launch the game through SMAPI.
4. **Start a new game on the Standard farm.** The farm-type and skip-intro options are managed for you — the mod's own intro plays in their place.

## How it works

- **The intro.** On a fresh game, Lewis greets you on the porch, then a Junimo explains the loop. You wake on Spring 1 and pick your first **weekly theme**.
- **Weekly themes.** Each week you choose a theme that grants a bonus and a matching liability (e.g. more forage on pickup, but the mines are closed). The planning hub opens at the start of each week.
- **Seasonal goals.** A tracker sits **above the Community Center fireplace**. Each season has a minimum you must donate to the Center before the season turns. **Miss it and the year unwinds to Spring 1.**
- **Junimo Points (JP).** Donations earn JP, scaled by rarity and by how late in the year you give (later seasons are worth much more). JP banks across loops.
- **The Junimo Shrine.** On every loop reset (and on a win), spend banked JP on upgrades that let you *hold on to some of what you gained* next loop — skill levels, tool tiers, recipes, buildings, a kept pet, and more.
- **Carryover surfaces on the farm.** A **Cookbook** (kitchen) and **Craftbook** (table) let you bank recipes to keep; a **Junimo Stash** chest preserves a few items across resets.
- **Winning.** Restore the entire Community Center within a year to break the loop. You can then choose to keep playing that run or start a fresh loop.

## Configuration

All knobs live in `Mods/TheLongestYear/config.json` (created on first run). The values most worth tuning during the beta:

| Setting | Default | What it controls |
|---|---|---|
| `Jp.CommonJp` / `UncommonJp` / `RareJp` / `VeryRareJp` | 1 / 3 / 10 / 25 | JP awarded per donated item by rarity |
| `Jp.SeasonMultipliers` | `[1.0, 1.5, 2.5, 4.0]` | Per-season JP multiplier (Spring→Winter) |
| `Jp.BundleCompletionBonus` / `RoomCompletionBonus` / `WeeklyQuestCompletionBonus` | 15 / 60 / 30 | Bonus JP for milestones (×season multiplier) |
| `StartingMoney` | 500 | Gold at the start of each loop |
| `BundleQuotas` | per-bundle | How much each percentage-bundle asks for |
| `SeasonGoalsBoardTileX/Y`, `CookbookTileX/Y`, `CraftbookTileX/Y`, `StashTileX/Y` | tuned defaults | Where the interactables live (auto-placed; override if needed) |
| `Enabled` | `true` | Master switch — turn the whole mod off to play vanilla |

Upgrade prices are defined in the shrine catalog (e.g. Cookbook/Craftbook tiers at 150 / 350 / 700 JP). Feedback on these is welcome.

## Limitations (beta)

- **PC only.** No Android port yet.
- **Standard farm only.** Other farm layouts put buildings in water and the stash off-map; the mod forces Standard on new games.
- **Start on a new save.** Loading the mod onto an existing year-1+ save is unsupported.
- Intro cutscene and dialogue are a first pass.
- Multiplayer is untested.

## Giving feedback

What helps most right now:

1. **Difficulty** — do the seasonal minimums feel fair? Too punishing, too easy? Which season wall hit hardest?
2. **Pricing** — are JP earnings and shrine upgrade costs well-balanced? What did you save for first, and did it feel worth it?
3. **Pacing** — how many loops before the run "clicked"? Did the carryover make later loops feel meaningfully stronger?
4. **Bugs / crashes** — include your `SMAPI-latest.txt` (`Stardew Valley/ErrorLogs/`).

See [`docs/beta-release-notes.md`](docs/beta-release-notes.md) for the tester guide.

## Building from source

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release   # builds + deploys to the Mods folder
dotnet test  TheLongestYear.sln -c Release                          # runs the unit suite
```

Core game logic lives in `TheLongestYear.Core` (pure, unit-tested); SMAPI/Harmony glue lives in `TheLongestYear`. Design specs and implementation plans are under `docs/superpowers/`.

## Credits

By **sonofskywalker3**. Built on [SMAPI](https://smapi.io/) and [HarmonyX](https://github.com/BepInEx/HarmonyX). Stardew Valley is a trademark of ConcernedApe.
