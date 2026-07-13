# The Longest Year

**Restore the Community Center within a single year — or the Junimos rewind the seasons and you begin again, a little stronger.**

A roguelite time-loop for Stardew Valley (PC).

⬇ **[Download on Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/47192)**

**The Longest Year** turns Stardew Valley's first year into a roguelite loop. Each season asks you to give back enough of the land's bounty to the old Community Center hall. Fall short by a season's end and the Junimos turn time back to Spring 1 — the world resets, but the strength you've earned (and the power your offerings bank) can carry forward. Restore the whole Center inside one year to break the loop for good.

This is a **beta** (`0.11.44`). It is feature-complete for v1 and stable in testing; what it most needs now is feedback on **difficulty, pricing, and pacing**. See [Giving feedback](#giving-feedback).

---

## What's New in 0.11.44

The big fix release: weekly goals redesigned around real bundle slots, remixed bundles fully supported, the loop reset made airtight, and new upgrades. Changes since `0.11.0`:

- **Weekly goals now target real bundle slots.** Complete redesign: each goal names a specific still-open bundle slot — like `Parsnip x5 (gold) — Quality Crops` — and ticks only when that exact slot is completed. Goals can no longer be cleared by a single item when five were asked, demand items with no matching open slot, or ask for something you already donated. If nothing's open, the week has no quest and the drawback lifts. The 1.5× banking bonus is slot-strict.
- **Remixed bundles all count.** Remixed or custom bundles that matched no known pattern were silently left out of season checkpoints and weekly themes — the gate quietly shrank on the recommended remixed setup. Every bundle now classifies (custom-bundle mods included), with sensible quotas derived for unknown ones.
- **The loop reset is airtight.** A full audit closed every "survived the loop" leak: museum donations and lost library books, worn boots/rings/trinkets, monster-slayer progress, mine milestone chests, power books / mastery / prize tickets, and max health/stamina all rewind with the year now. And your **clothes stay on** — hat, shirt, and pants are yours to keep.
- **Kept buildings stay where YOU put them.** Coop, barn, and silo keeps rebuild each loop at their last position instead of a fixed spot, just like the stable. New **Keep Silo** upgrade (150 JP, Buildings).
- **New Foresight chain: Cart Whisperer I–V.** On Traveling Cart days, the shrine planning view flags which of the cart's actual stock can feed a Community Center bundle.
- **Green rain is back in summer.** The scheduler was overriding vanilla's green-rain day; it's now reserved like a festival and shows in the TV and Weather Sage forecasts. A reset also no longer drags the old day's weather into Spring 1.
- **The farm cave asks again.** Each loop, entering the cave offers the mushrooms / fruit bats choice fresh instead of locking your first-ever pick in forever.
- **Big-chest mod compatibility.** Better Chests and Unlimited Storage no longer inflate the 4-slot Junimo Stash into a giant grid.
- **Smaller fixes:** the horse no longer asks to be renamed every morning; a theme pick can't be lost to a stale offer; quest-tip layout.

Full history in [CHANGELOG.md](CHANGELOG.md).

---

## Features

- **Seasonal time-loop.** Each season has a donation minimum. Miss it and the year unwinds to Spring 1.
- **Junimo Points.** Donations earn JP — scaled by rarity and by how late in the year you give. JP banks across loops.
- **The Junimo Shrine.** Spend JP on upgrades that let you hold on to some of what you gained: skill levels, tool tiers, recipes, buildings, a kept pet, and more.
- **Weekly themes.** Each week, pick a theme that grants a bonus and a paired liability. Plan around it.
- **Carryover surfaces.** A **Bundle Log** book that tracks each season's goals, a Cookbook and Craftbook to bank recipes, and a Junimo Stash chest that survives resets.
- **A real intro.** Lewis greets you on the porch; a Junimo explains the loop. Then the run begins.
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
5. **Recommended: turn ON "remix bundles"** (Advanced Options → Community Center Bundles → Remixed when creating the farm). Every loop reshuffles which bundles you get and what they ask for, so no two runs play the same — it's the setup the mod is balanced around. Standard bundles work fine too if you prefer to master one fixed set.

## How it works

- **The intro.** On a fresh game, Lewis greets you on the porch, then a Junimo explains the loop. You wake on Spring 1 and pick your first **weekly theme**.
- **Weekly themes.** Each week you choose a theme that grants a bonus and a matching liability (e.g. more forage on pickup, but the mines are closed). The planning hub opens at the start of each week.
- **Seasonal goals.** The **Bundle Log** book (click to open) tracks each season's required donations. Each season has a minimum you must donate to the Center before the season turns. **Miss it and the year unwinds to Spring 1.**
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
