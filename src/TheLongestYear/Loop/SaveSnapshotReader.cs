using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>Reads what the real save has into a <see cref="SaveSnapshot"/> for the fairness
    /// rule (spec 2026-09-15 Part B, 1.1). Host only; read once per night pass.</summary>
    internal static class SaveSnapshotReader
    {
        private static readonly string[] SkillNames = { "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck" };
        private const string BeachBridgeFixed = "beachBridgeFixed";

        /// <summary><paramref name="log"/> takes one trace line when a recipe had to be skipped, so a
        /// content pack with broken recipe data shows up in the log instead of going silent.</summary>
        public static SaveSnapshot Read(Action<string> log = null)
        {
            Farmer player = Game1.player;
            if (player == null) return SaveSnapshot.Empty;

            var recipes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in player.cookingRecipes.Keys) recipes.Add(name);
            foreach (string name in player.craftingRecipes.Keys) recipes.Add(name);

            // What the known crafting recipes make, by qualified id, so a missing keg can cost a day
            // of crafting on Normal rather than rule the route out.
            var craftable = new HashSet<string>(StringComparer.Ordinal);
            int skippedRecipes = 0;
            foreach (string name in player.craftingRecipes.Keys)
            {
                try
                {
                    Item made = new CraftingRecipe(name, false).createItem();
                    if (made != null) craftable.Add(made.QualifiedItemId);
                }
                catch (Exception ex) when (ex is KeyNotFoundException or NullReferenceException or ArgumentException)
                {
                    // A content mod's recipe with no item data: skip it, it cannot be a machine we
                    // need. Counted rather than swallowed, and reported once after the loop.
                    skippedRecipes++;
                }
            }
            if (skippedRecipes > 0)
                log?.Invoke($"Save snapshot: skipped {skippedRecipes} crafting recipe(s) with no item data.");

            var buildings = new HashSet<string>(StringComparer.Ordinal);
            var animals = new HashSet<string>(StringComparer.Ordinal);
            var friendship = new Dictionary<string, int>(StringComparer.Ordinal);
            Farm farm = Game1.getFarm();
            if (farm != null)
            {
                foreach (Building b in farm.buildings)
                {
                    // A Big Coop stands in for a Coop: walk the upgrade chain down (BuildingData.BuildingToUpgrade).
                    string type = b.buildingType.Value;
                    int guard = 0;
                    while (!string.IsNullOrEmpty(type) && buildings.Add(type) && guard++ < 8)
                        type = Game1.buildingData != null && Game1.buildingData.TryGetValue(type, out BuildingData data) ? data.BuildingToUpgrade : null;
                }
                foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                {
                    string type = animal.type.Value;
                    animals.Add(type);
                    int f = animal.friendshipTowardFarmer.Value;
                    if (!friendship.TryGetValue(type, out int best) || f > best) friendship[type] = f;
                }
            }

            var machines = new HashSet<string>(StringComparer.Ordinal);
            Utility.ForEachLocation(loc =>
            {
                foreach (StardewValley.Object obj in loc.objects.Values)
                {
                    if (obj.bigCraftable.Value) machines.Add(obj.QualifiedItemId);
                    if (obj is Chest chest)
                        foreach (Item item in chest.Items)
                            if (item is StardewValley.Object o && o.bigCraftable.Value) machines.Add(o.QualifiedItemId);
                }
                return true;
            });
            foreach (Item item in player.Items)
                if (item is StardewValley.Object o && o.bigCraftable.Value) machines.Add(o.QualifiedItemId);

            var mail = new HashSet<string>(StringComparer.Ordinal);
            foreach (string flag in player.mailReceived) mail.Add(flag);
            foreach (string flag in Game1.MasterPlayer.mailReceived) mail.Add(flag);

            // The beach bridge is world state, not mail; the fairness rule reads both as one flag set.
            if (NetWorldState.checkAnywhereForWorldStateID(BeachBridgeFixed)) mail.Add(BeachBridgeFixed);

            var skills = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < SkillNames.Length; i++) skills[SkillNames[i]] = player.GetSkillLevel(i);

            int floor = Game1.netWorldState?.Value == null ? 0 : MineShaft.lowestLevelReached;
            return new SaveSnapshot(recipes, buildings, machines, craftable, animals, friendship, mail, floor, skills);
        }
    }
}
