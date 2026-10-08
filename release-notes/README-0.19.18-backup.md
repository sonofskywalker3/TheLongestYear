# The Longest Year

**Restore the Community Center within a single year — or the Junimos rewind the seasons and you begin again, a little stronger.**

A roguelite time-loop for Stardew Valley (PC).

⬇ **[Download on Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/47192)**

**The Longest Year** turns Stardew Valley's first year into a roguelite loop. Each season asks you to give back enough of the land's bounty to the old Community Center hall. Fall short by a season's end and the Junimos turn time back to Spring 1 — the world resets, but the strength you've earned (and the power your offerings bank) can carry forward. Restore the whole Center inside one year to break the loop for good.

This is a **beta** (`0.19.10`). It is feature-complete for v1 and stable in testing; what it most needs now is feedback on **difficulty, pricing, and pacing**. See [Giving feedback](#giving-feedback) below.

**This is the last big engine update.** From here the plan is bug fixes and balance passes driven by your feedback, and then work begins on the story. So this is the version to tell me what is wrong with.

---

## See it played

**Emmalution** is playing the whole loop on YouTube, and her edited episodes are the best introduction to the mod there is. Thank you, Emma, for the videos and for the bug reports that came with them.

- Spring: [I must donate to the Community Centre OR RESET!](https://www.youtube.com/watch?v=fU4EVzlD1K8)
- Summer: [The Junimos challenged me to complete these bundles...](https://www.youtube.com/watch?v=WaAzDjMWmt8)

Channel: [youtube.com/@emmalution](https://www.youtube.com/@emmalution)

---

## What's New in 0.19.10

**TLY Custom boards ask only for vanilla items.** Other mods' items are left out of what TLY Custom bundles ask for and what they give, so the balance holds whatever else you have installed.

**New setting: Allow mod items in custom bundles.** It sits next to Bundle source in the mod's settings menu (GMCM). On, TLY Custom bundles ask for and give other mods' items the way they used to. It is off on new farms. Saves you already have start with it on, so they carry on the way they were created. Like Bundle source, it belongs to the save you have loaded and a change applies at your next loop.

**Tech's Cross-Mod Bundles: a fresh board every loop.** With it installed, Normal and Remixed now roll a new Tech board at each reset instead of handing back the old one. Reloading a save keeps your loop's board, with TLY's difficulty changes, instead of bringing back an older Tech board.

**Fixed: a swapped-out item stays swapped.** When a bundle asks for something you can't get this year, TLY swaps it for something you can. That swap now stays put through reloads, instead of a different item turning up each time you load.

**If you play with other mods, please read this.** Turning on Allow mod items in custom bundles, or playing Normal or Remixed with other mods installed, can give you bundles that ask for items that are impossible to get. That will stay true until I start tuning for specific mods. Other mods are not officially supported, but if you report an issue with one, I will look at it.

Thanks to PixxiePerfect, who pointed me at Tech's Cross-Mod Bundles.

## 0.19.1

**Fixed: the Bundle Log shows the same counts as the Community Center.** After a theme week's discount ended, the Bundle Log could keep showing the lower count while the Community Center asked for the full amount (or the other way round right after picking a theme). The Log now reads the counts from the Community Center each time you open it. Reported on Reddit.

## 0.19.0: the Randomizer

**A new Randomizer section in the mod's settings, for runs that play out differently each time.**

Every option in it is off by default, so if you never open it, the game plays exactly as balanced. The weekly options take effect from next week's offer, and Random bundle rewards from the next loop.

- **Theme rerolls** (Off / Costs JP / Free). A Re-roll Themes button in the planning hub. On Costs JP the first reroll each week is 50 JP, each one after costs double, and the price goes back to 50 every week.
- **Random theme items.** Weekly goals are drawn evenly from everything a theme could ask for. Nothing impossible is ever asked.
- **Random pairings.** Each card keeps its theme's buff but gets a random drawback, never one that blocks that theme's own goals.
- **Random multiplier.** Each card shows its own JP multiplier, from 0.5x to 1.5x, for the JP from that theme's goals.
- **Mystery card.** About one week in four, one card is dealt face down. It shows only its multiplier, 1.25x to 1.75x, and the theme is revealed when you pick it.
- **Double theme week.** Once a season, in week 2 or 3, you take both cards: both buffs, both drawbacks and two goal lists.
- **Wildcard days.** One day each week gets a one-day twist, good, bad or odd. You see which day at the start of the week and the twist that morning.
- **Random shrine donations.** Extra weekly goals for items with no Community Center slot, donated on the farm statue's new Donate tab. Missing them never fails you, but the theme keeps its drawback.
- **Random bundle rewards.** Bundle rewards are picked at random, starting from the next loop. The Vault bundles are included.
- **Random cart days.** The Traveling Cart comes on random days, about two a week, never on a festival.

The old **Allow re-rolling the weekly themes** switch has moved into the Randomizer section. If you had it on, Theme rerolls is set to Free.

**Also fixed: Dried Fruit slots no longer ask for grapes.** The Dehydrator turns grapes into Raisins, not Dried Fruit, so a Dried Grapes slot could never be filled. Reported by Treedomy.

**Also fixed: a festival that ends while someone is talking no longer freezes the night.**

Thanks to Nijah, whose Nexus thread started this.

## 0.18 in brief

- **Seven new keeps.** The Herd Book brings your registered animals back with their names and hearts, and Junimo Upgrades adds Keep Fish Pond, Keep Lost Books, Keep Farm Decor, Keep Worn Gear, Keep Farmhouse Furniture and Keep Special Orders Board.
- **Restart the year.** A button on the Junimo Shrine starts a fresh loop whenever you like. Season pity is gone; use the Difficulty settings instead.
- **Spring, Summer and Fall Returns.** One-week boosts that bring a past season's fish and forage back.
- **One Difficulty setting** sets every dial at once, and once-per-loop items like the legendary fish are only ever asked for once.
- **Bundles only ask for what a year can reach.** Ginger Island, year 2 shops, the wrong water or season and other mods' items a year can't reach stay off the board, and a board you already have gets repaired when you load it.
- **Cookbook and Craftbook start with four free slots**, and a fourth tier takes them to 20.
- **Picking a theme lowers that week's goals** on Easy and Normal.

Every 0.18 change, with who reported it, is in [CHANGELOG.md](CHANGELOG.md).

---

## Features

- **Seasonal time-loop.** Each season has a donation minimum. Miss it and the year unwinds to Spring 1.
- **Junimo Points.** Donations earn JP — scaled by rarity and by how late in the year you give. JP banks across loops.
- **Junimo Upgrades.** Spend JP on upgrades that let you hold on to some of what you gained: skill levels, tool tiers, recipes, buildings, a kept pet, the power books you have read, your wallet items and Stardrops, the Junimos' own gifts (greenhouse, quarry bridge, boulder, minecarts, bus), and more.
- **JP Boosts.** The Junimo statue on your farm sells this-loop edges for JP: tomorrow's rain or a lucky day, a free late night, stacking forage/fish/crop/mine/drop chances, a faster walk, a season of cheaper shops, faster friendships, extra energy, a skill level on the spot, or an elevator stop you have not earned yet. An Active tab shows what is running and a Plan tab shows what each keep still needs.
- **Weekly themes.** Each week, pick one of two themes (Foraging, Farming, Fishing, Mining, Mixed, Spelunking, Artisan, Kitchen) for a bonus and a paired liability. Goals follow the season gate and the weekly bonus is paid per goal.
- **Randomizer.** An optional settings section that adds chance to the weekly themes, goals, bundle rewards and the cart. Everything in it starts off.
- **Carryover surfaces.** A **Bundle Log** book that tracks each season's goals, a Cookbook and Craftbook to bank recipes, a **Herd Book** that brings your registered animals back with their hearts, and a Junimo Stash chest that survives resets.
- **A real intro.** Lewis greets you on the porch; a Junimo explains the loop. Then the run begins.
- **A starved Traveling Cart.** Joja has squeezed the merchant's suppliers — the cart carries **one item** per visit until you unlock more stalls with the **Cart Stall** upgrades (and Cart Whisperer previews what's coming). Prefer the full vanilla cart? Turn off `LimitTravelingCartStock`.
- **The town half-remembers.** Villagers you have spent a lot of time with across loops occasionally say something uncanny. Rare, no gameplay effect, and it never explains itself. Toggle in Features.
- **Break the loop.** Finish the Center in a year to win — then keep playing or start fresh.

## Requirements

- **Stardew Valley 1.6+** (PC: Windows, Linux, macOS)
- **SMAPI 4.0.0** or newer
- A **new save** (any farm type; see the note under Limitations)

## Install

1. Install [SMAPI](https://smapi.io/) (4.0.0+).
2. Download the latest `TheLongestYear` release and unzip it into your `Stardew Valley/Mods` folder, so you have `Mods/TheLongestYear/TheLongestYear.dll`.
3. Launch the game through SMAPI.
4. **Start a new game** on any farm type. Leave **Skip intro** off the first time: the mod's own opening plays in place of the vanilla one and explains the loop. Tick it on later farms to go straight to the theme picker.
5. **Community Center Bundles** under **New → Advanced Options** defaults to **TLY Custom**: every loop rolls a fresh board from the vanilla + remix pools plus the mod's own authored bundles, using vanilla items only unless you turn on Allow mod items in custom bundles. Prefer the game's own board (or another bundle mod's)? Pick **Normal** or **Remixed** there instead. The mod keeps that board and re-rolls it the same way on every reset. With other mods installed, those bundles may not reshuffle each loop and may ask for items you can't get. (You can change this later: `Bundle source` in GMCM switches an existing save between all three, applying at its next loop.)

   ![New game → Advanced Options → Community Center Bundles showing TLY Custom](release-notes/advanced-options-tly-custom.png)

## How it works

- **The intro.** On a fresh game, Lewis greets you on the porch, then a Junimo explains the loop. You wake on Spring 1 and pick your first **weekly theme**.
- **Weekly themes.** Each week you choose one of two offered themes. Room themes (Foraging, Farming, Fishing, Mining) take goals from their Community Center room; Spelunking, Artisan and Kitchen take goals by item kind from anywhere on the board, and Mixed takes anything. Goals follow what the season gate demands first; the weekly JP bonus is paid goal by goal and the drawback lifts when every goal is done. The planning hub opens at the start of each week.
- **Seasonal goals.** The **Bundle Log** book (click to open) tracks each season's required donations. Each season has a minimum you must donate to the Center before the season turns. **Miss it and the year unwinds to Spring 1.**
- **Fail night.** When a season's minimum is missed, the Junimos rewind the year. Before the upgrade menu opens, they ask whether to keep the same bundle board for the next loop or let time reshuffle it. The first hold is free; each further hold in a row costs 50, 100, 200, then 300 JP, and reshuffling resets the price. This works on every bundle source.
- **Junimo Points (JP).** Donations earn JP, scaled by rarity and by how late in the year you give (later seasons are worth much more). JP banks across loops.
- **Junimo Upgrades.** On every loop reset (and on a win), spend banked JP on upgrades that let you *hold on to some of what you gained* next loop — skill levels, tool tiers, recipes, buildings, a kept pet, the power books you have read, your wallet items and Stardrops, and more.
- **JP Boosts.** The Junimo statue on the farm sells this-loop edges for JP any day (weather, luck, a free late night, stacking bonuses, a temporary skill level, an elevator stop) and shows what is running on its Active tab.
- **Carryover surfaces on the farm.** A **Cookbook** (kitchen) and **Craftbook** (table) let you bank recipes to keep; a **Junimo Stash** chest preserves a few items across resets.
- **Restarting.** Want a fresh loop before the season runs out? The Junimo Shrine's **Restart the year** button ends the day and runs a Fail night without the Junimo scene: keep or reshuffle your bundles, spend JP, bank recipes, and wake on Spring 1. It counts as a loop, and it works after Keep playing too.
- **Winning.** Restore the entire Community Center within a year to break the loop. You can then choose to keep playing that run or start a fresh loop.

## Difficulty

Nine difficulty dials live in the mod's settings menu (GMCM) under **Difficulty**, with one overall **Difficulty** option above them. Pick a level there and every dial switches to it, then change any single dial you like. Every dial has four steps, **Easy / Normal / Hard / Extreme**, and every one starts on **Normal**, which is the balance the mod ships with. Changing nothing changes nothing.

**Changes take effect on your next loop, not straight away.** The dials are stamped onto your save when a loop begins, so the year you are already playing keeps the rules it started under.

![The Difficulty section of the settings menu, every dial on Normal](release-notes/settings-difficulty.png)

**What the bundles ask for**

- **Stack size.** How many of an item a slot asks for, as a share of what a week of going after it actually yields: Easy 10 to 30%, Normal 20 to 50%, Hard 50 to 65%, Extreme 65 to 80%, and never above 80%. Fish, forage, crops, crab pots, monster drops, the mines, animals and machines each have their own yield behind that number, and a yield is capped at one stack of 99 (Slime, Bat Wings and the like), so an Extreme ask of 80 is a stack, not a share. Artifacts stay at one. Money bundles are never affected.
- **Once-per-loop items ask for one.** On by default. The five legendary fish, the two books hidden around town and the Golden Pumpkin can only be had once in a loop, so a bundle never asks for more than one of them whatever Stack size says. Off, they scale like everything else. Takes effect at the next reset.
- **Quality asks.** How often a slot wants a silver or gold star. Items the game never gives a star to are still never asked for at quality, at any step.
- **Required slots.** How many of a bundle's shown items you must actually donate. Hard asks for one more, Easy one fewer, Extreme asks for all of them.
- **Item rarity.** Weights bundles toward harder items: rarer, later in the year, or needing a keg or a press. **TLY Custom bundles only** (see below).

**What you carry between loops**

- **Junimo Points earned.** Scales every JP award, so progress across loops is faster or slower. The season ramp keeps its shape, so late-season donating is still worth the most.
- **Upgrade prices.** Scales what upgrades cost in the upgrade menu.
- **Starting gold.** Scales the `StartingMoney` value rather than replacing it. Extreme starts you with nothing.
- **Starting cart slots.** How many items the Traveling Cart offers before you buy any Cart Stall upgrade. On Hard and Extreme the cart is empty until you buy Cart Stall I.
- **Hold prices.** Scales the JP price of keeping your board on a Fail night. The first hold stays free at every step.

**One dial does not work on vanilla boards.** Item rarity applies to **TLY Custom** bundles only, because changing which item a vanilla bundle asks for would be changing the bundle. Stack size, quality asks and required slots all work on vanilla Standard and Remixed boards too.

`tly_difficulty` in the SMAPI console prints what your save is actually running under, including anything you have changed since your last loop. Please attach it to any balance report.

## Switching bundle source later

You are not locked into the board you picked when you started. **Bundle source** in the mod's settings menu (GMCM) is one setting with three choices, and you can move an existing save between any of them:

- **TLY Custom** - the mod composes a fresh board every loop from the vanilla and remix pools plus its own authored bundles. It asks only for vanilla items and gives only vanilla rewards: other mods' items are left out so the balance holds. **Allow mod items in custom bundles**, next to Bundle source, lets other mods' items in. It is off on new games, and saves from before 0.19.4 start with it on. Other mods aren't supported, so with it on a bundle may ask for an item you can't get.
- **Normal** - the game's own standard bundle layout, re-rolled the same way each loop.
- **Remixed** - the game's own remixed layout, likewise.

Another bundle mod's board is covered by Normal or Remixed: whatever the game generates is what the mod keeps. Using Normal or Remixed with other mods (bundle mods included) is allowed, but the bundles may not reshuffle each loop and may ask for items you can't get. With Tech's Cross-Mod Bundles, both Normal and Remixed roll a fresh board each loop.

Like the difficulty dials, a switch applies at your **next loop**, not straight away. The year you are already playing keeps the board it started with. The setting belongs to the save you have loaded. On the title screen it only picks what a new game starts with, so one farm's choice never changes another's.

**Keeping your board on a Fail night works on all three.** If you hold, you get the same board back next loop whichever source it came from.

## Configuration

The **Features** section of the settings menu turns individual parts of the mod off or tunes them, and changes there apply straight away:

![The Features section of the settings menu](release-notes/settings-features.png)

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
| `BundleSource` | `Engine` | One setting, three values: `Engine` (the mod's own board every loop, the new-game **TLY Custom** choice), `Normal` or `Remixed` (the game's own board of that kind, or another bundle mod's, re-rolled the same way each loop). Only the default a new game starts on: each save keeps its own choice, which the in-game settings menu changes, and it takes effect at the next loop. See [Switching bundle source later](#switching-bundle-source-later) |
| `AllowModItemsInCustomBundles` | `false` | Whether TLY Custom bundles may ask for and give other mods' items. Only the default a new game starts on: each save keeps its own choice (saves from before 0.19.4 start with it on), which the in-game settings menu changes, and it takes effect at the next loop. Other mods aren't supported, so with it on a bundle may ask for an item you can't get |
| `BundleHoldCosts` | `[0, 50, 100, 200, 300]` | JP cost of keeping the same bundle board on a Fail night, by how many holds you have taken in a row (first is free; the last value repeats). Reshuffling resets the count |
| `Enabled` | `true` | Master switch: turn the whole mod off to play vanilla |

Upgrade prices are defined in the upgrade catalog (e.g. Cookbook/Craftbook tiers at 150 / 350 / 700 / 1200 JP). Feedback on these is welcome.

## Is this a bug?

A few things get reported often enough to be worth answering up front.

**Demetrius never shows up about the cave, I just get a popup asking mushrooms or bats.**

That is the mod, working as intended. His scene plays once per playthrough; from the second loop on, walking into the farm cave gives you the choice directly instead of replaying a cutscene you have already watched. The mushrooms-or-bats decision is re-offered every loop because the rewind clears it.

**The Traveling Cart only has one item.**

Also intended. Joja is squeezing the merchant's suppliers, and the Cart Stall upgrades in the upgrade menu add slots back. If you would rather have the full cart, turn off LimitTravelingCartStock in the config.

**I did the Egg Hunt and now Lewis will not let me do it again today.**

Since 0.14.1, a festival's main event runs once per day. Festivals in this mod do not end your day, so you can walk out and back in - which used to restart the whole festival and let the hunt be repeated for the prize. The stalls, the shop and everyone at the festival still work on a repeat visit. A new loop is a clean slate: as far as the valley is concerned the festival has not happened yet, so you get to do it again.

**A bundle is asking for silver or gold on something that cannot have a quality.**

That was a bug and it is fixed. Quality is only ever asked for on things the game itself gives quality to (crop harvests, rod-caught fish, spawned forage). Existing boards pick the change up at the next reset.

**A weekly goal ticked off without me donating anything.**

Fixed in 0.14.0. Finishing a bundle that only needs some of its listed items used to mark the rest as filled, which credited goals you never handed in. A goal now waits for the real donation.

**I bought Keep Bus Unlocked and the Vault bundles are still on the board.**

Intended. The gift keeps the bus and the desert open from day 1; the Vault bundles are still part of restoring the Center, still have to be paid, and still earn JP. Completing them again does not replay the repair scene. Every Gift of the Junimos works this way.

**The skill level I bought at the statue was gone after the rewind.**

Intended. Crash Course levels are temporary: they last the loop, never reach level 10, and cannot be kept. Only levels you earn open the Keep Level rows on a Fail night.

**Elevator Pass says "Enter the mine first", or a weather boost says "Not now".**

Both intended. The pass extends the elevator from wherever it already reaches, so it needs one trip into the mine first. Rain Dance and Storm Call are refused on day 28 and the day before a festival, because the game forces sunshine on those mornings and the JP would be wasted.

**My enchanted sword lost its enchantments in the Junimo Stash.**

Fixed since 0.16.7. Tools and weapons in the stash keep their enchantments, forged gems, bait and tackle across the rewind.

Anything not on this list, please do report - the bugs tab on Nexus is read.

## Limitations (beta)

- **PC only.** No Android port yet.
- **Other mods are not officially supported.** TLY Custom boards ask only for vanilla items unless you turn on Allow mod items in custom bundles (off on new games; saves from before 0.19.4 start with it on), so by default other mods' items are left out and the balance holds. Normal or Remixed with other mods (including other bundle mods) is allowed, but the bundles may not reshuffle each loop and may ask for items you can't get. If something goes wrong with a specific mod, report it and I will take a look.
- **Farm types are balanced as a set, not individually.** The bundle asks are sized from Standard farm yields. Each other type trades one thing for another (Riverland and Beach: more fish, less field; Forest: more forage; Hilltop: more ore; Wilderness: more monsters; Meadowlands: easier animals), so the year is a little easier in some bundles and a little harder in others. Beach farm's no-sprinklers rule makes its crop bundles a real step harder. Custom farm maps that replace a vanilla slot load fine but are untested for balance.
- **Start on a new save.** A run can only begin from a new game; other saves load normally and are left untouched.
- Intro cutscene and dialogue are a first pass.
- Multiplayer is untested.

## Giving feedback

What helps most right now:

1. **Difficulty**: do the seasonal minimums feel fair? Too punishing, too easy? Which season wall hit hardest?
2. **Pricing**: are JP earnings and upgrade costs well-balanced? What did you save for first, and did it feel worth it?
3. **Pacing**: how many loops before the run "clicked"? Did the carryover make later loops feel meaningfully stronger?
4. **Bugs / crashes**: include your `SMAPI-latest.txt` (`Stardew Valley/ErrorLogs/`).

## Art wanted

The mod leans on vanilla sprites for most things. If anyone would enjoy making some custom **sprite artwork** (the shrine, the stash chest, icons), I'd genuinely love to accept it and credit you. Drop a note in the comments.

*Banner art by **cwybabiesucks**. Book art by **supercam19**. Thank you both!*

## Translations

As of 0.11.60, every player-visible string in the mod lives in a JSON file
(`i18n/default.json`) — the mod is fully translatable with no DLL edits or rebuilds. See
[`docs/TRANSLATING.md`](docs/TRANSLATING.md) for how to add a language. If you translate it,
let us know (Nexus DM or GitHub issue) and we'll link your work from the mod page.

---

## Thanks

This mod is shaped by the players who report bugs and suggest ideas. Every one of these made it better. Thank you!

- **tanky24u**: suggested the Herd Book and Restart the year, and reported ten bugs and balance problems.
- **FayGabi**: rings in the Dye bundle, Friendship 101 in the Book bundle, Chests Anywhere in the stash, once-per-loop items, and a new farm quit before its first night.
- **ChaoticMindset**: Willy's letter, quest scenes and Gunther's Rusty Key coming back every loop, and the Joja Mart bundle.
- **Emmalution**: played the whole loop on YouTube; her stream found the villager first-meeting lines and the weekly goals that ticked for free.
- **Dummy Dog Ben**: played it on YouTube; the stream turned up the shrine restart order, week 1 quality and artifact goals, the Dye bundle, Marnie's pet visit and tooltips running off the screen.
- **gazumbrado**: Golden Egg and legendary fish asks, fish quantities, Keep Bus Unlocked, and the Skip intro checkbox.
- **Thrippa**: eggs in the Foraging bundles and year-2 seeds from other mods.
- **ozzy2540**: seasons failing under Challenging CC Bundles, and a finished season that could still fail.
- **Mycatisinapiano1528**: Marlon selling back last loop's items, and Cactus Fruit before the desert.
- **ShadowedAciexox**: dried fruit and smoked fish names, and Gil's Trophies asks.
- **ada113**: must-donate-all season gates, and when the Cookbook and Craftbook open.
- **asteriaths**: getting stuck on a second festival visit, and Chests Anywhere in the stash.
- **spenderg**: out-of-season fish in weekly themes, and a controller double press.
- **elaineofshalott**: suggested Keep Fish Pond.
- **amaliekirstine**: festival contests and cutscenes stopping the clock.
- **SilviaVA**: the Dye bundle asking for things a year cannot reach.
- **RiseiJaku**: Zoom Level and UI Scale surviving the rewind.
- **goblinslayer66666**: the Difficulty setting reaching Hard and Extreme.
- **gmastern1**: quitting after a failed season skipping the rewind.
- **pitytheviolins**: bundles asking for a dish from another mod's shop.
- **Tottelotta123**: Garlic Cultivation and its price.
- **khauser13**: egg colors in the quest log, and the Advanced Options screenshot.
- **ggrace67**: weekly goals that ticked without a donation.
- **victoriatauanem**: a Normal-bundles farm switching to custom bundles at the rewind.
- **Ninjamaid**: an Ostrich Mayo ask from another mod.
- **PixxiePerfect**: pointed me at Tech's Cross-Mod Bundles.
- **Nijah, nyxnyx2234, Bumblewyn, IshoMoogoo and lexihope**: reports that shaped the early balance passes.
- **supercam19**: the book art, and the fix that refills mine carts and barrels every loop.
- **cwybabiesucks**: the banner art.
- **u/Gribbleby**: the idea for villagers' déjà vu dialogue.

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

By **sonofskywalker3**. Banner art by **cwybabiesucks**. Book art by **supercam19**. Deja-vu villager dialogue idea by **u/Gribbleby**. Thanks to **Emmalution** for playing it on YouTube and for the feedback that shaped 0.16 and 0.17. Built on [SMAPI](https://smapi.io/) and [HarmonyX](https://github.com/BepInEx/HarmonyX). Stardew Valley is a trademark of ConcernedApe.

## License

Released under the [MIT License](LICENSE).
