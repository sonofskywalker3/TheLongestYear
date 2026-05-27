using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Queues vanilla <see cref="LevelUpMenu"/> instances for kept skills that landed at
    /// Level 5 or 10 — re-runs the profession picker so the player can change their picks
    /// each loop. The reset code can't open menus directly (loadForNewGame is mid-flight),
    /// so we enqueue here and the next DayStarted handler in <see cref="RunController"/>
    /// drains the queue by stacking the menus via <c>Game1.endOfNightMenus.Push</c> — the
    /// same path vanilla uses when natural level-ups happen at sleep.
    ///
    /// One menu is required per profession threshold: Level 5 gives the first profession
    /// choice, Level 10 gives the specialisation. A skill kept at L10 needs BOTH menus
    /// queued (vanilla pushes the L5 menu first then the L10 menu).
    /// </summary>
    internal sealed class ProfessionPickerScheduler
    {
        private readonly IMonitor _monitor;
        private readonly Queue<(int Skill, int Level)> _pending = new();

        public ProfessionPickerScheduler(IMonitor monitor) => _monitor = monitor;

        public int PendingCount => _pending.Count;

        /// <summary>Queue picker menus for the given skill index based on the kept level.
        /// L5 keep → one menu. L10 keep → two menus (L5 then L10).</summary>
        public void Enqueue(int skillIndex, int level)
        {
            if (level >= 5)
                _pending.Enqueue((skillIndex, 5));
            if (level >= 10)
                _pending.Enqueue((skillIndex, 10));
        }

        /// <summary>Push all pending menus onto Game1.endOfNightMenus in queue order. They
        /// pop in LIFO, so the LAST pushed shows first — push in REVERSE so the player sees
        /// L5 before L10 within a skill, and Farming before Mining etc. (alphabetic enqueue).
        /// Safe to call when queue is empty (no-op).</summary>
        public void DrainOnDayStart()
        {
            if (_pending.Count == 0)
                return;

            // Collect to a list so we can iterate in reverse.
            var pickers = new List<(int Skill, int Level)>(_pending);
            _pending.Clear();

            for (int i = pickers.Count - 1; i >= 0; i--)
                Game1.endOfNightMenus.Push(new LevelUpMenu(pickers[i].Skill, pickers[i].Level));

            _monitor.Log(
                $"ProfessionPickerScheduler: queued {pickers.Count} profession picker menu(s) for the player.",
                LogLevel.Info);

            // Trigger the menu stack to start drawing. endOfNightMenus is normally drained by
            // the post-sleep sequence; since we're queuing on DayStarted we kick it manually
            // by activating the top one. Game1.showEndOfNightStuff() is the public entrypoint.
            Game1.showEndOfNightStuff();
        }
    }
}
