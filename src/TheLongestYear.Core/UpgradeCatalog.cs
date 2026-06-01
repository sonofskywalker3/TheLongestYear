using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Hand-authored starter set of permanent upgrades for v1 (spec §11). Effects are applied in a later
/// plan; here we only model the shop-side metadata: id, category, name, description, cost, prerequisite.
///
/// Cost tuning (2026-05-26): every entry was bumped ~1.5× and rounded to the nearest 25. The late-game
/// JP multiplier curve (Fall 2.5×, Winter 4.0×) means a player who reaches Fall/Winter banks JP much
/// faster than the linear ramp implied — bumping costs keeps unlock pacing in line with progression.
/// </summary>
public static class UpgradeCatalog
{
    private static readonly IReadOnlyList<UpgradeDefinition> _all = Build();
    private static readonly IReadOnlyDictionary<string, UpgradeDefinition> _byId =
        _all.ToDictionary(u => u.Id);

    public static IReadOnlyList<UpgradeDefinition> All => _all;

    public static IReadOnlyList<UpgradeDefinition> ByCategory(UpgradeCategory category)
        => _all.Where(u => u.Category == category).ToList();

    public static UpgradeDefinition? TryGet(string id)
        => id != null && _byId.TryGetValue(id, out UpgradeDefinition? u) ? u : null;

    /// <summary>
    /// Total cooking recipe slots granted by the highest owned Cookbook tier.
    /// Tier 0 = no Cookbook purchased = 0 slots. Tier 1 = 5, Tier 2 = 10, Tier 3 = 20.
    /// The highest tier wins — owning II gives 10 slots, not 5+10=15.
    /// </summary>
    public static int CookbookSlotCount(int highestOwnedTier) => highestOwnedTier switch
    {
        1 => 5,
        2 => 10,
        3 => 20,
        _ => 0
    };

    /// <summary>
    /// Total crafting recipe slots granted by the highest owned Craftbook tier.
    /// Same slot counts as <see cref="CookbookSlotCount"/> — mirrors Cookbook by design.
    /// </summary>
    public static int CraftbookSlotCount(int highestOwnedTier) => highestOwnedTier switch
    {
        1 => 5,
        2 => 10,
        3 => 20,
        _ => 0
    };

    private static IReadOnlyList<UpgradeDefinition> Build()
    {
        var entries = new List<UpgradeDefinition>
        {
        // Loadout
        new UpgradeDefinition("backpack_1", UpgradeCategory.Loadout, "Backpack I",
            "Start each run with the 24-slot backpack.", 150,
            metaRequirement: null, runReachRequirement: "backpack:1"),
        new UpgradeDefinition("backpack_2", UpgradeCategory.Loadout, "Backpack II",
            "Start each run with the 36-slot backpack.", 375, "backpack_1",
            metaRequirement: null, runReachRequirement: "backpack:2"),

        // Golden Scythe — a convenience keep. Reach-gated on having obtained the Golden
        // Scythe this run (mail "gotGoldenScythe"); once owned it is a permanent floor —
        // FarmerReset grants the Golden Scythe instead of the basic scythe every loop.
        new UpgradeDefinition("keep_golden_scythe", UpgradeCategory.Loadout, "Keep Golden Scythe",
            "Start each run with the Golden Scythe instead of the basic scythe.", 250,
            metaRequirement: null, runReachRequirement: "scythe:golden"),

        // Seed Money — 5-tier chain (2026-05-29 rebalance: was 2-tier +500g/+1500g, now
        // 5-tier with a more generous floor and ceiling per user feedback). Each tier
        // sets the TOTAL starting-gold bonus to its amount (not additive — owning II
        // means +2500g, not +1000+2500). Highest owned tier wins, same as backpack.
        // Costs match the shop-discount curve since both are similar early-run impact.
        new UpgradeDefinition("starter_gold_1", UpgradeCategory.Loadout, "Seed Money I",
            "Start each run with +1,000g. (Permanent — applies every run.)", 75),
        new UpgradeDefinition("starter_gold_2", UpgradeCategory.Loadout, "Seed Money II",
            "Start each run with +2,500g instead of +1,000g.", 175, "starter_gold_1"),
        new UpgradeDefinition("starter_gold_3", UpgradeCategory.Loadout, "Seed Money III",
            "Start each run with +5,000g.", 350, "starter_gold_2"),
        new UpgradeDefinition("starter_gold_4", UpgradeCategory.Loadout, "Seed Money IV",
            "Start each run with +10,000g.", 600, "starter_gold_3"),
        new UpgradeDefinition("starter_gold_5", UpgradeCategory.Loadout, "Seed Money V",
            "Start each run with +25,000g.", 900, "starter_gold_4"),

        // (Carryover: hand-authored entries removed in Plan 06A — replaced by the 50
        // programmatically-generated keep_<skill>_level_N entries below.)

        // Carryover — Cookbook + Craftbook (recipe banking across runs).
        // Tier determines the slot pool size. Highest owned tier wins (owning III = 20 slots).
        // Cookbook: gated by kitchen (HouseUpgradeLevel >= 1) at interaction time, not at purchase.
        // Craftbook: no in-run prereq — available from day 1 of the run after purchase.
        new UpgradeDefinition("cookbook_1", UpgradeCategory.Carryover, "Cookbook I",
            "Bank up to 5 cooking recipes across runs. The Junimos leave a cookbook on your kitchen counter.",
            150),
        new UpgradeDefinition("cookbook_2", UpgradeCategory.Carryover, "Cookbook II",
            "Expand your cookbook to 10 recipe slots.",
            350, "cookbook_1"),
        new UpgradeDefinition("cookbook_3", UpgradeCategory.Carryover, "Cookbook III",
            "Expand your cookbook to 20 recipe slots.",
            700, "cookbook_2"),
        new UpgradeDefinition("craftbook_1", UpgradeCategory.Carryover, "Craftbook I",
            "Bank up to 5 crafting recipes across runs. The Junimos leave a craftbook on your farmhouse table.",
            150),
        new UpgradeDefinition("craftbook_2", UpgradeCategory.Carryover, "Craftbook II",
            "Expand your craftbook to 10 recipe slots.",
            350, "craftbook_1"),
        new UpgradeDefinition("craftbook_3", UpgradeCategory.Carryover, "Craftbook III",
            "Expand your craftbook to 20 recipe slots.",
            700, "craftbook_2"),

        // Efficiency
        new UpgradeDefinition("early_horse", UpgradeCategory.Efficiency, "Early Horse",
            "Start each run with the horse and stable.", 450),

        // Shop Discount — 5-tier chain (2026-05-29 rebalance: was single-tier 5%, now
        // 5/10/15/20/25%. Renamed from shop_discount_5 → shop_discount_1..5; the prior
        // ID's "5" meant the percent and was confusing alongside the chain numbering).
        // Effect applies EVERY run (permanent), not just this run — earlier wording
        // implied single-use. Same cost curve as starter_gold; both are early-run cushion.
        new UpgradeDefinition("shop_discount_1", UpgradeCategory.Efficiency, "Shop Discount I",
            "5% off all shop purchases. (Permanent — applies every run.)", 75),
        new UpgradeDefinition("shop_discount_2", UpgradeCategory.Efficiency, "Shop Discount II",
            "10% off all shop purchases.", 175, "shop_discount_1"),
        new UpgradeDefinition("shop_discount_3", UpgradeCategory.Efficiency, "Shop Discount III",
            "15% off all shop purchases.", 350, "shop_discount_2"),
        new UpgradeDefinition("shop_discount_4", UpgradeCategory.Efficiency, "Shop Discount IV",
            "20% off all shop purchases.", 600, "shop_discount_3"),
        new UpgradeDefinition("shop_discount_5", UpgradeCategory.Efficiency, "Shop Discount V",
            "25% off all shop purchases.", 900, "shop_discount_4"),

        // JP Boost — 5-tier chain. Multiplies ALL JP-awarding sources (item donations,
        // bundle completions, room completions, weekly theme quests, interim run-end
        // awards). Premium pricing relative to accelerators because the effect compounds:
        // every JP earned after purchase is bigger, including the JP that will fund the
        // next upgrade. Tier 1 (100) is the most expensive starter T1; full chain (3000)
        // is the most expensive Obtainability chain.
        new UpgradeDefinition("jp_boost_1", UpgradeCategory.Efficiency, "Junimo Favor I",
            "All Junimo Point gains increased by 5%. (Permanent — compounds with itself.)", 100),
        new UpgradeDefinition("jp_boost_2", UpgradeCategory.Efficiency, "Junimo Favor II",
            "All Junimo Point gains increased by 10%.", 250, "jp_boost_1"),
        new UpgradeDefinition("jp_boost_3", UpgradeCategory.Efficiency, "Junimo Favor III",
            "All Junimo Point gains increased by 15%.", 500, "jp_boost_2"),
        new UpgradeDefinition("jp_boost_4", UpgradeCategory.Efficiency, "Junimo Favor IV",
            "All Junimo Point gains increased by 20%.", 850, "jp_boost_3"),
        new UpgradeDefinition("jp_boost_5", UpgradeCategory.Efficiency, "Junimo Favor V",
            "All Junimo Point gains increased by 25%.", 1300, "jp_boost_4"),

        // Obtainability
        new UpgradeDefinition("cult_red_cabbage", UpgradeCategory.Obtainability, "Cultivation: Red Cabbage",
            "Red Cabbage can appear from Mixed Seeds in Summer.", 750),
        // 2026-05-29 user spec: starfruit decoupled from red cabbage — picker should be able
        // to grab either independently. Cost dropped 1125 → 750 to match cult_red_cabbage
        // ("change the starfruit cost to the same cost as the red cabbage").
        new UpgradeDefinition("cult_starfruit", UpgradeCategory.Obtainability, "Cultivation: Starfruit",
            "Starfruit can appear from Mixed Seeds in Summer.", 750),
        new UpgradeDefinition("fortune_rare_fish", UpgradeCategory.Obtainability, "Fortune: Rare Fish",
            "Rare fish catch chance increased by 25%.", 525),

        // Obtainability — Passive Accelerators (added 2026-05-29, cost-tuned 2026-05-29)
        //
        // Three 5-tier chains at +5% per tier (max 25%) — cheap early-run upgrades for
        // players still ramping up JP banking. Curve is 50 / 125 / 250 / 425 / 650 per
        // tier (sum 1500 per chain). Tier 1 (50 JP) is now the cheapest entry in the
        // catalog — first-run target is to bank ~100-150 JP and buy 2-3 of these, OR
        // one Keep Copper Pickaxe (150), OR one Seed Money I (75). All three chains are
        // permanent passives that stack with the matching theme-week bonus.
        //
        // 2026-05-29 user balance feedback: "13 JP after a week, hoping for ~100 by end
        // of month" — prior 75-JP tier was on the edge of "feasible after one run"; 50 JP
        // makes the first purchase a sure thing even on a casual first-run pace.

        // Green Thumb — % chance per watered crop per night to gain an extra growth day.
        new UpgradeDefinition("green_thumb_1", UpgradeCategory.Obtainability, "Green Thumb I",
            "Each watered crop has a 5% chance per day to gain an extra day of growth.", 50),
        new UpgradeDefinition("green_thumb_2", UpgradeCategory.Obtainability, "Green Thumb II",
            "Each watered crop has a 10% chance per day to gain an extra day of growth.", 125, "green_thumb_1"),
        new UpgradeDefinition("green_thumb_3", UpgradeCategory.Obtainability, "Green Thumb III",
            "Each watered crop has a 15% chance per day to gain an extra day of growth.", 250, "green_thumb_2"),
        new UpgradeDefinition("green_thumb_4", UpgradeCategory.Obtainability, "Green Thumb IV",
            "Each watered crop has a 20% chance per day to gain an extra day of growth.", 425, "green_thumb_3"),
        new UpgradeDefinition("green_thumb_5", UpgradeCategory.Obtainability, "Green Thumb V",
            "Each watered crop has a 25% chance per day to gain an extra day of growth.", 650, "green_thumb_4"),

        // Coal Vein — % chance per destroyed stone to drop +1 coal.
        new UpgradeDefinition("coal_vein_1", UpgradeCategory.Obtainability, "Coal Vein I",
            "Each stone you destroy has a 5% chance to drop an extra coal.", 50),
        new UpgradeDefinition("coal_vein_2", UpgradeCategory.Obtainability, "Coal Vein II",
            "Each stone you destroy has a 10% chance to drop an extra coal.", 125, "coal_vein_1"),
        new UpgradeDefinition("coal_vein_3", UpgradeCategory.Obtainability, "Coal Vein III",
            "Each stone you destroy has a 15% chance to drop an extra coal.", 250, "coal_vein_2"),
        new UpgradeDefinition("coal_vein_4", UpgradeCategory.Obtainability, "Coal Vein IV",
            "Each stone you destroy has a 20% chance to drop an extra coal.", 425, "coal_vein_3"),
        new UpgradeDefinition("coal_vein_5", UpgradeCategory.Obtainability, "Coal Vein V",
            "Each stone you destroy has a 25% chance to drop an extra coal.", 650, "coal_vein_4"),

        // Forager's Eye — % chance per overnight forage spawn to be doubled.
        new UpgradeDefinition("foragers_eye_1", UpgradeCategory.Obtainability, "Forager's Eye I",
            "Each overnight forage spawn has a 5% chance to be doubled.", 50),
        new UpgradeDefinition("foragers_eye_2", UpgradeCategory.Obtainability, "Forager's Eye II",
            "Each overnight forage spawn has a 10% chance to be doubled.", 125, "foragers_eye_1"),
        new UpgradeDefinition("foragers_eye_3", UpgradeCategory.Obtainability, "Forager's Eye III",
            "Each overnight forage spawn has a 15% chance to be doubled.", 250, "foragers_eye_2"),
        new UpgradeDefinition("foragers_eye_4", UpgradeCategory.Obtainability, "Forager's Eye IV",
            "Each overnight forage spawn has a 20% chance to be doubled.", 425, "foragers_eye_3"),
        new UpgradeDefinition("foragers_eye_5", UpgradeCategory.Obtainability, "Forager's Eye V",
            "Each overnight forage spawn has a 25% chance to be doubled.", 650, "foragers_eye_4"),

        // Quick Bite — % faster fish bite per tier. Stacks multiplicatively with the Fishing
        // theme bonus (0.70x): Quick Bite V on Fishing week = 0.70 × 0.75 = ~47.5% sooner total.
        new UpgradeDefinition("quick_bite_1", UpgradeCategory.Obtainability, "Quick Bite I",
            "Fish bite 5% sooner.", 50),
        new UpgradeDefinition("quick_bite_2", UpgradeCategory.Obtainability, "Quick Bite II",
            "Fish bite 10% sooner.", 125, "quick_bite_1"),
        new UpgradeDefinition("quick_bite_3", UpgradeCategory.Obtainability, "Quick Bite III",
            "Fish bite 15% sooner.", 250, "quick_bite_2"),
        new UpgradeDefinition("quick_bite_4", UpgradeCategory.Obtainability, "Quick Bite IV",
            "Fish bite 20% sooner.", 425, "quick_bite_3"),
        new UpgradeDefinition("quick_bite_5", UpgradeCategory.Obtainability, "Quick Bite V",
            "Fish bite 25% sooner.", 650, "quick_bite_4"),

        // Foresight — Weather Sage chain (7 tiers per spec §11)
        new UpgradeDefinition("weather_sage_1", UpgradeCategory.Foresight, "Weather Sage I",
            "Reveal 1 day of next week's weather on the planning hub.", 150),
        new UpgradeDefinition("weather_sage_2", UpgradeCategory.Foresight, "Weather Sage II",
            "Reveal 2 days of next week's weather.", 275, "weather_sage_1"),
        new UpgradeDefinition("weather_sage_3", UpgradeCategory.Foresight, "Weather Sage III",
            "Reveal 3 days of next week's weather.", 375, "weather_sage_2"),
        new UpgradeDefinition("weather_sage_4", UpgradeCategory.Foresight, "Weather Sage IV",
            "Reveal 4 days of next week's weather.", 525, "weather_sage_3"),
        new UpgradeDefinition("weather_sage_5", UpgradeCategory.Foresight, "Weather Sage V",
            "Reveal 5 days of next week's weather.", 725, "weather_sage_4"),
        new UpgradeDefinition("weather_sage_6", UpgradeCategory.Foresight, "Weather Sage VI",
            "Reveal 6 days of next week's weather.", 950, "weather_sage_5"),
        new UpgradeDefinition("weather_sage_7", UpgradeCategory.Foresight, "Weather Sage VII",
            "Reveal the full 7-day forecast.", 1200, "weather_sage_6"),

        // Foresight — Cart Whisperer (first 3 of 10 tiers in v1; rest in Plan 06)
        new UpgradeDefinition("cart_whisper_1", UpgradeCategory.Foresight, "Cart Whisperer I",
            "Reveal 2 items from this week's Traveling Cart stock.", 200),
        new UpgradeDefinition("cart_whisper_2", UpgradeCategory.Foresight, "Cart Whisperer II",
            "Reveal 4 items from this week's Cart stock.", 350, "cart_whisper_1"),
        new UpgradeDefinition("cart_whisper_3", UpgradeCategory.Foresight, "Cart Whisperer III",
            "Reveal 6 items from this week's Cart stock.", 525, "cart_whisper_2"),

        // Stash
        new UpgradeDefinition("stash_1", UpgradeCategory.Stash, "Junimo Stash I",
            "Expand the Junimo Stash from 4 to 8 slots.", 300),
        new UpgradeDefinition("stash_2", UpgradeCategory.Stash, "Junimo Stash II",
            "Expand the Junimo Stash from 8 to 12 slots.", 675, "stash_1"),
        new UpgradeDefinition("stash_3", UpgradeCategory.Stash, "Junimo Stash III",
            "Expand the Junimo Stash from 12 to 16 slots.", 1200, "stash_2"),

        // Buildings — Keep [X] chain. Effects (actually pre-build the structure on run start)
        // are deferred to a later plan; here we only record the entitlement.
        // Coop chain: ~5 runs to bank Keep Coop, more to upgrade.
        new UpgradeDefinition("keep_coop", UpgradeCategory.Buildings, "Keep Coop",
            "Start each run with a Coop already built.", 600),
        new UpgradeDefinition("keep_big_coop", UpgradeCategory.Buildings, "Keep Big Coop",
            "Start each run with a Big Coop instead of a Coop.", 1200, "keep_coop"),
        new UpgradeDefinition("keep_deluxe_coop", UpgradeCategory.Buildings, "Keep Deluxe Coop",
            "Start each run with a Deluxe Coop.", 2000, "keep_big_coop"),
        new UpgradeDefinition("keep_barn", UpgradeCategory.Buildings, "Keep Barn",
            "Start each run with a Barn already built.", 600),
        new UpgradeDefinition("keep_big_barn", UpgradeCategory.Buildings, "Keep Big Barn",
            "Start each run with a Big Barn.", 1200, "keep_barn"),
        new UpgradeDefinition("keep_deluxe_barn", UpgradeCategory.Buildings, "Keep Deluxe Barn",
            "Start each run with a Deluxe Barn.", 2000, "keep_big_barn"),
        new UpgradeDefinition("keep_kitchen", UpgradeCategory.Buildings, "Keep Kitchen",
            "Start each run with the Kitchen house upgrade (cooking accessible day 1).", 800),
        new UpgradeDefinition("keep_basement", UpgradeCategory.Buildings, "Keep Basement",
            "Start each run with the full L3 farmhouse (kitchen, kids' room, cellar with 33 cask slots) " +
            "and the Cask recipe.", 1800, "keep_kitchen"),
        new UpgradeDefinition("keep_shortcuts", UpgradeCategory.Buildings, "Keep Map Shortcuts",
            "Start each run with all five of Robin's map shortcuts pre-built (Town fence, bus tunnel, " +
            "forest stump bridge, Mountain→Quarry, Mountain→Town).", 900),

        // Keep Pet — preserves the player's pet (kind, breed, name, friendship hearts)
        // across loops. 2026-05-29 spec: sentimental upgrade, not progression-gating —
        // pets don't produce anything you'd ship or donate, so the cost reflects "mostly
        // for feelings" rather than the typical Keep upgrade premium. Barn/coop animals
        // explicitly do NOT carry hearts across loops (see PetCarryoverService remarks).
        new UpgradeDefinition("keep_pet", UpgradeCategory.Buildings, "Keep Pet",
            "Your pet returns at the start of every loop with its name and friendship hearts intact.", 75),

        // Vault — pre-pay all four bus bundles once and keep them paid across resets.
        // Without this upgrade the player has to pay the season's Vault bundle every run by day 28
        // to clear the monthly gate (2,500g Spring, 5,000g Summer, 10,000g Fall, 25,000g Winter).
        new UpgradeDefinition(VaultRules.KeepBusUnlockedId, UpgradeCategory.Buildings, "Keep Bus Unlocked",
            "Start each run with all four Vault bundles already paid — the bus is restored from day 1.",
            1500),

        // Buildings — Start with [animal]. Requires both the housing upgrade AND ever having
        // owned the species across previous runs (tracked in MetaState.AnimalSpeciesEverOwned).
        // Coop birds:
        new UpgradeDefinition("start_chicken", UpgradeCategory.Buildings, "Start with Chicken",
            "Start each run with a Chicken in your Coop.", 400, "keep_coop", "species:Chicken"),
        new UpgradeDefinition("start_void_chicken", UpgradeCategory.Buildings, "Start with Void Chicken",
            "Start each run with a Void Chicken (any Coop tier).", 600, "keep_coop", "species:VoidChicken"),
        new UpgradeDefinition("start_duck", UpgradeCategory.Buildings, "Start with Duck",
            "Start each run with a Duck (Big Coop or better).", 500, "keep_big_coop", "species:Duck"),
        new UpgradeDefinition("start_dinosaur", UpgradeCategory.Buildings, "Start with Dinosaur",
            "Start each run with a Dinosaur (Big Coop or better).", 900, "keep_big_coop", "species:Dinosaur"),
        new UpgradeDefinition("start_rabbit", UpgradeCategory.Buildings, "Start with Rabbit",
            "Start each run with a Rabbit (Deluxe Coop).", 700, "keep_deluxe_coop", "species:Rabbit"),
        new UpgradeDefinition("start_ostrich", UpgradeCategory.Buildings, "Start with Ostrich",
            "Start each run with an Ostrich (Deluxe Coop).", 1500, "keep_deluxe_coop", "species:Ostrich"),
        // Barn animals:
        new UpgradeDefinition("start_cow", UpgradeCategory.Buildings, "Start with Cow",
            "Start each run with a Cow in your Barn.", 400, "keep_barn", "species:Cow"),
        new UpgradeDefinition("start_goat", UpgradeCategory.Buildings, "Start with Goat",
            "Start each run with a Goat (Big Barn or better).", 500, "keep_big_barn", "species:Goat"),
        new UpgradeDefinition("start_sheep", UpgradeCategory.Buildings, "Start with Sheep",
            "Start each run with a Sheep (Deluxe Barn).", 600, "keep_deluxe_barn", "species:Sheep"),
        new UpgradeDefinition("start_pig", UpgradeCategory.Buildings, "Start with Pig",
            "Start each run with a Pig (Deluxe Barn).", 700, "keep_deluxe_barn", "species:Pig"),
        };
        entries.AddRange(UpgradeCatalogGenerators.LoadoutToolKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverSkillLevelKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverMineElevatorKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverMasteryKeeps());
        return entries;
    }
}
