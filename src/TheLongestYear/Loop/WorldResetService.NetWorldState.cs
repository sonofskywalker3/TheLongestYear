using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.UI;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    internal sealed partial class WorldResetService
    {
        // Vanilla's Traveling Cart year-1 guarantee window, transcribed from Game1.loadForNewGame
        // (Game1.cs:4212-4218): seed = uniqueIDForThisGame * 12, window = random.Next(2, ((16-2)*2)+3).
        private const double Y1GuaranteeSeedFactor = 12.0;
        private const int Y1GuaranteeMinVisits = 2;
        private const int Y1GuaranteeMaxVisits = 31;

        // The rest of the NetWorldState wipe-by-default pass (audit 2026-07-13). Rulings:
        // WIPE = run progression that must rewind with the year (island/walnut chain, raccoon
        // chain, perfection counters, Shrine-of-Challenge difficulty, in-flight Robin/Wizard
        // builds, world-state flags, daily ephemera). KEEP = save-level configuration or
        // session state (whichFarm/whichModFarm, ShuffleMineChests remix option, player
        // limits/privacy/farmhands, pause flags, LocationsWithBuildings — maintained by the
        // engine, DishOfTheDay — re-rolled by loadForNewGame). Calendar, weather, bundles,
        // museum, lost books, and mine levels are handled in their own steps above/below.
        private void ResetNetWorldStateLeftovers()
        {
            NetWorldState ws = Game1.netWorldState.Value;

            // Ginger Island / golden-walnut chain.
            ws.GoldenWalnuts = 0;
            ws.GoldenWalnutsFound = 0;
            ws.GoldenCoconutCracked = false;
            ws.FoundBuriedNuts.Clear();
            ws.IslandVisitors.Clear();
            ws.ParrotPlatformsUnlocked = false;
            ws.ActivatedGoldenParrot = false;
            ws.IsGoblinRemoved = false;
            ws.IsSubmarineLocked = false;

            // Perfection-adjacent + misc progression counters.
            ws.MiniShippingBinsObtained = 0;
            ws.PerfectionWaivers = 0;
            ws.TreasureTotemsUsed = 0;

            // Raccoon chain (giant stump requests).
            ws.TimesFedRaccoons = 0;
            ws.SeasonOfCurrentRacconBundle = -1;
            ws.DaysPlayedWhenLastRaccoonBundleWasFinished = 0;
            for (int i = 0; i < ws.raccoonBundles.Count; i++)
                ws.raccoonBundles[i] = false;

            // Shrine of Challenge / hard-mode toggles.
            ws.MinesDifficulty = 0;
            ws.SkullCavesDifficulty = 0;

            // In-flight Robin/Wizard constructions reference buildings the world wipe just
            // deleted — same class as Clint's toolBeingUpgraded in FarmerReset.
            ws.Builders.Clear();

            // World-state flags (trash bear, one-time map states, …) mirror the static
            // Game1.worldStateIDs set; clear both sides so nothing re-syncs back.
            foreach (string id in Game1.worldStateIDs.ToArray())
                ws.removeWorldStateID(id);
            Game1.worldStateIDs.Clear();

            // Daily ephemera the skipped vanilla day-start would have refreshed.
            ws.ActivePassiveFestivals.Clear();
            ws.CheckedGarbage.Clear();
            ws.canDriveYourselfToday.Value = false;
            ws.goldenClocksTurnedOff.Value = false;

            // Dish of the Day. Corrects the 2026-07-13 ruling, which kept it on the reasoning that
            // loadForNewGame re-rolls it — it does not. Game1.UpdateDishOfTheDay (Game1.cs:9432) is
            // reached only from the _newDayAfterFade night chain, which the rewind skips entirely, so
            // Gus opened Spring 1 still selling the dish from the day the reset fired. A real vanilla
            // Spring 1 has NO dish of the day for the same reason, and every consumer null-checks it
            // (ItemQueryResolver.cs:145, DefaultPhoneHandler.cs:394), so null is both the safe value
            // and the vanilla-accurate one. Day 2's night pass rolls the new run's first dish.
            ws.DishOfTheDay = null;

            // Traveling Cart year-1 red-cabbage guarantee. Corrects the 2026-07-13 ruling, which
            // set this to -1 as "the new-game sentinel" — it is not. -1 means the guarantee is
            // DISABLED: both consumers gate on `>= 0` before decrementing (Forest.cs:763,
            // Game1.cs:9020). Vanilla rolls the window once, at save creation, and only when the
            // YearOneCompletable new-game option is on (Game1.cs:4204-4219); loadForNewGame cannot
            // re-roll it during a reset because newGameSetupOptions is not populated outside the
            // new-game flow. So on a YearOneCompletable save the old line permanently killed the
            // guarantee at the first rewind. Re-roll the window with vanilla's own formula off the
            // freshly re-seeded uniqueIDForThisGame instead, and only when the save had it armed —
            // a save that never enabled the option sits at -1 and is left alone.
            if (ws.VisitsUntilY1Guarantee >= 0)
                ws.VisitsUntilY1Guarantee =
                    Utility.CreateRandom((double)Game1.uniqueIDForThisGame * Y1GuaranteeSeedFactor)
                        .Next(Y1GuaranteeMinVisits, Y1GuaranteeMaxVisits);

            _monitor.Log("In-place reset: netWorldState leftovers wiped (island/walnuts, raccoons, " +
                "perfection counters, mine difficulty, builders, world flags, daily ephemera, " +
                "dish of the day).",
                LogLevel.Trace);
        }
    }
}
