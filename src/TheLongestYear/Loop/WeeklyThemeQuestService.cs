using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Surfaces the current week's selected theme + bonus-item checklist as a vanilla
    /// <see cref="Quest"/> in the player's quest log. Each donation to the Community Center
    /// that matches a bonus item flips its checkbox in the quest objective. When all four
    /// are donated, the quest auto-completes.
    ///
    /// Spec source: 2026-05-26 playtest discussion (logged in TODO.md). User reiterated on
    /// 2026-05-28: "I still don't have a quest for tracking my weekly theme." Implemented
    /// as the v1.1 ask after the v1 polish batch shipped.
    ///
    /// Persistence:
    ///   - Quest lives in <c>Game1.player.questLog</c>, which is saved by vanilla.
    ///   - Bonus list + donation ledger live in <see cref="RunState"/>, which is saved by
    ///     MetaStore on the game's Saving event.
    ///   - On save reload, <see cref="OnRunLoaded"/> re-derives the objective text so a
    ///     mid-week reload doesn't show stale progress.
    ///
    /// Lifecycle:
    ///   - <see cref="OnThemeSelected"/> from <c>RunController.SelectByName</c> + the day-28
    ///     pre-pick application path. Removes any prior weekly quest and creates a fresh one.
    ///   - <see cref="OnItemDonated"/> from <c>DonationService.OnItemDonated</c> after the
    ///     ledger is updated. Cheap — one questLog scan + one text update.
    ///   - The reset wipes <c>player.questLog</c> via <c>loadForNewGame</c>, so no explicit
    ///     cleanup is needed across runs.
    /// </summary>
    internal sealed class WeeklyThemeQuestService
    {
        /// <summary>Prefix shared by all TLY weekly quest ids. Used for find + cleanup so a
        /// reopen of the hub mid-week (rare) doesn't accumulate duplicate quest entries.</summary>
        private const string QuestIdPrefix = "tly.weekly.";

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly Func<string, int> _stackForIngredient;

        public WeeklyThemeQuestService(IMonitor monitor, MetaStore store,
            Func<string, int> stackForIngredient)
        {
            _monitor = monitor;
            _store = store;
            _stackForIngredient = stackForIngredient ?? (_ => 1);
        }

        private RunState Run => _store.Run;

        /// <summary>
        /// Called after a theme is selected (current-week pick or day-28 pre-pick application).
        /// Removes any prior weekly quest and adds a fresh one keyed to the current week.
        /// </summary>
        public void OnThemeSelected()
        {
            RemoveExistingWeeklyQuests();

            if (!Run.CurrentSelection.HasValue) return;
            if (Run.CurrentWeekBonusItems.Count == 0) return;

            Theme theme = Run.CurrentSelection.Value;
            var (bonusId, liabilityId) = ThemeModifiers.For(theme);

            var q = new Quest();
            q.questType.Value = Quest.type_basic;
            q.questTitle = $"Weekly Theme: {theme}";
            q.questDescription =
                $"Bonus: {ThemeModifiers.DisplayNameFor(bonusId)}\n" +
                $"Drawback: {ThemeModifiers.DisplayNameFor(liabilityId)}\n\n" +
                "Donate any of this week's bonus items to the Community Center for 1.5x JP.";
            q.id.Value = $"{QuestIdPrefix}{Run.WeekOfYear}";
            q.dayQuestAccepted.Value = Game1.Date.TotalDays;
            q.daysLeft.Value = -1;   // no time limit (the next week's pick will replace it)
            Game1.player.questLog.Add(q);

            RefreshObjective(q);

            _monitor.Log(
                $"WeeklyThemeQuestService: added quest '{q.questTitle}' for week {Run.WeekOfYear} " +
                $"with {Run.CurrentWeekBonusItems.Count} bonus items.",
                LogLevel.Info);
        }

        /// <summary>
        /// Called after a CC donation lands in the ledger. Refreshes the current weekly quest's
        /// objective text and auto-completes it if every bonus item has been donated.
        /// </summary>
        public void OnItemDonated()
        {
            Quest q = FindCurrentWeeklyQuest();
            if (q == null) return;
            RefreshObjective(q);
        }

        /// <summary>
        /// Called from <c>RunController.OnRunLoaded</c>. Two responsibilities:
        ///   1. Re-render the objective so a save+reload mid-week reflects the persisted ledger
        ///      instead of stale serialised text.
        ///   2. Create the quest if it's missing but a theme is already selected. Covers the
        ///      first-time-installing-this-version case where the player picked a theme on a
        ///      prior build that didn't have the quest service yet.
        /// </summary>
        public void OnRunLoaded()
        {
            Quest q = FindCurrentWeeklyQuest();
            if (q != null)
            {
                RefreshObjective(q);
                return;
            }

            // No quest in log — back-fill it if a selection is already active.
            if (Run.CurrentSelection.HasValue && Run.CurrentWeekBonusItems.Count > 0)
                OnThemeSelected();
        }

        private void RefreshObjective(Quest q)
        {
            IList<string> bonusItems = Run.CurrentWeekBonusItems;
            List<string> donated = Run.DonatedItemIds;
            int doneCount = 0;
            var lines = new List<string>();

            foreach (string id in bonusItems)
            {
                string name = ResolveDisplayName(id);
                bool isDone = donated.Contains(id);
                if (isDone) doneCount++;
                // ASCII checkbox glyphs — Stardew's smallFont doesn't include U+2611/U+2610.
                lines.Add(isDone ? $"  [X] {name}" : $"  [ ] {name}");
            }

            q.currentObjective = $"Donated {doneCount}/{bonusItems.Count}:\n" + string.Join("\n", lines);

            // Auto-complete when every bonus item has been donated this run (idempotent — the
            // questComplete call no-ops if already completed).
            if (bonusItems.Count > 0 && doneCount == bonusItems.Count && !q.completed.Value)
            {
                q.questComplete();
                _monitor.Log(
                    $"WeeklyThemeQuestService: all {doneCount} bonus items donated — quest complete.",
                    LogLevel.Info);
            }
        }

        /// <summary>Resolve a qualified item id to "DisplayName xStack" or fall back to the raw id.
        /// Stack count comes from the same ingredient-stack map the hub uses, so the quest text
        /// matches the bonus icons' badges (e.g. "Wood x99" rather than just "Wood").</summary>
        private string ResolveDisplayName(string qualifiedId)
        {
            try
            {
                Item item = ItemRegistry.Create(qualifiedId, 1, 0, allowNull: true);
                if (item != null)
                {
                    int stack = _stackForIngredient(qualifiedId);
                    string qty = stack > 1 ? $" x{stack}" : "";
                    return $"{item.DisplayName}{qty}";
                }
            }
            catch (Exception)
            {
                // ItemRegistry may throw for malformed ids; fall through to the raw id.
            }
            return qualifiedId;
        }

        private static Quest FindCurrentWeeklyQuest()
        {
            if (Game1.player?.questLog == null) return null;
            foreach (Quest q in Game1.player.questLog)
            {
                if (q?.id?.Value != null && q.id.Value.StartsWith(QuestIdPrefix, StringComparison.Ordinal))
                    return q;
            }
            return null;
        }

        private static void RemoveExistingWeeklyQuests()
        {
            if (Game1.player?.questLog == null) return;
            for (int i = Game1.player.questLog.Count - 1; i >= 0; i--)
            {
                Quest q = Game1.player.questLog[i];
                if (q?.id?.Value != null && q.id.Value.StartsWith(QuestIdPrefix, StringComparison.Ordinal))
                    Game1.player.questLog.RemoveAt(i);
            }
        }
    }
}
