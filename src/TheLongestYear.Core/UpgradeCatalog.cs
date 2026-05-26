using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Hand-authored starter set of permanent upgrades for v1 (spec §11). Effects are applied in a later
/// plan; here we only model the shop-side metadata: id, category, name, description, cost, prerequisite.
/// Costs are round numbers — balance tuning is a Plan 06 calibration pass.
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

    private static IReadOnlyList<UpgradeDefinition> Build() => new List<UpgradeDefinition>
    {
        // Loadout
        new UpgradeDefinition("backpack_1", UpgradeCategory.Loadout, "Backpack I",
            "Start each run with the 24-slot backpack.", 100),
        new UpgradeDefinition("backpack_2", UpgradeCategory.Loadout, "Backpack II",
            "Start each run with the 36-slot backpack.", 250, "backpack_1"),
        new UpgradeDefinition("starter_gold_1", UpgradeCategory.Loadout, "Seed Money I",
            "Start each run with +500g.", 75),
        new UpgradeDefinition("starter_gold_2", UpgradeCategory.Loadout, "Seed Money II",
            "Start each run with +1500g (instead of +500g).", 200, "starter_gold_1"),

        // Carryover
        new UpgradeDefinition("carry_xp_25", UpgradeCategory.Carryover, "Carryover XP I",
            "Retain 25% of your peak skill XP across runs.", 150),
        new UpgradeDefinition("carry_xp_50", UpgradeCategory.Carryover, "Carryover XP II",
            "Retain 50% of your peak skill XP across runs.", 400, "carry_xp_25"),

        // Efficiency
        new UpgradeDefinition("early_horse", UpgradeCategory.Efficiency, "Early Horse",
            "Start each run with the horse and stable.", 300),
        new UpgradeDefinition("shop_discount_5", UpgradeCategory.Efficiency, "Shop Discount I",
            "5% off all shop purchases this run.", 175),

        // Obtainability
        new UpgradeDefinition("cult_red_cabbage", UpgradeCategory.Obtainability, "Cultivation: Red Cabbage",
            "Red Cabbage can appear from Mixed Seeds in Summer.", 500),
        new UpgradeDefinition("cult_starfruit", UpgradeCategory.Obtainability, "Cultivation: Starfruit",
            "Starfruit can appear from Mixed Seeds in Summer.", 750, "cult_red_cabbage"),
        new UpgradeDefinition("fortune_rare_fish", UpgradeCategory.Obtainability, "Fortune: Rare Fish",
            "Rare fish catch chance increased by 25%.", 350),

        // Foresight — Weather Sage chain (7 tiers per spec §11)
        new UpgradeDefinition("weather_sage_1", UpgradeCategory.Foresight, "Weather Sage I",
            "Reveal 1 day of next week's weather on the planning hub.", 100),
        new UpgradeDefinition("weather_sage_2", UpgradeCategory.Foresight, "Weather Sage II",
            "Reveal 2 days of next week's weather.", 175, "weather_sage_1"),
        new UpgradeDefinition("weather_sage_3", UpgradeCategory.Foresight, "Weather Sage III",
            "Reveal 3 days of next week's weather.", 250, "weather_sage_2"),
        new UpgradeDefinition("weather_sage_4", UpgradeCategory.Foresight, "Weather Sage IV",
            "Reveal 4 days of next week's weather.", 350, "weather_sage_3"),
        new UpgradeDefinition("weather_sage_5", UpgradeCategory.Foresight, "Weather Sage V",
            "Reveal 5 days of next week's weather.", 475, "weather_sage_4"),
        new UpgradeDefinition("weather_sage_6", UpgradeCategory.Foresight, "Weather Sage VI",
            "Reveal 6 days of next week's weather.", 625, "weather_sage_5"),
        new UpgradeDefinition("weather_sage_7", UpgradeCategory.Foresight, "Weather Sage VII",
            "Reveal the full 7-day forecast.", 800, "weather_sage_6"),

        // Foresight — Cart Whisperer (first 3 of 10 tiers in v1; rest in Plan 06)
        new UpgradeDefinition("cart_whisper_1", UpgradeCategory.Foresight, "Cart Whisperer I",
            "Reveal 2 items from this week's Traveling Cart stock.", 125),
        new UpgradeDefinition("cart_whisper_2", UpgradeCategory.Foresight, "Cart Whisperer II",
            "Reveal 4 items from this week's Cart stock.", 225, "cart_whisper_1"),
        new UpgradeDefinition("cart_whisper_3", UpgradeCategory.Foresight, "Cart Whisperer III",
            "Reveal 6 items from this week's Cart stock.", 350, "cart_whisper_2"),

        // Stash
        new UpgradeDefinition("stash_1", UpgradeCategory.Stash, "Junimo Stash I",
            "Unlock the Junimo Stash with 4 slots (carries items across runs).", 200),
        new UpgradeDefinition("stash_2", UpgradeCategory.Stash, "Junimo Stash II",
            "Expand the Junimo Stash to 8 slots.", 450, "stash_1")
    };
}
