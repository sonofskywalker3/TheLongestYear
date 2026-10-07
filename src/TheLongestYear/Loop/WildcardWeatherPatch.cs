using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wildcard snow_day (spec section 8): snow for today, applied at the reveal on the wildcard
    /// morning. Vanilla already ran <c>UpdateWeatherForNewDay</c> / <c>ApplyWeatherForNewDay</c>
    /// for today, so this writes today's state in both places the game reads it: the "Default"
    /// context's <see cref="LocationWeather"/> (what <c>GameLocation.IsSnowingHere</c> reads and
    /// <c>NetWorldState.UpdateFromGame1</c> overwrites from the statics) and the <c>Game1.is*</c>
    /// statics (drawing, music). Tomorrow's forecast (<c>WeatherForTomorrow</c>, the schedule
    /// writer's slot) is never touched, so the snow lasts the one day. Snow never waters: any rain
    /// watering already happened at day start, and the overnight crop skip
    /// (<see cref="WildcardSnowNightCropPatch"/>) means nothing outdoors grows that night anyway.
    /// </summary>
    internal static class WildcardWeatherPatch
    {
        private const string DefaultContext = "Default";
        private const string RainTrack = "rain";
        private const string NoTrack = "none";

        /// <summary>Make today snowy. Idempotent (the load path calls it again).</summary>
        public static void ApplySnow(IMonitor monitor)
        {
            if (!RunActivation.IsActive || !DayEffects.Has(WildcardSchedule.SnowDay)) return;
            if (Game1.IsMasterGame)
            {
                LocationWeather weather = Game1.netWorldState.Value.GetWeatherForLocation(DefaultContext);
                weather.Weather = Game1.weather_snow;
                weather.IsGreenRain = false;   // before IsRaining: the green-rain setter can raise it
                weather.IsRaining = false;
                weather.IsLightning = false;
                weather.IsDebrisWeather = false;
                weather.IsSnowing = true;
            }
            Game1.isGreenRain = false;
            Game1.isRaining = false;
            Game1.isLightning = false;
            Game1.isDebrisWeather = false;
            Game1.isSnowing = true;
            Game1.updateWeatherIcon();
            // A rain morning starts the rain ambience; it would keep playing over the snow.
            if (Game1.getMusicTrackName() == RainTrack)
                Game1.changeMusicTrack(NoTrack, track_interruptable: true);
            monitor.Log("Wildcard snow day: today's weather set to snow.", LogLevel.Info);
        }

        /// <summary>True for a location under today's wildcard snow (the valley's weather context).</summary>
        internal static bool SnowDayHere(GameLocation location)
            => DayEffects.Has(WildcardSchedule.SnowDay)
               && location != null
               && location.GetLocationContextId() == DefaultContext;
    }
}
