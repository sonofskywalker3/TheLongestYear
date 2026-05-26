using System.Linq;
using StardewValley;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>Captures a <see cref="WorldFingerprint"/> from the live game for the leak test.</summary>
    internal static class WorldStateProbe
    {
        public static WorldFingerprint Capture()
        {
            Farmer p = Game1.player;

            int crops = 0, placedObjects = 0, buildings = 0;
            foreach (GameLocation loc in Game1.locations)
            {
                placedObjects += loc.objects.Count();
                buildings += loc.buildings.Count;
                foreach (TerrainFeature tf in loc.terrainFeatures.Values)
                    if (tf is HoeDirt hd && hd.crop != null)
                        crops++;
            }

            int totalXp = 0;
            for (int i = 0; i < p.experiencePoints.Count; i++)
                totalXp += p.experiencePoints[i];

            int completedBundles = Game1.netWorldState.Value.Bundles.Pairs
                .Count(kvp => kvp.Value.All(done => done));

            return new WorldFingerprint
            {
                Year = Game1.year,
                Season = (CoreSeason)(int)Game1.season,
                DayOfMonth = Game1.dayOfMonth,
                Money = p.Money,
                Stamina = (int)p.stamina,
                InventoryItemCount = p.Items.Count(it => it != null),
                TotalSkillXp = totalXp,
                CropCount = crops,
                PlacedObjectCount = placedObjects,
                BuildingCount = buildings,
                CompletedBundleCount = completedBundles,
                FriendshipCount = p.friendshipData.Count(),
                MailReceivedCount = p.mailReceived.Count(),
                EventsSeenCount = p.eventsSeen.Count(),
                LowestMineLevel = Game1.netWorldState.Value.LowestMineLevelForOrder
            };
        }
    }
}
