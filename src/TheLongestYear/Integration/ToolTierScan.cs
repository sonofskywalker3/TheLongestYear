using System.Collections.Generic;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Every tool the player owns, wherever it sits: the backpack, the tool Clint is
    /// upgrading (Farmer.toolBeingUpgraded, which is NOT in the bag), and every chest, fridge,
    /// mini-fridge and Junimo chest on the player's own maps (the farm, the farmhouse, cabins and
    /// every farm building's interior). Walked when a shrine menu is built and once at reset,
    /// never per tick.</summary>
    internal static class ToolTierScan
    {
        public const string HoeSlug = "hoe", PickaxeSlug = "pickaxe", AxeSlug = "axe",
            WateringCanSlug = "watering_can", RodSlug = "fishing_rod";

        public static string SlugOf(Item item) => item switch
        {
            FishingRod => RodSlug,
            Hoe => HoeSlug,
            Pickaxe => PickaxeSlug,
            Axe => AxeSlug,
            WateringCan => WateringCanSlug,
            _ => null,
        };

        /// <summary>Where a sighting came from, for the reach-snapshot log.</summary>
        public const string BagSource = "bag", ClintSource = "clint";

        public static IEnumerable<(string Slug, Tool Tool, string Source)> OwnedTools(Farmer p)
        {
            if (p == null) yield break;

            foreach (Item it in p.Items)
                if (SlugOf(it) is string slug && it is Tool t)
                    yield return (slug, t, BagSource);

            if (p.toolBeingUpgraded.Value is Tool atClint && SlugOf(atClint) is string clintSlug)
                yield return (clintSlug, atClint, ClintSource);

            foreach (GameLocation loc in OwnedLocations())
            {
                foreach ((string slug, Tool t) in ToolsInChests(loc, p))
                    yield return (slug, t, loc.NameOrUniqueName);
            }
        }

        /// <summary>Slug -> highest UpgradeLevel across <see cref="OwnedTools"/>.</summary>
        public static IReadOnlyDictionary<string, int> Peaks(Farmer p)
            => ToolTierPeaks.Highest(Sightings(p));

        private static IEnumerable<(string, int)> Sightings(Farmer p)
        {
            foreach ((string slug, Tool t, _) in OwnedTools(p))
                yield return (slug, t.UpgradeLevel);
        }

        /// <summary>The maps the player owns: the farm, the farmhouse and cabins, and every
        /// building interior (sheds, barns, the slime hutch...), which only exist on a farm.</summary>
        private static IEnumerable<GameLocation> OwnedLocations()
        {
            var owned = new List<GameLocation>();
            Utility.ForEachLocation(loc =>
            {
                if (loc != null && (loc.IsFarm || loc is FarmHouse || loc.GetParentLocation() != null))
                    owned.Add(loc);
                return true;
            }, includeInteriors: true, includeGenerated: false);
            return owned;
        }

        private static IEnumerable<(string, Tool)> ToolsInChests(GameLocation loc, Farmer p)
        {
            var chests = new List<Chest>();
            foreach (StardewValley.Object obj in loc.Objects.Values)
                if (obj is Chest chest)
                    chests.Add(chest);
            // The kitchen fridge is its own NetRef on the house, not an object on the map.
            if (loc is FarmHouse house && house.fridge.Value != null)
                chests.Add(house.fridge.Value);

            foreach (Chest chest in chests)
            {
                // GetItemsForPlayer resolves the Junimo chest's shared inventory; for a normal
                // chest, fridge or mini-fridge it is the chest's own list.
                IEnumerable<Item> items = chest.GetItemsForPlayer(p.UniqueMultiplayerID);
                if (items == null) continue;
                foreach (Item it in items)
                    if (SlugOf(it) is string slug && it is Tool t)
                        yield return (slug, t);
            }
        }
    }
}
