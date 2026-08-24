# The Longest Year

**Restore the Community Center within a single year — or the Junimos rewind the seasons and you begin again, a little stronger.**

A roguelite time-loop for Stardew Valley (PC).

⬇ **[Download on Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/47192)**

**The Longest Year** turns Stardew Valley's first year into a roguelite loop. Each season asks you to give back enough of the land's bounty to the old Community Center hall. Fall short by a season's end and the Junimos turn time back to Spring 1 — the world resets, but the strength you've earned (and the power your offerings bank) can carry forward. Restore the whole Center inside one year to break the loop for good.

This is a **beta** (`0.12.17`). It is feature-complete for v1 and stable in testing; what it most needs now is feedback on **difficulty, pricing, and pacing**. See [Giving feedback](#giving-feedback).

---

## What's New in 0.12.17

**On a Fail night you can now keep your bundle board for the next loop instead of letting the Junimos reshuffle it.**

- **Keep these bundles, or let time reshuffle them.** After the Junimos' Fail-night speech and before the shrine, you choose: hold the same bundle board into the next loop, or roll a fresh one. A kept board asks for exactly the same things, so anything you gathered for a bundle you almost finished is still worth having.
- **The first hold is free; holding again in a row costs JP.** 50, 100, 200, then 300 JP for each further consecutive hold (config `BundleHoldCosts`). Letting time reshuffle resets the price back to free.
- **TLY Custom boards only.** The hold applies to the mod's own board (the new-game **TLY Custom** choice). Normal and Remixed vanilla boards keep regenerating the game's way on every reset, as before.
- **The Season Goals title shows how many times the board has been held**, and the day-1 Junimo now says up front that impossible-looking asks are expected, can be set aside for later, and can be held across a rewind.
- **Text cleanup** across all in-game strings.

Full history in [CHANGELOG.md](CHANGELOG.md).

---

## Features

- **Seasonal time-loop.** Each season has a donation minimum. Miss it and the year unwinds to Spring 1.
- **Junimo Points.** Donations earn JP — scaled by rarity and by how late in the year you give. JP banks across loops.
- **The Junimo Shrine.** Spend JP on upgrades that let you hold on to some of what you gained: skill levels, tool tiers, recipes, buildings, a kept pet, and more.
- **Weekly themes.** Each week, pick a theme that grants a bonus and a paired liability. Plan around it.
- **Carryover surfaces.** A **Bundle Log** book that tracks each season's goals, a Cookbook and Craftbook to bank recipes, and a Junimo Stash chest that survives resets.
- **A real intro.** Lewis greets you on the porch; a Junimo explains the loop. Then the run begins.
- **A starved Traveling Cart.** Joja has squeezed the merchant's suppliers — the cart carries **one item** per visit until you unlock more stalls with the **Cart Stall** upgrades (and Cart Whisperer previews what's coming). Prefer the full vanilla cart? Turn off `LimitTravelingCartStock`.
- **Break the loop.** Finish the Center in a year to win — then keep playing or start fresh.

## Requirements

- **Stardew Valley 1.6+** (PC — Windows/Linux/macOS)
- **SMAPI 4.0.0** or newer
- A **new save** on the **Standard farm** (see [Limitations](#limitations-beta))

## Install

1. Install [SMAPI](https://smapi.io/) (4.0.0+).
2. Download the latest `TheLongestYear` release and unzip it into your `Stardew Valley/Mods` folder, so you have `Mods/TheLongestYear/TheLongestYear.dll`.
3. Launch the game through SMAPI.
4. **Start a new game on the Standard farm.** The farm-type and skip-intro options are managed for you — the mod's own intro plays in their place.
5. **Community Center Bundles** under **New → Advanced Options** defaults to **TLY Custom**: every loop rolls a fresh board from the vanilla + remix pools plus the mod's own authored bundles. Prefer the game's own board (or another bundle mod's)? Pick **Normal** or **Remixed** there instead — the mod keeps that board and re-rolls it the same way on every reset. (`BundleSource` in the config/GMCM is the default for new games and switches an existing save at its next reset.)

   ![New game → Advanced Options → Community Center Bundles showing TLY Custom](release-notes/advanced-options-tly-custom.png)

## How it works

- **The intro.** On a fresh game, Lewis greets you on the porch, then a Junimo explains the loop. You wake on Spring 1 and pick your first **weekly theme**.
- **Weekly themes.** Each week you choose a theme that grants a bonus and a matching liability (e.g. more forage on pickup, but the mines are closed). The planning hub opens at the start of each week.
- **Seasonal goals.** The **Bundle Log** book (click to open) tracks each season's required donations. Each season has a minimum you must donate to the Center before the season turns. **Miss it and the year unwinds to Spring 1.**
- **Fail night.** When a season's minimum is missed, the Junimos rewind the year. Before the shrine they ask whether to keep the same bundle board for the next loop or let time reshuffle it. The first hold is free; each further hold in a row costs 50, 100, 200, then 300 JP, and reshuffling resets the price. (TLY Custom boards only.)
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
| `StashTileX/Y` | `0,0` (auto) | Where the Junimo Stash chest is placed (`0,0` = auto-pick near the farmhouse). The Bundle Log / Cookbook / Craftbook are placeable furniture you can put anywhere. |
| `LimitTravelingCartStock` | `true` | Cap the Traveling Cart to the stalls unlocked by the Cart Stall upgrades (one item until Cart Stall II). `false` = full vanilla cart |
| `BundleSource` | `Engine` | `Engine` = the mod's own board every loop (the new-game **TLY Custom** choice). `Vanilla` = keep the game's Standard/Remixed board (or another bundle mod's, e.g. Challenging Community Center Bundles) and re-roll it the same way on each reset. Takes effect at the next reset; the new-game dropdown sets it per save |
| `BundleHoldCosts` | `[0, 50, 100, 200, 300]` | JP cost of keeping the same bundle board on a Fail night, by how many holds you have taken in a row (first is free; the last value repeats). Reshuffling resets the count |
| `Enabled` | `true` | Master switch — turn the whole mod off to play vanilla |

Upgrade prices are defined in the shrine catalog (e.g. Cookbook/Craftbook tiers at 150 / 350 / 700 JP). Feedback on these is welcome.

## Limitations (beta)

- **PC only.** No Android port yet.
- **Standard farm only.** Other farm layouts put buildings in water and the stash off-map; the mod forces Standard on new games.
- **Start on a new save.** A run can only begin from a new game; other saves load normally and are left untouched.
- Intro cutscene and dialogue are a first pass.
- Multiplayer is untested.

## Giving feedback

What helps most right now:

1. **Difficulty** — do the seasonal minimums feel fair? Too punishing, too easy? Which season wall hit hardest?
2. **Pricing** — are JP earnings and shrine upgrade costs well-balanced? What did you save for first, and did it feel worth it?
3. **Pacing** — how many loops before the run "clicked"? Did the carryover make later loops feel meaningfully stronger?
4. **Bugs / crashes** — include your `SMAPI-latest.txt` (`Stardew Valley/ErrorLogs/`).

## Art wanted

The mod leans on vanilla sprites throughout. If anyone would enjoy making some custom **book / sprite artwork** (the Cookbook and Craftbook especially), I'd genuinely love to accept it and credit you. Drop a note in the comments.

*Banner art by **cwybabiesucks** — thank you!*

## Translations

As of this version, every player-visible string in the mod lives in a JSON file
(`i18n/default.json`) — the mod is fully translatable with no DLL edits or rebuilds. See
[`docs/TRANSLATING.md`](docs/TRANSLATING.md) for how to add a language. If you translate it,
let us know (Nexus DM or GitHub issue) and we'll link your work from the mod page.

---

## Also by this author

- [**Android Consolizer**](https://www.nexusmods.com/stardewvalley/mods/41869) — Full console-style controller support for Stardew Valley on Android.
- [**Cart Catalog**](https://www.nexusmods.com/stardewvalley/mods/47146) — Order from the Traveling Cart's daily stock; items arrive in a package on your porch the next morning.
- [**Nap Time**](https://www.nexusmods.com/stardewvalley/mods/42616) — Nap in bed to recover energy without ending the day. Configurable rate and wake-up cap. PC + Android.

## Source

Open source (MIT) — [github.com/sonofskywalker3/TheLongestYear](https://github.com/sonofskywalker3/TheLongestYear)

---

<!-- GitHub-only appendix (not part of the Nexus description) -->

## Building from source

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release   # builds + deploys to the Mods folder
dotnet test  TheLongestYear.sln -c Release                          # runs the unit suite
```

Core game logic lives in `TheLongestYear.Core` (pure, unit-tested); SMAPI/Harmony glue lives in `TheLongestYear`. Design specs and implementation plans are under `docs/superpowers/`.

## Credits

By **sonofskywalker3**. Banner art by **cwybabiesucks**. Built on [SMAPI](https://smapi.io/) and [HarmonyX](https://github.com/BepInEx/HarmonyX). Stardew Valley is a trademark of ConcernedApe.

## License

Released under the [MIT License](LICENSE).
