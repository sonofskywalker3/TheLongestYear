using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Credits and pays a random shrine donation goal (Randomizer, spec section 7) donated at the
    /// farm statue. The Donate tab and the tly_shrinedonate debug command both come through
    /// <see cref="Donate"/>. An item is only taken from the inventory when a goal is credited in
    /// the same call. No CC ledger write: shrine goals have no board slot.
    /// </summary>
    internal sealed class ShrineDonationService
    {
        /// <summary>Set on save load (null on non-TLY saves and at the title).</summary>
        internal static ShrineDonationService Active;

        private const int SecondList = 1;

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;

        /// <summary>Built per call from the run's stamped difficulty profile, like DonationService.</summary>
        private JpCalculator Jp =>
            new JpCalculator(_config.Jp, _store.State.EffectiveDifficulty(_config).JpEarnedFactor);

        /// <summary>Fires after a goal is credited and paid: the weekly quest refresh (its goal share,
        /// completion and drawback lift).</summary>
        public Action AfterDonation;

        public ShrineDonationService(IMonitor monitor, MetaStore store, GameplayConfig config)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
        }

        private RunState Run => _store.Run;

        /// <summary>This week's shrine goals, both lists; an index into this list is a goal index.</summary>
        public IReadOnlyList<ShrineGoal> Goals => Run.CurrentWeekShrineGoals ?? new List<ShrineGoal>();

        /// <summary>The bank, for debug before/after lines.</summary>
        public long JunimoPoints => _store.State.JunimoPoints;

        /// <summary>True when <paramref name="item"/> would fill goal <paramref name="goalIndex"/>
        /// (right item, any quality, enough in this one stack, goal still open).</summary>
        public bool CanFill(int goalIndex, Item item)
            => item != null
               && goalIndex >= 0 && goalIndex < Goals.Count
               && ShrineGoalRules.CanCredit(Goals[goalIndex], item.QualifiedItemId, item.Quality, item.Stack);

        /// <summary>The first open goal <paramref name="item"/> can fill, or -1.</summary>
        public int GoalIndexFor(Item item)
        {
            for (int i = 0; i < Goals.Count; i++)
                if (CanFill(i, item)) return i;
            return -1;
        }

        /// <summary>Donate exactly the goal's stack out of <paramref name="item"/>, which must sit in
        /// the player's inventory. Marks the goal deposited, pays its JP and refreshes the quest.
        /// Returns false and changes nothing when the item cannot fill the goal.</summary>
        public bool Donate(int goalIndex, Item item)
        {
            if (!CanFill(goalIndex, item))
            {
                _monitor.Log($"Shrine donate refused: {item?.QualifiedItemId ?? "nothing"} x{item?.Stack ?? 0} cannot fill goal {goalIndex}.", LogLevel.Trace);
                return false;
            }
            Farmer who = Game1.player;
            int slot = who?.Items == null ? -1 : who.Items.IndexOf(item);
            if (slot < 0)
            {
                _monitor.Log($"Shrine donate refused: {item.QualifiedItemId} is not in the inventory.", LogLevel.Warn);
                return false;
            }

            ShrineGoal goal = Goals[goalIndex];
            if (item.Stack == goal.Stack)
                who.removeItemFromInventory(item);
            else
                item.Stack -= goal.Stack;
            goal.Deposited = true;

            Rarity rarity = ItemRarityResolver.Resolve(goal.ItemId, _config.RarityThresholds);
            double listMultiplier = goal.ListIndex == SecondList ? Run.SecondGoalMultiplier : Run.CurrentGoalMultiplier;
            long jp = ShrineGoalRules.DonationJp(Jp.PerItem(rarity, Run.WeekOfYear), goal.Stack,
                _config.SelectionBonusMultiplier, listMultiplier);
            jp = JpBoostHelper.Apply(_store.State, jp);
            _store.State.JunimoPoints += jp;
            _monitor.Log(
                $"Shrine goal {goalIndex} donated: {goal.Stack}x {goal.ItemId} ({rarity}, list {goal.ListIndex}) -> +{jp} JP " +
                $"(bonus x{_config.SelectionBonusMultiplier}, card x{listMultiplier}, now {_store.State.JunimoPoints}).",
                LogLevel.Info);

            AfterDonation?.Invoke();
            return true;
        }
    }
}
