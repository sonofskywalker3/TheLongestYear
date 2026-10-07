using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wildcard days in the game (Randomizer, spec section 8). The week start plans the day and
    /// puts a quest in the log naming it; that morning the twist is revealed with a HUD line and
    /// published to <see cref="DayEffects"/>, which the twist patches read. Every other morning
    /// clears the channel. RunState holds the plan and the twist; the channel is rebuilt from it.
    ///
    /// The quest is a basic quest that never completes (vanilla removes a completed no-reward
    /// quest), replaced at each week start and removed when the option is off or on a rewind.
    /// </summary>
    internal sealed class WildcardDayService
    {
        private const string QuestIdPrefix = "tly.wildcard.";

        private readonly IMonitor _monitor;
        private readonly Func<RunState> _run;

        public WildcardDayService(IMonitor monitor, Func<RunState> run)
        {
            _monitor = monitor;
            _run = run;
        }

        private RunState Run => _run();

        /// <summary>The crop patch's read of tonight's extra-growth flag. Null while dormant.</summary>
        public static Func<bool> GrowthNight;

        /// <summary>The run, for the overnight twist patches (snow night, night event). Null while dormant.</summary>
        public static Func<RunState> NightRun;

        /// <summary>Tonight's overnight twist (<see cref="RunState.WildcardNightTwist"/>); null while dormant.</summary>
        internal static string NightTwist()
            => RunActivation.IsActive ? NightRun?.Invoke()?.WildcardNightTwist : null;

        /// <summary>Minecarts repaired by either route: the rockslide twist needs them.</summary>
        private static bool MinecartsRepaired()
        {
            var mail = Game1.MasterPlayer?.mailReceived;
            return mail != null && (mail.Contains("ccBoilerRoom") || mail.Contains("jojaBoilerRoom"));
        }

        /// <summary>Whether today is a vanilla green rain day, so the snow day stays out of the roll.
        /// <c>Game1.isGreenRain</c> is today's applied weather (ApplyWeatherForNewDay sets it before
        /// DayStarted; SaveGame sets it on load); <c>Utility.isGreenRainDay()</c> is vanilla's own
        /// schedule for today, kept as a second check so nothing that ran earlier this morning can
        /// hide the day.</summary>
        private static bool GreenRainToday()
            => Game1.isGreenRain || Utility.isGreenRainDay();

        /// <summary>Every morning, after the run date sync and before the weekly offer.
        /// <paramref name="weekSettings"/> is read only when a plan may be needed (a week start, or
        /// the week's snapshot already exists), so no snapshot is ever taken early.</summary>
        public void OnDayStarted(Func<RandomizerSettings> weekSettings)
        {
            RunState run = Run;
            // Last night's extra-growth pass is done.
            run.WildcardGrowthNight = false;
            // So is last night's snow-night / night-event half.
            run.WildcardNightTwist = null;

            bool mayPlan = Calendar.IsWeekStart(run.DayOfMonth) || run.RandomizerWeek == run.WeekOfYear;
            if (mayPlan && run.WildcardWeek != run.WeekOfYear)
            {
                bool enabled = (weekSettings() ?? new RandomizerSettings()).WildcardDays;
                bool planned = WildcardDays.PlanWeek(run, enabled);
                RemoveQuests();
                if (planned)
                    _monitor.Log($"Wildcard day for week {run.WeekOfYear}: {SeasonName(run)} {run.WildcardDay}.", LogLevel.Info);
            }

            string twist = WildcardDays.RevealToday(run, MinecartsRepaired, out bool revealedNow, GreenRainToday);
            DayEffects.Set(twist);
            RockslidePatch.Sync(_monitor);
            if (twist != null)
            {
                ApplyMorningEffects(twist);
                if (revealedNow)
                {
                    ApplyRevealEffects(twist);
                    Game1.addHUDMessage(new HUDMessage(
                        Strings.Get("hud.wildcard.reveal", new Dictionary<string, string> { ["twist"] = WildcardText.Name(twist) }),
                        HUDMessage.newQuest_type));
                    _monitor.Log($"Wildcard day revealed: {twist} ({SeasonName(run)} {run.DayOfMonth}).", LogLevel.Info);
                }
            }
            EnsureQuest();
        }

        /// <summary>Load: re-publish the twist already stored for today (never rolls) and refresh the quest.</summary>
        public void OnRunLoaded()
        {
            string twist = WildcardDays.StoredTwistToday(Run);
            DayEffects.Set(twist);
            RockslidePatch.Sync(_monitor);
            if (twist != null)
            {
                ApplyMorningEffects(twist);
                _monitor.Log($"Restored today's wildcard twist: {twist}.", LogLevel.Info);
            }
            EnsureQuest();
        }

        /// <summary>The wildcard day's DayEnding: the overnight crop pass sees tomorrow's date, so
        /// tonight's extra growth is keyed on a flag set now.</summary>
        public void OnDayEnding()
        {
            RunState run = Run;
            run.WildcardGrowthNight = WildcardDays.GrowthTonight(run);
            if (run.WildcardGrowthNight)
                _monitor.Log("Wildcard extra growth: watered crops grow an extra day tonight.", LogLevel.Info);
            // Snow day / night event: tonight's half (no outdoor growth, the forced farm event).
            run.WildcardNightTwist = WildcardDays.NightTwistTonight(run);
            if (run.WildcardNightTwist != null)
                _monitor.Log($"Wildcard night: {run.WildcardNightTwist} runs overnight.", LogLevel.Info);
            // The rockslide lasts the day; clear it before the save so a save never holds the rubble.
            RockslidePatch.Release(_monitor);
        }

        /// <summary>The rewind: the old loop's quest goes (the new loop plans its own week).</summary>
        public void OnReset()
        {
            DayEffects.Clear();
            RockslidePatch.Forget();
            RemoveQuests();
        }

        /// <summary>Effects that are a one-time write rather than a patch read. Max luck is also
        /// written by BoostEffectsService after its own luck write; this covers cutscene mornings,
        /// where the day start runs after the boosts' pass.</summary>
        private void ApplyMorningEffects(string twist)
        {
            if (twist == WildcardSchedule.MaxLuck && Game1.IsMasterGame && Game1.player?.team != null)
                Game1.player.team.sharedDailyLuck.Value = WildcardEffects.Luck(Game1.player.team.sharedDailyLuck.Value, true);
            // Snow is today's weather state (not a patch read); idempotent, so the load path repeats it.
            if (twist == WildcardSchedule.SnowDay)
                WildcardWeatherPatch.ApplySnow(_monitor);
        }

        /// <summary>One-shot effects that run on the reveal only, never on a reload (a reload must not
        /// put back debris the player already cleared).</summary>
        private void ApplyRevealEffects(string twist)
        {
            if (twist == WildcardSchedule.DebrisReturn)
                DebrisReturn.Apply(_monitor);
        }

        /// <summary>Debug (tly_wildcard): set today's twist, clear it, or describe the week.</summary>
        public string Debug(string[] args)
        {
            RunState run = Run;
            if (args.Length == 0)
                return $"Wildcard week {run.WildcardWeek} (now {run.WeekOfYear}): day {run.WildcardDay}, " +
                       $"twist {run.WildcardTwist ?? "none"} (day {run.WildcardTwistDay}), today {DayEffects.Today ?? "none"}, " +
                       $"growth night {run.WildcardGrowthNight}, night twist {run.WildcardNightTwist ?? "none"}.";
            string arg = args[0].Trim().ToLowerInvariant();
            if (arg == "clear")
            {
                WildcardDays.ClearTwist(run);
                DayEffects.Clear();
                RockslidePatch.Sync(_monitor);
                EnsureQuest();
                return "Wildcard twist cleared for today.";
            }
            if (!((IList<string>)WildcardSchedule.AllTwists).Contains(arg))
                return $"Unknown twist '{arg}'. One of: {string.Join(", ", WildcardSchedule.AllTwists)}, clear.";
            WildcardDays.ForceToday(run, arg);
            DayEffects.Set(arg);
            RockslidePatch.Sync(_monitor);
            ApplyMorningEffects(arg);
            ApplyRevealEffects(arg);
            EnsureQuest();
            return $"Wildcard twist for today set to {arg}.";
        }

        private static string SeasonName(RunState run) => Utility.getSeasonNameFromNumber((int)run.Season);

        /// <summary>Add the week's quest if it is missing, then set its text. No plan, no quest.</summary>
        private void EnsureQuest()
        {
            RunState run = Run;
            if (Game1.player?.questLog == null) return;
            if (!WildcardDays.HasPlanThisWeek(run))
            {
                RemoveQuests();
                return;
            }
            string id = QuestIdPrefix + run.WeekOfYear;
            Quest q = Find(id);
            if (q == null)
            {
                RemoveQuests();
                q = new Quest();
                q.questType.Value = Quest.type_basic;
                q.id.Value = id;
                q.dayQuestAccepted.Value = Game1.Date.TotalDays;
                q.daysLeft.Value = -1;   // no time limit; the next week start replaces it
                Game1.player.questLog.Add(q);
                _monitor.Log($"WildcardDayService: added quest {id}.", LogLevel.Trace);
            }
            q.questTitle = Strings.Get("quest.wildcard.title");
            q.questDescription = Strings.Get("quest.wildcard.description");
            string twist = run.WildcardTwist;
            q.currentObjective = twist == null
                ? Strings.Get("quest.wildcard.day", new Dictionary<string, string>
                {
                    ["season"] = SeasonName(run),
                    ["day"] = run.WildcardDay.ToString(),
                })
                : Strings.Get("quest.wildcard.revealed", new Dictionary<string, string>
                {
                    ["season"] = SeasonName(run),
                    ["day"] = run.WildcardDay.ToString(),
                    ["twist"] = WildcardText.Name(twist),
                });
        }

        private static Quest Find(string id)
        {
            foreach (Quest q in Game1.player.questLog)
                if (q?.id?.Value == id) return q;
            return null;
        }

        private static void RemoveQuests()
        {
            if (Game1.player?.questLog == null) return;
            for (int i = Game1.player.questLog.Count - 1; i >= 0; i--)
            {
                string qid = Game1.player.questLog[i]?.id?.Value;
                if (qid != null && qid.StartsWith(QuestIdPrefix, StringComparison.Ordinal))
                    Game1.player.questLog.RemoveAt(i);
            }
        }
    }
}
