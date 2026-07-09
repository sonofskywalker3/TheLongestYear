using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using StardewValley.Menus;
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
        private readonly JpCalculator _jp;
        private readonly Func<int, bool[]> _slotStateForBundle;

        public WeeklyThemeQuestService(IMonitor monitor, MetaStore store,
            GameplayConfig config, Func<int, bool[]> slotStateForBundle)
        {
            _monitor = monitor;
            _store = store;
            _jp = new JpCalculator(config.Jp);
            _slotStateForBundle = slotStateForBundle ?? (_ => null);
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
            if (Run.CurrentWeekBonusSlots.Count == 0) return;

            Theme theme = Run.CurrentSelection.Value;
            var (bonusId, liabilityId) = ThemeModifiers.For(theme);

            var q = new Quest();
            q.questType.Value = Quest.type_basic;
            q.questTitle = $"Weekly Theme: {theme}";
            q.questDescription =
                $"Bonus: {ThemeModifiers.DisplayNameFor(bonusId)}\n" +
                $"Drawback: {ThemeModifiers.DisplayNameFor(liabilityId)}\n\n" +
                "Tip: hold matching donations for their theme week - completing a goal slot pays " +
                "1.5x JP, and finishing every goal lifts the drawback. Each goal names the exact " +
                "bundle slot it wants (full quantity and quality).";
            q.id.Value = $"{QuestIdPrefix}{Run.WeekOfYear}";
            q.dayQuestAccepted.Value = Game1.Date.TotalDays;
            q.daysLeft.Value = -1;   // no time limit (the next week's pick will replace it)
            Game1.player.questLog.Add(q);

            RefreshObjective(q);

            _monitor.Log(
                $"WeeklyThemeQuestService: added quest '{q.questTitle}' for week {Run.WeekOfYear} " +
                $"with {Run.CurrentWeekBonusSlots.Count} goal slots.",
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
            if (Run.CurrentSelection.HasValue && Run.CurrentWeekBonusSlots.Count > 0)
                OnThemeSelected();
        }

        private void RefreshObjective(Quest q)
        {
            IList<BonusSlot> slots = Run.CurrentWeekBonusSlots;
            int doneCount = 0;
            var lines = new List<string>();

            foreach (BonusSlot slot in slots)
            {
                // Live CC slot state is the source of truth: every sampled slot was open at
                // selection time, so "complete now" means "completed this week". Self-reconciling
                // (no observer-miss drift), and vanilla only completes a slot when the full
                // stack at the required quality is deposited — multi-item goals need all items.
                bool isDone = IsSlotComplete(slot);
                if (isDone) doneCount++;
                // ASCII checkbox glyphs — Stardew's smallFont doesn't include U+2611/U+2610.
                lines.Add(isDone ? $"  [X] {DescribeSlot(slot)}" : $"  [ ] {DescribeSlot(slot)}");
            }

            q.currentObjective = $"Donated {doneCount}/{slots.Count}:\n" + string.Join("\n", lines);

            // Auto-complete when every goal slot has been donated this week. Two rewards land:
            //   1) A flat JP bonus (season-scaled like the bundle/room completion bonuses).
            //   2) The week's liability is lifted for the remaining days (bonus stays active).
            //      RunState.LiabilitySuppressedThisWeek persists the lifted state so a reload
            //      doesn't snap the liability back on; ActiveEffectsProvider.SuppressLiability
            //      drives the live patches (ForageOffPatch et al.) to short-circuit to false.
            if (slots.Count > 0 && doneCount == slots.Count && !q.completed.Value)
            {
                q.questComplete();
                AwardCompletionRewards();
            }
        }

        private bool IsSlotComplete(BonusSlot slot)
        {
            bool[] state = _slotStateForBundle(slot.BundleIndex);
            return state != null
                && slot.IngredientIndex >= 0
                && slot.IngredientIndex < state.Length
                && state[slot.IngredientIndex];
        }

        private void AwardCompletionRewards()
        {
            // Idempotency guard: if the persisted flag is already set, the rewards already
            // landed in a prior session — don't double-pay JP on a save+reload.
            if (Run.LiabilitySuppressedThisWeek)
                return;

            long bonus = JpBoostHelper.Apply(_store.State, _jp.WeeklyQuestBonus(Run.WeekOfYear));
            _store.State.JunimoPoints += bonus;
            Run.LiabilitySuppressedThisWeek = true;
            ActiveEffectsProvider.SuppressLiability();

            string liabilityName = Run.CurrentSelection.HasValue
                ? ThemeModifiers.DisplayNameFor(ThemeModifiers.For(Run.CurrentSelection.Value).LiabilityId)
                : "drawback";

            Game1.addHUDMessage(new HUDMessage(
                $"Weekly theme complete! +{bonus} JP, drawback lifted.",
                HUDMessage.achievement_type));

            _monitor.Log(
                $"WeeklyThemeQuest complete: +{bonus} JP (now {_store.State.JunimoPoints}), " +
                $"liability '{liabilityName}' suppressed for the rest of the week.",
                LogLevel.Info);
        }

        /// <summary>Egg objects whose DisplayName collides across colors: 174/182 both render as
        /// "Large Egg" and 176/180 both as "Egg". A bundle slot accepts exactly ONE color, so the
        /// quest log must name it or the player can't tell which egg the goal wants (khauser13,
        /// Nexus: "says a large egg but it needed a large brown egg — the white one didn't count").
        /// Keyed by bare id (qualifier stripped).</summary>
        private static readonly Dictionary<string, string> AmbiguousEggColors = new(StringComparer.Ordinal)
        {
            ["174"] = "White",   // Large Egg (white)
            ["182"] = "Brown",   // Large Egg (brown)
            ["176"] = "White",   // Egg (white)
            ["180"] = "Brown",   // Egg (brown)
        };

        /// <summary>"DisplayName (Brown) x5 (gold) - Bundle Name" — names the exact slot
        /// requirement. Quality tags: 1=silver, 2/3=gold, 4=iridium.</summary>
        private string DescribeSlot(BonusSlot slot)
        {
            string name = slot.ItemId;
            try
            {
                Item item = ItemRegistry.Create(slot.ItemId, 1, 0, allowNull: true);
                if (item != null) name = item.DisplayName;
            }
            catch (Exception)
            {
                // ItemRegistry may throw for malformed ids; fall back to the raw id.
            }

            string colorTag = AmbiguousEggColors.TryGetValue(BareItemId(slot.ItemId), out string color)
                ? $" ({color})" : "";
            string qty = slot.Stack > 1 ? $" x{slot.Stack}" : "";
            string quality = slot.Quality >= 4 ? " (iridium)"
                : slot.Quality >= 2 ? " (gold)"
                : slot.Quality >= 1 ? " (silver)"
                : "";
            return $"{name}{colorTag}{qty}{quality} - {slot.BundleName}";
        }

        /// <summary>Strip a "(O)"/"(BC)" type prefix from a qualified id, leaving the bare id. Used
        /// to key the egg-color table regardless of qualifier.</summary>
        private static string BareItemId(string qualifiedId)
        {
            if (string.IsNullOrEmpty(qualifiedId)) return qualifiedId;
            int close = qualifiedId.IndexOf(')');
            return close >= 0 && qualifiedId.StartsWith("(", StringComparison.Ordinal)
                ? qualifiedId[(close + 1)..]
                : qualifiedId;
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
