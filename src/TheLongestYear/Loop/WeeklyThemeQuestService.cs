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
    /// Surfaces the current week's selected theme + goal-slot checklist as a vanilla
    /// <see cref="Quest"/> in the player's quest log. Goals are exact Community Center bundle
    /// slots (bundle, ingredient line, stack, quality) seeded-sampled from the open-slot pool at
    /// selection time (<see cref="RunState.CurrentWeekBonusSlots"/>). Each goal ticks its
    /// checkbox when that slot flips complete in LIVE CC state — self-reconciling, since vanilla
    /// only marks a slot complete once the full required stack and quality are deposited. When
    /// every goal is done, the quest auto-completes.
    ///
    /// Spec source: 2026-05-26 playtest discussion (logged in TODO.md). User reiterated on
    /// 2026-05-28: "I still don't have a quest for tracking my weekly theme." Implemented
    /// as the v1.1 ask after the v1 polish batch shipped. Slots replaced the earlier "any 4
    /// sampled item ids" design in the 2026-07-09 slot redesign.
    ///
    /// Persistence:
    ///   - Quest lives in <c>Game1.player.questLog</c>, which is saved by vanilla.
    ///   - Goal slots live in <see cref="RunState.CurrentWeekBonusSlots"/>, which is saved by
    ///     MetaStore on the game's Saving event. Live CC state (not a separate ledger) is read
    ///     directly to determine completion, so there's nothing else to persist per-donation.
    ///   - On save reload, <see cref="OnRunLoaded"/> re-derives the objective text so a
    ///     mid-week reload doesn't show stale progress.
    ///
    /// Lifecycle:
    ///   - <see cref="OnThemeSelected"/> from <c>RunController.SelectByName</c> + the day-28
    ///     pre-pick application path. Removes any prior weekly quest and creates a fresh one.
    ///     An empty open-slot pool at selection means no quest is created and the week's
    ///     liability is auto-lifted instead (see <c>RunController.ApplyEmptyPoolLiftIfNeeded</c>).
    ///   - <see cref="OnItemDonated"/> from <c>DonationService.OnItemDonated</c>. Cheap — one
    ///     questLog scan + one live-CC-state re-check + one text update.
    ///   - Each completed goal pays its share of the weekly JP bonus (rule D, see
    ///     <see cref="PayGoalShares"/>); completing all goals lifts the drawback for the rest of
    ///     the week (see <see cref="LiftLiability"/>).
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
        private readonly GameplayConfig _config;
        /// <summary>Built per call from the run's STAMPED difficulty profile, not cached in the
        /// constructor: a reset re-stamps the profile mid-session, so a calculator captured at
        /// construction time would keep paying the previous loop's rate for the rest of the
        /// session. The object is tiny, so building one per award costs nothing worth saving.</summary>
        private JpCalculator Jp =>
            new JpCalculator(_config.Jp, _store.State.EffectiveDifficulty(_config).JpEarnedFactor);
        private readonly Func<int, bool[]> _slotStateForBundle;

        public WeeklyThemeQuestService(IMonitor monitor, MetaStore store,
            GameplayConfig config, Func<int, bool[]> slotStateForBundle)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _slotStateForBundle = slotStateForBundle ?? (_ => null);
        }

        private RunState Run => _store.Run;

        /// <summary>The week's goal lists: list 0 is the picked theme, list 1 the second card on a
        /// double theme week (spec section 6). Each list has its own quest, pays its own weekly
        /// bonus at its own multiplier and lifts its own drawback.</summary>
        private const int FirstList = 0, SecondList = 1;
        private static readonly int[] Lists = { FirstList, SecondList };

        /// <summary>The second list's quest id is the first one's plus this suffix.</summary>
        private const string SecondQuestSuffix = ".b";

        private Theme? SelectionOf(int list) => list == SecondList ? Run.SecondSelection : Run.CurrentSelection;

        private List<BonusSlot> SlotsOf(int list)
            => list == SecondList ? (Run.SecondWeekBonusSlots ??= new List<BonusSlot>()) : Run.CurrentWeekBonusSlots;

        /// <summary>The list's random shrine donation goals (Randomizer; empty when the option is off).</summary>
        private IReadOnlyList<ShrineGoal> ShrineGoalsOf(int list)
            => ShrineGoalRules.OfList(Run.CurrentWeekShrineGoals, list);

        private double MultiplierOf(int list) => list == SecondList ? Run.SecondGoalMultiplier : Run.CurrentGoalMultiplier;

        private (string BonusId, string LiabilityId) EffectsOf(int list, Theme theme)
            => list == SecondList ? RandomPairing.SecondEffectsFor(Run, theme) : RandomPairing.EffectsFor(Run, theme);

        /// <summary>
        /// Called after a theme is selected (current-week pick or day-28 pre-pick application).
        /// Removes any prior weekly quest and adds a fresh one keyed to the current week (two on a
        /// double week).
        /// </summary>
        public void OnThemeSelected()
        {
            RemoveExistingWeeklyQuests();
            foreach (int list in Lists)
                AddQuest(list);
        }

        private void AddQuest(int list)
        {
            if (SelectionOf(list) is not Theme theme) return;
            List<BonusSlot> slots = SlotsOf(list);
            IReadOnlyList<ShrineGoal> shrineGoals = ShrineGoalsOf(list);
            // A list with only shrine goals (Randomizer) still gets its quest.
            if (slots.Count == 0 && shrineGoals.Count == 0) return;

            var (bonusId, liabilityId) = EffectsOf(list, theme);
            double multiplier = MultiplierOf(list);

            var q = new Quest();
            q.questType.Value = Quest.type_basic;
            q.questTitle = Strings.Get("quest.weekly.title",
                new Dictionary<string, string> { ["theme"] = ThemeDisplay.Name(theme) });
            // Keep the description SHORT: the quest-log page renders it above the objective
            // text, and a long paragraph here pushes the checklist below the fold (user
            // feedback 2026-07-09: "had to scroll down to find them"). The tip rides at the
            // END of the objective text instead — below the checklist.
            q.questDescription = Strings.Get("quest.weekly.description", new Dictionary<string, string>
            {
                ["bonus"] = ThemeModifiers.DisplayNameFor(bonusId),
                ["drawback"] = ThemeModifiers.DisplayNameFor(liabilityId),
            });
            // The randomizer's card multiplier (1x shows nothing, the quest log stays as before).
            if (multiplier != NoGoalMultiplier)
                q.questDescription += "\n" + Strings.Get("quest.weekly.mult", new Dictionary<string, string>
                {
                    ["mult"] = CardMultiplier.Format(multiplier),
                });
            q.id.Value = $"{QuestIdPrefix}{Run.WeekOfYear}{(list == SecondList ? SecondQuestSuffix : "")}";
            q.dayQuestAccepted.Value = Game1.Date.TotalDays;
            q.daysLeft.Value = -1;   // no time limit (the next week's pick will replace it)
            Game1.player.questLog.Add(q);

            RefreshObjective(q, list);

            _monitor.Log(
                $"WeeklyThemeQuestService: added quest '{q.questTitle}' ({q.id.Value}) for week {Run.WeekOfYear} " +
                $"with {slots.Count} goal slots" +
                (shrineGoals.Count > 0 ? $" and {shrineGoals.Count} shrine goal(s)." : "."),
                LogLevel.Info);
        }

        /// <summary>
        /// Called after a CC donation lands. Refreshes each weekly quest's objective text and
        /// auto-completes one if every goal slot on its list is now complete in live CC state.
        /// </summary>
        public void OnItemDonated()
        {
            foreach (int list in Lists)
            {
                Quest q = FindCurrentWeeklyQuest(list);
                if (q != null) RefreshObjective(q, list);
            }
        }

        /// <summary>
        /// Called from <c>RunController.OnRunLoaded</c>. Two responsibilities:
        ///   1. Re-render the objective so a save+reload mid-week reflects the persisted ledger
        ///      instead of stale serialised text.
        ///   2. Create the quest if it's missing but a theme is already selected. Covers the
        ///      first-time-installing-this-version case where the player picked a theme on a
        ///      prior build that didn't have the quest service yet.
        /// Each list (two on a double week) is handled on its own.
        /// </summary>
        public void OnRunLoaded()
        {
            // Saves written before the deposit rule existed carry no deposit records, so goals the
            // player legitimately finished would silently un-tick. Trust what is already complete
            // once, here, and let the rule govern from this point on.
            int grandfathered = WeeklyGoalCredit.GrandfatherCompleted(Run.CurrentWeekBonusSlots, IsSlotFlaggedInCc);
            if (grandfathered > 0)
                _monitor.Log(
                    $"WeeklyThemeQuest: credited {grandfathered} already-complete goal slot(s) from a pre-0.14.0 save.",
                    LogLevel.Info);

            foreach (int list in Lists)
            {
                Quest q = FindCurrentWeeklyQuest(list);
                if (q != null)
                {
                    RefreshObjective(q, list);
                    continue;
                }

                // No quest in log — back-fill it if a selection is already active.
                if (SelectionOf(list).HasValue && (SlotsOf(list).Count > 0 || ShrineGoalsOf(list).Count > 0))
                    AddQuest(list);
            }
        }

        private void RefreshObjective(Quest q, int list)
        {
            List<BonusSlot> slots = SlotsOf(list);
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

            // Random shrine donation goals (Randomizer) join this list's total, done count and
            // paid shares; a shrine goal is done once deposited at the statue.
            IReadOnlyList<ShrineGoal> shrineGoals = ShrineGoalsOf(list);
            foreach (ShrineGoal goal in shrineGoals)
                lines.Add(goal.Deposited ? $"  [X] {DescribeShrineGoal(goal)}" : $"  [ ] {DescribeShrineGoal(goal)}");
            int ccDone = doneCount;
            (doneCount, int total) = ShrineGoalRules.Tally(ccDone, slots.Count, shrineGoals);

            // Checklist first, tip LAST — the tip must never push the goals below the fold
            // (user feedback 2026-07-09).
            string progress = Strings.Get("quest.weekly.progress", new Dictionary<string, string>
            {
                ["done"] = doneCount.ToString(),
                ["total"] = total.ToString(),
            });
            q.currentObjective =
                progress + "\n" + string.Join("\n", lines) +
                "\n\n" + Strings.Get("quest.weekly.tip");

            // Rule D (activity-themes spec): each goal that lands pays its share of the weekly
            // bonus right away; BonusSlot.Paid guards against paying twice across a reload.
            int newlyPaid = WeeklyGoalPayout.MarkPaid(slots, IsSlotComplete) + ShrineGoalRules.MarkPaid(shrineGoals);
            if (newlyPaid > 0)
                PayGoalShares(newlyPaid, doneCount, total, MultiplierOf(list));

            // Auto-complete when every goal slot has been donated this week: the week's liability
            // is lifted for the remaining days (bonus stays active). RunState.LiabilitySuppressedThisWeek
            // persists the lifted state so a reload doesn't snap the liability back on;
            // ActiveEffectsProvider.SuppressLiability drives the live patches (ForageOffPatch et al.).
            // On a double week each list lifts only its own drawback.
            // Shrine goals count: the drawback lifts only when every goal of both kinds is done.
            if (total > 0 && doneCount == total && !q.completed.Value)
            {
                q.questComplete();
                if (list == SecondList) LiftSecondLiability();
                else LiftLiability();
            }
        }

        /// <summary>The goal multiplier that changes nothing (randomizer off, old saves, debug picks).</summary>
        private const double NoGoalMultiplier = 1.0;

        /// <summary>Rule D: the weekly bonus (30 x season multiplier) split evenly across the
        /// week's goals and paid as each lands. A one-goal Winter week pays 120 / 7, not 120.
        /// The randomizer's card multiplier (stored at selection) scales the bonus before the split.
        /// On a double week each list pays its own bonus at its own multiplier over its own goals.</summary>
        private void PayGoalShares(int newlyPaid, int doneCount, int total, double multiplier)
        {
            long weeklyBonus = (long)Math.Round(Jp.WeeklyQuestBonus(Run.WeekOfYear) * multiplier,
                MidpointRounding.AwayFromZero);
            long perGoal = WeeklyGoalPayout.PerGoal(weeklyBonus, total);
            long paid = JpBoostHelper.Apply(_store.State, perGoal * newlyPaid);
            _store.State.JunimoPoints += paid;
            Game1.addHUDMessage(new HUDMessage(
                Strings.Get("hud.goal-paid", new Dictionary<string, string>
                {
                    ["jp"] = paid.ToString(),
                    ["done"] = doneCount.ToString(),
                    ["total"] = total.ToString(),
                }),
                HUDMessage.achievement_type));
            _monitor.Log(
                $"WeeklyThemeQuest: {newlyPaid} goal(s) done, +{paid} JP ({doneCount}/{total}, now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>Vanilla's own flag for the slot. NOT sufficient on its own to credit a goal,
        /// because completing a bundle blanket-sets every flag in it - see WeeklyGoalCredit.</summary>
        private bool IsSlotFlaggedInCc(BonusSlot slot)
        {
            bool[] state = _slotStateForBundle(slot.BundleIndex);
            return state != null
                && slot.IngredientIndex >= 0
                && slot.IngredientIndex < state.Length
                && state[slot.IngredientIndex];
        }

        /// <summary>A goal counts only when the player actually deposited into the slot AND vanilla
        /// agrees it is filled (@ggrace67, 2026-08-26: finishing an n-of-m bundle with other items
        /// used to tick the goal, pay the JP and lift the drawback for free).</summary>
        private bool IsSlotComplete(BonusSlot slot) =>
            WeeklyGoalCredit.IsSatisfied(slot, IsSlotFlaggedInCc(slot));

        /// <summary>Every goal done: lift the drawback for the rest of the week. The JP was already
        /// paid goal by goal (rule D); the persisted flag keeps a reload from re-announcing it.</summary>
        private void LiftLiability()
        {
            if (Run.LiabilitySuppressedThisWeek)
                return;

            Run.LiabilitySuppressedThisWeek = true;
            ActiveEffectsProvider.SuppressLiability();

            string liabilityName = Run.CurrentSelection.HasValue
                ? ThemeModifiers.DisplayNameFor(RandomPairing.EffectsFor(Run, Run.CurrentSelection.Value).LiabilityId)
                : "drawback";

            // A double week names the theme, since the other list's drawback may still be on.
            Game1.addHUDMessage(new HUDMessage(
                Run.IsDoubleWeekSelection && Run.CurrentSelection.HasValue
                    ? Strings.Get("hud.theme-complete-named", new Dictionary<string, string> { ["theme"] = ThemeDisplay.Name(Run.CurrentSelection.Value) })
                    : Strings.Get("hud.theme-complete"),
                HUDMessage.achievement_type));

            _monitor.Log(
                $"WeeklyThemeQuest complete: liability '{liabilityName}' suppressed for the rest of the week.",
                LogLevel.Info);
        }

        /// <summary>Double week: the second list is done, so lift only the second card's drawback.</summary>
        private void LiftSecondLiability()
        {
            if (Run.SecondLiabilitySuppressedThisWeek || Run.SecondSelection is not Theme second)
                return;

            Run.SecondLiabilitySuppressedThisWeek = true;
            ActiveEffectsProvider.SuppressSecondLiability();

            Game1.addHUDMessage(new HUDMessage(
                Strings.Get("hud.theme-complete-named", new Dictionary<string, string> { ["theme"] = ThemeDisplay.Name(second) }),
                HUDMessage.achievement_type));

            _monitor.Log(
                $"WeeklyThemeQuest (second list, {second}) complete: liability " +
                $"'{RandomPairing.SecondEffectsFor(Run, second).LiabilityId}' suppressed for the rest of the week.",
                LogLevel.Info);
        }

        /// <summary>Egg objects whose DisplayName collides across colors: 174/182 both render as
        /// "Large Egg" and 176/180 both as "Egg". A bundle slot accepts exactly ONE color, so the
        /// quest log must name it or the player can't tell which egg the goal wants (khauser13,
        /// Nexus: "says a large egg but it needed a large brown egg — the white one didn't count").
        /// Keyed by bare id (qualifier stripped).</summary>
        private static readonly Dictionary<string, string> AmbiguousEggColors = new(StringComparer.Ordinal)
        {
            ["174"] = "egg-color.white",   // Large Egg (white)
            ["182"] = "egg-color.brown",   // Large Egg (brown)
            ["176"] = "egg-color.white",   // Egg (white)
            ["180"] = "egg-color.brown",   // Egg (brown)
        };

        /// <summary>"DisplayName (Brown) x5 (gold) - Bundle Name" — names the exact slot
        /// requirement. Quality tags via <see cref="QualityTags"/>.</summary>
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

            string colorTag = AmbiguousEggColors.TryGetValue(BareItemId(slot.ItemId), out string colorKey)
                ? Strings.Get(colorKey) : "";
            string qty = slot.Stack > 1
                ? Strings.Get("quest.weekly.qty", new Dictionary<string, string> { ["count"] = slot.Stack.ToString() })
                : "";
            string quality = QualityTags.For(slot.Quality);
            string described = Strings.Get("quest.weekly.slot", new Dictionary<string, string>
            {
                ["item"] = name,
                ["color"] = colorTag,
                ["qty"] = qty,
                ["quality"] = quality,
                ["bundle"] = slot.BundleName,
            });
            // Spec 2026-08-28-obtainable-board-2-stretch: a stretch goal asks for an item the
            // model wouldn't otherwise call obtainable yet - tag it so the player isn't confused.
            string tagged = slot.Stretch ? described + Strings.Get("goal.stretch-tag") : described;
            // Spec 2026-08-28-obtainable-board-4-boosts: tag a goal that routes through a Boost
            // (Year-Two Seeds, Sneak Peek) so the player knows why it's askable at all.
            return slot.RouteTag == null
                ? tagged
                : tagged + Strings.Get("goal.route-tag", new Dictionary<string, string> { ["tag"] = slot.RouteTag });
        }

        /// <summary>"DisplayName (Brown) x5 - Junimo Shrine": a random shrine donation goal, any quality.</summary>
        private string DescribeShrineGoal(ShrineGoal goal)
        {
            string name = goal.ItemId;
            try
            {
                Item item = ItemRegistry.Create(goal.ItemId, 1, 0, allowNull: true);
                if (item != null) name = item.DisplayName;
            }
            catch (Exception ex)
            {
                // ItemRegistry may throw for malformed ids; fall back to the raw id.
                _monitor.Log($"Shrine goal '{goal.ItemId}' has no item data ({ex.GetType().Name}); showing the raw id.", LogLevel.Trace);
            }
            string colorTag = AmbiguousEggColors.TryGetValue(BareItemId(goal.ItemId), out string colorKey)
                ? Strings.Get(colorKey) : "";
            string qty = goal.Stack > 1
                ? Strings.Get("quest.weekly.qty", new Dictionary<string, string> { ["count"] = goal.Stack.ToString() })
                : "";
            return Strings.Get("quest.weekly.shrine-slot", new Dictionary<string, string>
            {
                ["item"] = name,
                ["color"] = colorTag,
                ["qty"] = qty,
            });
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

        /// <summary>The weekly quest for <paramref name="list"/>: the second list's id ends with
        /// <see cref="SecondQuestSuffix"/>, the first list's does not. Vanilla removes a completed
        /// quest from the log, so one list's quest can be gone while the other's is still there.</summary>
        private static Quest FindCurrentWeeklyQuest(int list)
        {
            if (Game1.player?.questLog == null) return null;
            foreach (Quest q in Game1.player.questLog)
            {
                string id = q?.id?.Value;
                if (id == null || !id.StartsWith(QuestIdPrefix, StringComparison.Ordinal)) continue;
                if (id.EndsWith(SecondQuestSuffix, StringComparison.Ordinal) == (list == SecondList))
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
