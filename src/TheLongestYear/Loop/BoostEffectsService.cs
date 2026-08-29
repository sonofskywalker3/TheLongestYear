using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Daily side of the boosts (spec 2026-08-29 sections 2.3 to 2.6): prune expired
    /// entries, set the lucky day, re-apply the endless buffs vanilla clears at sleep, write
    /// tomorrow's weather. Static read-throughs feed the Harmony patches.</summary>
    internal sealed class BoostEffectsService
    {
        public const string QuickFeetBuffId = "sonofskywalker3.TheLongestYear/QuickFeet";
        public const string IronLungsBuffId = "sonofskywalker3.TheLongestYear/IronLungs";
        public const float QuickFeetSpeed = 1f;
        public const float IronLungsStamina = 50f;
        /// <summary>Vanilla's daily-luck ceiling (Game1.cs: random.Next(-100, 101) / 1000, capped at 0.1).</summary>
        public const double LuckyDay = 0.10;
        private const string BuffSource = "The Longest Year";

        public static Func<bool> SecondWindTonight;
        public static Func<bool> FastFriendsActive;
        public static Func<bool> HagglerActive;

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;

        public BoostEffectsService(IMonitor monitor, MetaStore store)
        {
            _monitor = monitor;
            _store = store;
        }

        private int Today => Calendar.DayOfYear((int)_store.Run.Season, _store.Run.DayOfMonth);

        public bool Active(BoostId id) => RunActivation.IsActive && BoostState.IsActive(_store.Run, id, Today);

        public void OnDayStarted()
        {
            if (!RunActivation.IsActive) return;
            int pruned = BoostState.Prune(_store.Run, Today);
            if (pruned > 0)
                _monitor.Log($"Boosts: {pruned} expired entr{(pruned == 1 ? "y" : "ies")} pruned.", LogLevel.Trace);

            if (Active(BoostId.FortunesFavor) && Game1.IsMasterGame && Game1.player?.team != null)
            {
                Game1.player.team.sharedDailyLuck.Value = LuckyDay;
                _monitor.Log("Fortune's Favor: daily luck set to +0.10.", LogLevel.Info);
            }
            ApplyDailyBuffs();
        }

        /// <summary>Vanilla clears buffs at sleep; endless buffs with stable ids replace rather than stack.</summary>
        public void ApplyDailyBuffs()
        {
            Farmer p = Game1.player;
            if (p == null) return;
            ApplyOrRemove(p, Active(BoostId.QuickFeet), QuickFeetBuffId, "boost.quick_feet.name",
                new BuffEffects { Speed = { QuickFeetSpeed } });
            ApplyOrRemove(p, Active(BoostId.IronLungs), IronLungsBuffId, "boost.iron_lungs.name",
                new BuffEffects { MaxStamina = { IronLungsStamina } });
        }

        private static void ApplyOrRemove(Farmer p, bool active, string buffId, string nameKey, BuffEffects effects)
        {
            if (active)
            {
                string name = Strings.Get(nameKey);
                p.applyBuff(new Buff(buffId, source: BuffSource, displaySource: name, duration: Buff.ENDLESS,
                    effects: effects, displayName: name));
            }
            else if (p.buffs.IsApplied(buffId))
            {
                p.buffs.Remove(buffId);
            }
        }

        /// <summary>Rain Dance / Storm Call: write tomorrow's weather the way WeatherScheduleWriterPatch does.</summary>
        public static void WriteTomorrow(string weather, IMonitor monitor)
        {
            Game1.weatherForTomorrow = weather;
            Game1.netWorldState.Value.WeatherForTomorrow = weather;
            Game1.netWorldState.Value.GetWeatherForLocation("Default").WeatherForTomorrow = weather;
            monitor.Log($"Boost: tomorrow's weather set to {weather}.", LogLevel.Info);
        }
    }
}
