using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>Year One Ending (spec 2026-09-06 §4 scene 6): lights ONE candle at grandpa's shrine the
    /// way vanilla's grandpaCandles event command does (Event.cs GrandpaCandles: set Farm.grandpaScore,
    /// one fireball sound per candle, Farm.addGrandpaCandles), with the count fixed at one instead of
    /// computed from the player's score. The farm relights from grandpaScore on every load, so the
    /// candle stays lit until the reset zeroes it.</summary>
    internal static class GrandpaCandleCommand
    {
        public const string Name = "tlyGrandpaCandle";
        private const int Candles = 1;

        public static void Register(IMonitor monitor)
        {
            Event.RegisterCommand(Name, (evt, args, context) =>
            {
                try
                {
                    Farm farm = Game1.getFarm();
                    farm.grandpaScore.Value = Candles;
                    Game1.playSound("fireball");
                    farm.addGrandpaCandles();
                }
                catch (System.Exception ex)
                {
                    monitor.Log($"{Name}: could not light the shrine candle ({ex.GetType().Name}: {ex.Message}); the scene continues.", LogLevel.Warn);
                }
                evt.CurrentCommand++;
            });
        }
    }
}
