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
        new UpgradeDefinition("backpack_1", UpgradeCategory.Loadout, 150,
            metaRequirement: null, runReachRequirement: "backpack:1"),
        new UpgradeDefinition("backpack_2", UpgradeCategory.Loadout, 375, "backpack_1",
            metaRequirement: null, runReachRequirement: "backpack:2"),

        // Golden Scythe — a convenience keep. Reach-gated on having obtained the Golden
        // Scythe this run (mail "gotGoldenScythe"); once owned it is a permanent floor —
        // FarmerReset grants the Golden Scythe instead of the basic scythe every loop.
        new UpgradeDefinition("keep_golden_scythe", UpgradeCategory.Loadout, 250,
            metaRequirement: null, runReachRequirement: "scythe:golden"),

        // Seed Money — 5-tier chain (2026-05-29 rebalance: was 2-tier +500g/+1500g, now
        // 5-tier with a more generous floor and ceiling per user feedback). Each tier
        // sets the TOTAL starting-gold bonus to its amount (not additive — owning II
        // means +2500g, not +1000+2500). Highest owned tier wins, same as backpack.
        // Costs match the shop-discount curve since both are similar early-run impact.
        new UpgradeDefinition("starter_gold_1", UpgradeCategory.Loadout, 75),
        new UpgradeDefinition("starter_gold_2", UpgradeCategory.Loadout, 175, "starter_gold_1"),
        new UpgradeDefinition("starter_gold_3", UpgradeCategory.Loadout, 350, "starter_gold_2"),
        new UpgradeDefinition("starter_gold_4", UpgradeCategory.Loadout, 600, "starter_gold_3"),
        new UpgradeDefinition("starter_gold_5", UpgradeCategory.Loadout, 900, "starter_gold_4"),

        // (Carryover: hand-authored entries removed in Plan 06A — replaced by the 50
        // programmatically-generated keep_<skill>_level_N entries below.)

        // Carryover — Cookbook + Craftbook (recipe banking across runs).
        // Tier determines the slot pool size. Highest owned tier wins (owning III = 20 slots).
        // Cookbook: gated by kitchen (HouseUpgradeLevel >= 1) at interaction time, not at purchase.
        // Craftbook: no in-run prereq — available from day 1 of the run after purchase.
        new UpgradeDefinition("cookbook_1", UpgradeCategory.Carryover, 150),
        new UpgradeDefinition("cookbook_2", UpgradeCategory.Carryover, 350, "cookbook_1"),
        new UpgradeDefinition("cookbook_3", UpgradeCategory.Carryover, 700, "cookbook_2"),
        new UpgradeDefinition("craftbook_1", UpgradeCategory.Carryover, 150),
        new UpgradeDefinition("craftbook_2", UpgradeCategory.Carryover, 350, "craftbook_1"),
        new UpgradeDefinition("craftbook_3", UpgradeCategory.Carryover, 700, "craftbook_2"),

        // Efficiency
        new UpgradeDefinition("early_horse", UpgradeCategory.Efficiency, 450),

        // Shop Discount — 5-tier chain (2026-05-29 rebalance: was single-tier 5%, now
        // 5/10/15/20/25%. Renamed from shop_discount_5 → shop_discount_1..5; the prior
        // ID's "5" meant the percent and was confusing alongside the chain numbering).
        // Effect applies EVERY run (permanent), not just this run — earlier wording
        // implied single-use. Same cost curve as starter_gold; both are early-run cushion.
        new UpgradeDefinition("shop_discount_1", UpgradeCategory.Efficiency, 75),
        new UpgradeDefinition("shop_discount_2", UpgradeCategory.Efficiency, 175, "shop_discount_1"),
        new UpgradeDefinition("shop_discount_3", UpgradeCategory.Efficiency, 350, "shop_discount_2"),
        new UpgradeDefinition("shop_discount_4", UpgradeCategory.Efficiency, 600, "shop_discount_3"),
        new UpgradeDefinition("shop_discount_5", UpgradeCategory.Efficiency, 900, "shop_discount_4"),

        // JP Boost — 5-tier chain. Multiplies ALL JP-awarding sources (item donations,
        // bundle completions, room completions, weekly theme quests, interim run-end
        // awards). Premium pricing relative to accelerators because the effect compounds:
        // every JP earned after purchase is bigger, including the JP that will fund the
        // next upgrade. Tier 1 (100) is the most expensive starter T1; full chain (3000)
        // is the most expensive Obtainability chain.
        new UpgradeDefinition("jp_boost_1", UpgradeCategory.Efficiency, 100),
        new UpgradeDefinition("jp_boost_2", UpgradeCategory.Efficiency, 250, "jp_boost_1"),
        new UpgradeDefinition("jp_boost_3", UpgradeCategory.Efficiency, 500, "jp_boost_2"),
        new UpgradeDefinition("jp_boost_4", UpgradeCategory.Efficiency, 850, "jp_boost_3"),
        new UpgradeDefinition("jp_boost_5", UpgradeCategory.Efficiency, 1300, "jp_boost_4"),

        // Obtainability
        new UpgradeDefinition("cult_red_cabbage", UpgradeCategory.Obtainability, 750),
        // 2026-05-29 user spec: starfruit decoupled from red cabbage — picker should be able
        // to grab either independently. Cost dropped 1125 → 750 to match cult_red_cabbage
        // ("change the starfruit cost to the same cost as the red cabbage").
        new UpgradeDefinition("cult_starfruit", UpgradeCategory.Obtainability, 750),
        new UpgradeDefinition("fortune_rare_fish", UpgradeCategory.Obtainability, 525),

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
        new UpgradeDefinition("green_thumb_1", UpgradeCategory.Obtainability, 50),
        new UpgradeDefinition("green_thumb_2", UpgradeCategory.Obtainability, 125, "green_thumb_1"),
        new UpgradeDefinition("green_thumb_3", UpgradeCategory.Obtainability, 250, "green_thumb_2"),
        new UpgradeDefinition("green_thumb_4", UpgradeCategory.Obtainability, 425, "green_thumb_3"),
        new UpgradeDefinition("green_thumb_5", UpgradeCategory.Obtainability, 650, "green_thumb_4"),

        // Coal Vein — % chance per destroyed stone to drop +1 coal.
        new UpgradeDefinition("coal_vein_1", UpgradeCategory.Obtainability, 50),
        new UpgradeDefinition("coal_vein_2", UpgradeCategory.Obtainability, 125, "coal_vein_1"),
        new UpgradeDefinition("coal_vein_3", UpgradeCategory.Obtainability, 250, "coal_vein_2"),
        new UpgradeDefinition("coal_vein_4", UpgradeCategory.Obtainability, 425, "coal_vein_3"),
        new UpgradeDefinition("coal_vein_5", UpgradeCategory.Obtainability, 650, "coal_vein_4"),

        // Forager's Eye — % chance per overnight forage spawn to be doubled.
        new UpgradeDefinition("foragers_eye_1", UpgradeCategory.Obtainability, 50),
        new UpgradeDefinition("foragers_eye_2", UpgradeCategory.Obtainability, 125, "foragers_eye_1"),
        new UpgradeDefinition("foragers_eye_3", UpgradeCategory.Obtainability, 250, "foragers_eye_2"),
        new UpgradeDefinition("foragers_eye_4", UpgradeCategory.Obtainability, 425, "foragers_eye_3"),
        new UpgradeDefinition("foragers_eye_5", UpgradeCategory.Obtainability, 650, "foragers_eye_4"),

        // Quick Bite — % faster fish bite per tier. Stacks multiplicatively with the Fishing
        // theme bonus (0.70x): Quick Bite V on Fishing week = 0.70 × 0.75 = ~47.5% sooner total.
        new UpgradeDefinition("quick_bite_1", UpgradeCategory.Obtainability, 50),
        new UpgradeDefinition("quick_bite_2", UpgradeCategory.Obtainability, 125, "quick_bite_1"),
        new UpgradeDefinition("quick_bite_3", UpgradeCategory.Obtainability, 250, "quick_bite_2"),
        new UpgradeDefinition("quick_bite_4", UpgradeCategory.Obtainability, 425, "quick_bite_3"),
        new UpgradeDefinition("quick_bite_5", UpgradeCategory.Obtainability, 650, "quick_bite_4"),

        // Foresight — Weather Sage chain (6 tiers; tier N reveals the next N days, max 6 —
        // today is excluded since its weather is already locked in).
        new UpgradeDefinition("weather_sage_1", UpgradeCategory.Foresight, 150),
        new UpgradeDefinition("weather_sage_2", UpgradeCategory.Foresight, 275, "weather_sage_1"),
        new UpgradeDefinition("weather_sage_3", UpgradeCategory.Foresight, 375, "weather_sage_2"),
        new UpgradeDefinition("weather_sage_4", UpgradeCategory.Foresight, 525, "weather_sage_3"),
        new UpgradeDefinition("weather_sage_5", UpgradeCategory.Foresight, 725, "weather_sage_4"),
        new UpgradeDefinition("weather_sage_6", UpgradeCategory.Foresight, 950, "weather_sage_5"),

        // Foresight — Cart Whisperer (5 tiers). On a cart day the shrine flags which of the
        // Traveling Cart's real stock can feed any CC bundle. Tier N previews 2*N cart slots
        // (CartStockPreview.SlotsToReveal), but the cart's visible stock is capped by the player's
        // cart_slot tier (CartSlotLimitPatch). So each tier is gated on the cart_slot upgrade that
        // makes its full reveal buyable — no paying for a preview wider than the cart can show:
        // cw_N (reveals 2N) requires cart_slot_(2N). Five tiers span the full 10-item cart (cw5 = all
        // 10). The slot gate is a MetaRequirement so the whisper chain (cw1->...->cw5 via
        // PrerequisiteId) stays linear for the shop-leaf UI.
        new UpgradeDefinition("cart_whisper_1", UpgradeCategory.Foresight, 350,
            metaRequirement: "upgrade:cart_slot_2"),
        new UpgradeDefinition("cart_whisper_2", UpgradeCategory.Foresight, 600, "cart_whisper_1",
            metaRequirement: "upgrade:cart_slot_4"),
        new UpgradeDefinition("cart_whisper_3", UpgradeCategory.Foresight, 900, "cart_whisper_2",
            metaRequirement: "upgrade:cart_slot_6"),
        new UpgradeDefinition("cart_whisper_4", UpgradeCategory.Foresight, 1200, "cart_whisper_3",
            metaRequirement: "upgrade:cart_slot_8"),
        new UpgradeDefinition("cart_whisper_5", UpgradeCategory.Foresight, 1500, "cart_whisper_4",
            metaRequirement: "upgrade:cart_slot_10"),

        // Foresight — Cart Stall upgrades. Each tier unlocks one additional visible item slot on
        // the Traveling Cart (base = 1; buying slot_2 gives 2 items per visit, etc.).
        new UpgradeDefinition("cart_slot_2", UpgradeCategory.Foresight, 40),
        new UpgradeDefinition("cart_slot_3", UpgradeCategory.Foresight, 80, "cart_slot_2"),
        new UpgradeDefinition("cart_slot_4", UpgradeCategory.Foresight, 140, "cart_slot_3"),
        new UpgradeDefinition("cart_slot_5", UpgradeCategory.Foresight, 220, "cart_slot_4"),
        new UpgradeDefinition("cart_slot_6", UpgradeCategory.Foresight, 320, "cart_slot_5"),
        new UpgradeDefinition("cart_slot_7", UpgradeCategory.Foresight, 450, "cart_slot_6"),
        new UpgradeDefinition("cart_slot_8", UpgradeCategory.Foresight, 620, "cart_slot_7"),
        new UpgradeDefinition("cart_slot_9", UpgradeCategory.Foresight, 850, "cart_slot_8"),
        new UpgradeDefinition("cart_slot_10", UpgradeCategory.Foresight, 1200, "cart_slot_9"),

        // Stash
        new UpgradeDefinition("stash_1", UpgradeCategory.Stash, 300),
        new UpgradeDefinition("stash_2", UpgradeCategory.Stash, 675, "stash_1"),
        new UpgradeDefinition("stash_3", UpgradeCategory.Stash, 1200, "stash_2"),

        // Buildings — Keep [X] chain. Effects (actually pre-build the structure on run start)
        // are deferred to a later plan; here we only record the entitlement.
        // Coop chain: ~5 runs to bank Keep Coop, more to upgrade.
        new UpgradeDefinition("keep_coop", UpgradeCategory.Buildings, 600,
            metaRequirement: null, runReachRequirement: "building:Coop"),
        new UpgradeDefinition("keep_big_coop", UpgradeCategory.Buildings, 1200, "keep_coop",
            metaRequirement: null, runReachRequirement: "building:Big Coop"),
        new UpgradeDefinition("keep_deluxe_coop", UpgradeCategory.Buildings, 2000, "keep_big_coop",
            metaRequirement: null, runReachRequirement: "building:Deluxe Coop"),
        new UpgradeDefinition("keep_barn", UpgradeCategory.Buildings, 600,
            metaRequirement: null, runReachRequirement: "building:Barn"),
        new UpgradeDefinition("keep_big_barn", UpgradeCategory.Buildings, 1200, "keep_barn",
            metaRequirement: null, runReachRequirement: "building:Big Barn"),
        new UpgradeDefinition("keep_deluxe_barn", UpgradeCategory.Buildings, 2000, "keep_big_barn",
            metaRequirement: null, runReachRequirement: "building:Deluxe Barn"),
        // Silo — requested twice (khauser13 2026-06-11 + Dusklight7 2026-07-05): cheap in vanilla,
        // but its absence from the keep-building options read as an oversight. Priced well below
        // the Coop/Barn keeps to match its vanilla cost (100g + stones vs. thousands).
        new UpgradeDefinition("keep_silo", UpgradeCategory.Buildings, 150,
            metaRequirement: null, runReachRequirement: "building:Silo"),
        new UpgradeDefinition("keep_kitchen", UpgradeCategory.Buildings, 800,
            metaRequirement: null, runReachRequirement: "house:1"),
        new UpgradeDefinition("keep_basement", UpgradeCategory.Buildings, 1800, "keep_kitchen",
            metaRequirement: null, runReachRequirement: "house:3"),
        new UpgradeDefinition("keep_shortcuts", UpgradeCategory.Buildings, 900,
            metaRequirement: null, runReachRequirement: "shortcuts:1"),

        // Keep Pet — preserves the player's pet (kind, breed, name, friendship hearts)
        // across loops. 2026-05-29 spec: sentimental upgrade, not progression-gating —
        // pets don't produce anything you'd ship or donate, so the cost reflects "mostly
        // for feelings" rather than the typical Keep upgrade premium. Barn/coop animals
        // explicitly do NOT carry hearts across loops (see PetCarryoverService remarks).
        new UpgradeDefinition("keep_pet", UpgradeCategory.Buildings, 75,
            metaRequirement: null, runReachRequirement: "pet:1"),

        // Vault — pre-pay all four bus bundles once and keep them paid across resets.
        // Without this upgrade the player has to pay the season's Vault bundle every run by day 28
        // to clear the monthly gate (2,500g Spring, 5,000g Summer, 10,000g Fall, 25,000g Winter).
        new UpgradeDefinition(VaultRules.KeepBusUnlockedId, UpgradeCategory.Buildings, 1500,
            metaRequirement: null, runReachRequirement: "bus:4"),

        // Buildings — Start with [animal]. Requires both the housing upgrade AND ever having
        // owned the species across previous runs (tracked in MetaState.AnimalSpeciesEverOwned).
        // Coop birds:
        new UpgradeDefinition("start_chicken", UpgradeCategory.Buildings, 400, "keep_coop", "species:Chicken"),
        new UpgradeDefinition("start_void_chicken", UpgradeCategory.Buildings, 600, "keep_coop", "species:VoidChicken"),
        new UpgradeDefinition("start_duck", UpgradeCategory.Buildings, 500, "keep_big_coop", "species:Duck"),
        new UpgradeDefinition("start_dinosaur", UpgradeCategory.Buildings, 900, "keep_big_coop", "species:Dinosaur"),
        new UpgradeDefinition("start_rabbit", UpgradeCategory.Buildings, 700, "keep_deluxe_coop", "species:Rabbit"),
        new UpgradeDefinition("start_ostrich", UpgradeCategory.Buildings, 1500, "keep_deluxe_coop", "species:Ostrich"),
        // Barn animals:
        new UpgradeDefinition("start_cow", UpgradeCategory.Buildings, 400, "keep_barn", "species:Cow"),
        new UpgradeDefinition("start_goat", UpgradeCategory.Buildings, 500, "keep_big_barn", "species:Goat"),
        new UpgradeDefinition("start_sheep", UpgradeCategory.Buildings, 600, "keep_deluxe_barn", "species:Sheep"),
        new UpgradeDefinition("start_pig", UpgradeCategory.Buildings, 700, "keep_deluxe_barn", "species:Pig"),
        };
        entries.AddRange(UpgradeCatalogGenerators.LoadoutToolKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverSkillLevelKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverMineElevatorKeeps());
        entries.AddRange(UpgradeCatalogGenerators.CarryoverMasteryKeeps());
        return entries;
    }
}
