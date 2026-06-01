using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Pure builder that derives a <see cref="RunBaseline"/> from <see cref="MetaState"/> (banked
/// upgrades), <see cref="RunState"/> (per-run peak for mine floor), and <see cref="PlayerSnapshot"/>
/// (per-run peaks for tool tiers + skill levels). All three peak sources feed the cap-not-grant
/// rule: a keep upgrade only restores what the player actually achieved this run. No game refs.
/// </summary>
public static class RunBaselineBuilder
{
    // Skill indexes — match Farmer.farmingSkill/etc. constants in the decompile
    // (StardewValley\StardewValley\Farmer.cs:85-95).
    private const int Farming = 0;
    private const int Fishing = 1;
    private const int Foraging = 2;
    private const int Mining = 3;
    private const int Combat = 4;

    private static readonly (string Slug, int Skill)[] SkillSlugs =
    {
        ("farming",  Farming),
        ("fishing",  Fishing),
        ("foraging", Foraging),
        ("mining",   Mining),
        ("combat",   Combat),
    };

    private static readonly string[] ToolSlugs = { "hoe", "pickaxe", "axe", "watering_can" };

    // Coop and Barn chains. Each entry: (keep_id, building blueprint name). The highest
    // owned in each chain wins.
    private static readonly (string UpgradeId, string Blueprint)[] CoopChain =
    {
        ("keep_coop",        "Coop"),
        ("keep_big_coop",    "Big Coop"),
        ("keep_deluxe_coop", "Deluxe Coop"),
    };

    private static readonly (string UpgradeId, string Blueprint)[] BarnChain =
    {
        ("keep_barn",        "Barn"),
        ("keep_big_barn",    "Big Barn"),
        ("keep_deluxe_barn", "Deluxe Barn"),
    };

    // start_<id> → (vanilla FarmAnimal type, required housing blueprint).
    // The vanilla types come from Data/FarmAnimals — verify each before shipping.
    private static readonly Dictionary<string, (string VanillaType, string HousingType)> StartingAnimalMap =
        new()
        {
            ["start_chicken"]       = ("White Chicken", "Coop"),
            ["start_void_chicken"]  = ("Void Chicken",  "Coop"),
            ["start_duck"]          = ("Duck",          "Big Coop"),
            ["start_dinosaur"]      = ("Dinosaur",      "Big Coop"),
            ["start_rabbit"]        = ("Rabbit",        "Deluxe Coop"),
            ["start_ostrich"]       = ("Ostrich",       "Deluxe Coop"),
            ["start_cow"]           = ("White Cow",     "Barn"),
            ["start_goat"]          = ("Goat",          "Big Barn"),
            ["start_sheep"]         = ("Sheep",         "Deluxe Barn"),
            ["start_pig"]           = ("Pig",           "Deluxe Barn"),
        };

    public static RunBaseline Build(MetaState meta, RunState run, PlayerSnapshot peaks, int defaultStartingMoney)
    {
        // Seed Money — 5-tier chain, highest owned tier wins (the dollar amount is the
        // total bonus, not additive across tiers). Values bumped 2026-05-29 per user
        // "more generous" feedback: 500/1500 → 1000/2500/5000/10000/25000.
        int gold = defaultStartingMoney
            + (meta.HasUpgrade("starter_gold_5") ? 25000
               : meta.HasUpgrade("starter_gold_4") ? 10000
               : meta.HasUpgrade("starter_gold_3") ? 5000
               : meta.HasUpgrade("starter_gold_2") ? 2500
               : meta.HasUpgrade("starter_gold_1") ? 1000
               : 0);

        int maxItems = meta.HasUpgrade("backpack_2") ? 36
                      : meta.HasUpgrade("backpack_1") ? 24
                      : 12;

        // Tool tiers (basic 4) — owned-tier capped at in-run peak. Values are written
        // as the literal UpgradeLevel the apply code should set on the Tool: 1=Copper,
        // 2=Steel, 3=Gold, 4=Iridium. Zero means "no keep written" (vanilla rusty).
        var toolTiers = new Dictionary<string, int>();
        foreach (string slug in ToolSlugs)
        {
            int owned = meta.HighestKeptTier($"keep_{slug}_", maxTier: 4);
            if (owned <= 0) continue;
            int peak = peaks.ToolTiers.TryGetValue(slug, out int p) ? p : 0;
            int capped = System.Math.Min(owned, peak);
            if (capped > 0)
                toolTiers[slug] = capped;
        }

        // Fishing rod — explicit keep-id → UpgradeLevel mapping (HighestKeptTier can't see the
        // tier-0 Bamboo keep). 1=bamboo, 2=fiberglass, 3=iridium = FishingRod.UpgradeLevel.
        int rodUpgradeLevel =
            meta.HasUpgrade("keep_fishing_rod_2") ? 3 :
            meta.HasUpgrade("keep_fishing_rod_1") ? 2 :
            meta.HasUpgrade("keep_fishing_rod_0") ? 1 : 0;
        if (rodUpgradeLevel > 0)
        {
            int rodPeak = peaks.ToolTiers.TryGetValue("fishing_rod", out int rp) ? rp : 0;
            int rodCapped = System.Math.Min(rodUpgradeLevel, rodPeak);
            if (rodCapped > 0)
                toolTiers["fishing_rod"] = rodCapped;
        }

        // Skill levels + profession re-pick queue — capped at in-run peak per skill.
        var skillLevels = new Dictionary<int, int>();
        var requeue = new List<int>();
        foreach (var (slug, skillIndex) in SkillSlugs)
        {
            int owned = meta.HighestKeptTier($"keep_{slug}_level_", maxTier: 10);
            if (owned <= 0) continue;
            int peak = peaks.SkillLevels.TryGetValue(skillIndex, out int sp) ? sp : 0;
            int capped = System.Math.Min(owned, peak);
            if (capped <= 0) continue;

            skillLevels[skillIndex] = capped;
            // Profession picker fires for the L5 and L10 thresholds that the CAPPED
            // level actually crosses. Owning keep_farming_level_5 but capped to L4
            // means no profession picker for Farming this reset.
            if (capped >= 5)
                requeue.Add(skillIndex);
        }

        // Mine elevator floor (cap-not-grant against in-run peak from RunState).
        int ownedFloor = HighestOwnedMineFloor(meta);
        int cappedFloor = System.Math.Min(ownedFloor, FloorDown10(run.PeakMineFloor));

        // Kept buildings — highest tier per chain wins
        var keptBuildings = new List<string>();
        AddTopOfChain(meta, CoopChain, keptBuildings);
        AddTopOfChain(meta, BarnChain, keptBuildings);

        // Starting animals — every owned start_<species> goes in (the prerequisite chain
        // already enforces the matching housing was bought, so the housing is in keptBuildings).
        var startingAnimals = new List<StartingAnimal>();
        foreach (var (upgradeId, mapping) in StartingAnimalMap)
        {
            if (meta.HasUpgrade(upgradeId))
                startingAnimals.Add(new StartingAnimal(mapping.VanillaType, mapping.HousingType));
        }

        return new RunBaseline
        {
            StartingGold = gold,
            MaxItems = maxItems,
            ToolTiers = toolTiers,
            SkillLevels = skillLevels,
            ProfessionPickerSkillsToRequeue = requeue,
            MineElevatorFloor = cappedFloor,
            KitchenOnDay1 = meta.HasUpgrade("keep_kitchen"),
            BasementOnDay1 = meta.HasUpgrade("keep_basement"),
            ShortcutsUnlocked = meta.HasUpgrade("keep_shortcuts"),
            BusUnlocked = meta.HasUpgrade(VaultRules.KeepBusUnlockedId),
            EarlyHorse = meta.HasUpgrade("early_horse"),
            KeptBuildings = keptBuildings,
            StartingAnimals = startingAnimals,
            MasteryLevel = MasteryFloor(meta),
            GrantGoldenScythe = meta.HasUpgrade("keep_golden_scythe"),
        };
    }

    // Floors come in steps of 10 (keep_mine_elevator_10/20/30/…). Round PeakMineFloor
    // DOWN to the nearest 10 so a player who reached floor 47 with keep_50 owned ends up
    // at floor 40, not 47 (the elevator only stops at multiples of 5 in vanilla anyway —
    // 5/10/15/… — but our keep tiers are documented as 10-step).
    private static int FloorDown10(int floor) => (floor / 10) * 10;

    private static int HighestOwnedMineFloor(MetaState meta)
    {
        int best = 0;
        for (int floor = 10; floor <= 120; floor += 10)
            if (meta.HasUpgrade($"keep_mine_elevator_{floor}"))
                best = floor;
        return best;
    }

    private static void AddTopOfChain(
        MetaState meta,
        (string UpgradeId, string Blueprint)[] chain,
        List<string> output)
    {
        // Iterate from highest to lowest, return first hit.
        for (int i = chain.Length - 1; i >= 0; i--)
            if (meta.HasUpgrade(chain[i].UpgradeId))
            {
                output.Add(chain[i].Blueprint);
                return;
            }
    }

    // Highest owned keep_mastery_N (1..5). Permanent floor — NOT capped at in-run peak,
    // unlike skill/tool keeps (mastery is hard-won end-game progression).
    private static int MasteryFloor(MetaState meta)
    {
        int best = 0;
        for (int n = 1; n <= 5; n++)
            if (meta.HasUpgrade($"keep_mastery_{n}"))
                best = n;
        return best;
    }
}
