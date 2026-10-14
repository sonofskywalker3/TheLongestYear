using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// The Animals tab of the upgrade menu (spec 2026-10-09). Holds the animal keeps that used to sit in
/// Efficiency (Keep Horse), Buildings (Keep Pet, the Coop / Barn chains, Keep Silo, Start with) and
/// Carryover (the Herd Book). Ids, prices and gates are unchanged from those tabs, so a save that owns
/// a row keeps it. Keep Fish Pond stays in Buildings: fish, not farm animals.
/// </summary>
public static class AnimalUpgradeRows
{
    /// <summary>Every row of the Animals tab, in the order the tab lists them.</summary>
    public static IEnumerable<UpgradeDefinition> Build(IReadOnlyList<long> herdBookCosts)
    {
        foreach (UpgradeDefinition def in Powers()) yield return def;
        foreach (UpgradeDefinition def in MovedKeeps()) yield return def;
        // Herd Book (spec 2026-09-25): a chain like the Cookbook's, no building gate. A slot whose
        // building keep is not owned yet just waits (the menu says so).
        for (int tier = 1; tier <= UpgradeCatalog.HerdBookMaxTier; tier++)
            yield return new UpgradeDefinition($"{UpgradeCatalog.HerdBookPrefix}{tier}", UpgradeCategory.Animals,
                herdBookCosts[tier - 1], tier == 1 ? null : $"{UpgradeCatalog.HerdBookPrefix}{tier - 1}");
    }

    /// <summary>The animal powers (spec 2026-10-09, prices from its table, Busy Barnyard split per Ruling 2).
    /// Rows that work for any animal come first, then the species rows, then the horse and pet rows.</summary>
    private static IEnumerable<UpgradeDefinition> Powers()
    {
        yield return new UpgradeDefinition(AnimalPowers.MorningRounds, UpgradeCategory.Animals, 600);
        for (int tier = 1; tier <= AnimalPowers.WarmWelcomeTiers; tier++)
            yield return new UpgradeDefinition(AnimalPowers.WarmWelcomePrefix + tier, UpgradeCategory.Animals,
                WarmWelcomeCosts[tier - 1], tier == 1 ? null : AnimalPowers.WarmWelcomePrefix + (tier - 1));
        yield return new UpgradeDefinition(AnimalPowers.QuickGrowth, UpgradeCategory.Animals, 400);
        yield return new UpgradeDefinition(AnimalPowers.FastHatch, UpgradeCategory.Animals, 200);
        // Growing Herd (round 2, 2026-10-09): only a Big or Deluxe Barn allows births (Data/Buildings
        // AllowAnimalPregnancy), and a Deluxe Barn counts as reaching a Big Barn.
        yield return new UpgradeDefinition(AnimalPowers.GrowingHerd, UpgradeCategory.Animals, 400,
            metaRequirement: null, runReachRequirement: "building:Big Barn");
        yield return new UpgradeDefinition(AnimalPowers.SnugBarn, UpgradeCategory.Animals, 250);
        // Busy Coop and Busy Barn (Ruling 1: permanent, no bundle compensation of any kind).
        yield return new UpgradeDefinition(AnimalPowers.BusyCoop, UpgradeCategory.Animals, 600);
        yield return new UpgradeDefinition(AnimalPowers.BusyBarn, UpgradeCategory.Animals, 600);
        yield return new UpgradeDefinition(AnimalPowers.FineFeathers, UpgradeCategory.Animals, 250,
            metaRequirement: "species:" + AnimalPowers.DuckType);
        yield return new UpgradeDefinition(AnimalPowers.LuckyRabbits, UpgradeCategory.Animals, 450,
            metaRequirement: "species:" + AnimalPowers.RabbitType);
        yield return new UpgradeDefinition(AnimalPowers.TruffleNose, UpgradeCategory.Animals, 400,
            metaRequirement: "species:" + AnimalPowers.PigType);
        yield return new UpgradeDefinition(AnimalPowers.SwiftHorse, UpgradeCategory.Animals, 300, AnimalPowers.KeepHorse);
        yield return new UpgradeDefinition(AnimalPowers.HorseFlute, UpgradeCategory.Animals, 350, AnimalPowers.KeepHorse);
        yield return new UpgradeDefinition(AnimalPowers.LoyalPet, UpgradeCategory.Animals, 150,
            metaRequirement: null, runReachRequirement: "pet:1");
    }

    private static readonly long[] WarmWelcomeCosts = { 200, 450, 800 };

    private static IEnumerable<UpgradeDefinition> MovedKeeps()
    {
        // Keep Pet: the player's pet (kind, breed, name, hearts) across loops. Sentimental, so cheap:
        // 50 JP (Jeff, 2026-09-29; was 75). Barn and coop animals do not carry hearts (Herd Book does).
        yield return new UpgradeDefinition("keep_pet", UpgradeCategory.Animals, 50,
            metaRequirement: null, runReachRequirement: "pet:1");

        // Keep Horse is pure carry-over (HorseCarryoverService), so it is only offered once this run
        // has a stable to carry (TODO 2026-08-28).
        yield return new UpgradeDefinition("early_horse", UpgradeCategory.Animals, 450,
            metaRequirement: null, runReachRequirement: "building:Stable");

        // Coop chain: ~5 runs to bank Keep Coop, more to upgrade.
        yield return new UpgradeDefinition("keep_coop", UpgradeCategory.Animals, 600,
            metaRequirement: null, runReachRequirement: "building:Coop");
        yield return new UpgradeDefinition("keep_big_coop", UpgradeCategory.Animals, 1200, "keep_coop",
            metaRequirement: null, runReachRequirement: "building:Big Coop");
        yield return new UpgradeDefinition("keep_deluxe_coop", UpgradeCategory.Animals, 2000, "keep_big_coop",
            metaRequirement: null, runReachRequirement: "building:Deluxe Coop");
        yield return new UpgradeDefinition("keep_barn", UpgradeCategory.Animals, 600,
            metaRequirement: null, runReachRequirement: "building:Barn");
        yield return new UpgradeDefinition("keep_big_barn", UpgradeCategory.Animals, 1200, "keep_barn",
            metaRequirement: null, runReachRequirement: "building:Big Barn");
        yield return new UpgradeDefinition("keep_deluxe_barn", UpgradeCategory.Animals, 2000, "keep_big_barn",
            metaRequirement: null, runReachRequirement: "building:Deluxe Barn");
        // Silo (khauser13 2026-06-11 + Dusklight7 2026-07-05): priced well below the Coop/Barn keeps to
        // match its vanilla cost. It only holds hay, so it sits with the animal buildings.
        yield return new UpgradeDefinition("keep_silo", UpgradeCategory.Animals, 150,
            metaRequirement: null, runReachRequirement: "building:Silo");

        // Start with [animal]: needs the housing keep AND ever having owned the species across runs
        // (MetaState.AnimalSpeciesEverOwned). Coop birds:
        yield return new UpgradeDefinition("start_chicken", UpgradeCategory.Animals, 400, "keep_coop", "species:Chicken");
        yield return new UpgradeDefinition("start_void_chicken", UpgradeCategory.Animals, 600, "keep_coop", "species:VoidChicken");
        yield return new UpgradeDefinition("start_duck", UpgradeCategory.Animals, 500, "keep_big_coop", "species:Duck");
        yield return new UpgradeDefinition("start_dinosaur", UpgradeCategory.Animals, 900, "keep_big_coop", "species:Dinosaur");
        yield return new UpgradeDefinition("start_rabbit", UpgradeCategory.Animals, 700, "keep_deluxe_coop", "species:Rabbit");
        // Barn animals:
        yield return new UpgradeDefinition("start_cow", UpgradeCategory.Animals, 400, "keep_barn", "species:Cow");
        yield return new UpgradeDefinition("start_goat", UpgradeCategory.Animals, 500, "keep_big_barn", "species:Goat");
        yield return new UpgradeDefinition("start_sheep", UpgradeCategory.Animals, 600, "keep_deluxe_barn", "species:Sheep");
        yield return new UpgradeDefinition("start_pig", UpgradeCategory.Animals, 700, "keep_deluxe_barn", "species:Pig");
        // Ostriches live in barns (Data/FarmAnimals House "Barn"), fixed with the Herd Book 2026-09-25.
        yield return new UpgradeDefinition("start_ostrich", UpgradeCategory.Animals, 1500, "keep_barn", "species:Ostrich");
    }
}
