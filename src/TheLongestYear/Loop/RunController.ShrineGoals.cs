using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>Random shrine donations (Randomizer, spec section 7): each picked theme list is
    /// topped up to the Required Slots target with items that have no slot on the board, donated
    /// at the farm statue. Rolled once per pick, never re-rolled on load.</summary>
    internal sealed partial class RunController
    {
        private const int FirstShrineList = 0, SecondShrineList = 1;

        /// <summary>The engine pool items a theme can ask for (ThemeEffortPools). Wired by ModEntry.</summary>
        public Func<Theme, IReadOnlyList<string>> ShrineThemeIds { get; set; }

        /// <summary>Ids the engine pools exclude (ItemPools.ExcludedIds). Wired by ModEntry.</summary>
        public Func<IReadOnlySet<string>> ShrineExcludedIds { get; set; }

        /// <summary>The seasons an item can be had in (SeasonResolver). Wired by ModEntry.</summary>
        public Func<string, IReadOnlySet<CoreSeason>> SeasonsOf { get; set; }

        /// <summary>Roll the shrine goals of every picked list that has none yet. Does nothing (no
        /// rng draw, no write) when this week's snapshot has the option off.</summary>
        private void RollShrineGoals()
        {
            if (!(RandomizerForWeekPeek(Run.WeekOfYear) ?? new RandomizerSettings()).RandomShrineDonations) return;
            Run.CurrentWeekShrineGoals ??= new List<ShrineGoal>();
            RollShrineList(FirstShrineList, Run.CurrentSelection, Run.CurrentWeekBonusSlots);
            RollShrineList(SecondShrineList, Run.SecondSelection, Run.SecondWeekBonusSlots);
        }

        private void RollShrineList(int list, Theme? selection, IReadOnlyList<BonusSlot> ccSlots)
        {
            int ccGoals = ccSlots?.Count ?? 0;
            string[] ccItemIds = ccSlots?.Select(s => s.ItemId).ToArray() ?? Array.Empty<string>();
            if (selection is not Theme theme) return;
            if (Run.CurrentWeekShrineGoals.Any(g => g.ListIndex == list)) return;
            if (ShrineThemeIds == null || Availability == null || SeasonsOf == null)
            {
                _monitor.Log("Shrine goals: item data is not wired yet, none rolled.", LogLevel.Warn);
                return;
            }

            DifficultyProfile profile = _store.State.EffectiveDifficulty(_config);
            int count = ShrineGoalRules.CountFor(profile.Steps.RequiredSlots, ccGoals);
            if (count == 0)
            {
                _monitor.Log($"Shrine goals: {theme} already asks {ccGoals} CC goal(s), none needed.", LogLevel.Trace);
                return;
            }

            ShrineGoalRules.BoardRefs board = ShrineGoalRules.ReadBoard(Game1.netWorldState?.Value?.BundleData);
            IReadOnlyList<string> candidates = ShrineGoalRules.Candidates(
                ShrineThemeIds(theme), ShrineExcludedIds?.Invoke(), board, CategoryOfObject,
                alreadyTaken: Run.CurrentWeekShrineGoals.Select(g => g.ItemId));

            int week = Run.WeekOfYear;
            CoreSeason season = Run.Season;
            var allowed = new List<string>();
            var unplaced = new List<string>();
            foreach (string id in candidates)
            {
                // IsPlaced first: For() on an id nothing placed records it as unknown.
                if (!Availability.IsPlaced(id)) { unplaced.Add(id); continue; }
                ItemAvailability a = Availability.For(id);
                bool inSeason = SeasonsOf(id)?.Contains(season) ?? false;
                if (ShrineGoalSampler.IsAllowed(profile.Steps.ItemRarity, placed: true, a.GoalWeek,
                        a.HardWeekOrPacing, inSeason, week))
                    allowed.Add(id);
            }
            if (unplaced.Count > 0)
                _monitor.Log($"Shrine goals: skipped {unplaced.Count} {theme} item(s) the availability model cannot place: " +
                             string.Join(", ", unplaced) + ".", LogLevel.Trace);

            // The list's CC goals count toward the one-per-list group caps (RunController.GoalCaps).
            IReadOnlyList<string> picked = ShrineGoalSampler.Pick(Run.Seed, week, theme, allowed, count, GoalCaps,
                alreadyChosen: ccItemIds);
            Random rng = RollSeed.Rng(Run.Seed, week, ShrineGoalRules.StackSalt, (int)theme);
            ItemAvailabilityModel model = Availability;
            List<ShrineGoal> goals = ShrineGoalRules.Build(picked, list, profile, rng,
                id => QuantityAskPass.Covers(id, model) ? QuantityAskPass.BasisByDeadline(id, season, model) : null,
                (id, s) => CappedAsks.ClampStack(id, UnstackableAsks.ClampStack(id,
                    OncePerLoopAsks.ClampStack(id, s, profile.OncePerLoopAsksOne))),
                _store.State.BoardDifficulty(_config).EffectiveWeeklyGoalStackDiscount());
            Run.CurrentWeekShrineGoals.AddRange(goals);

            _monitor.Log(
                $"Shrine goals for {theme} (list {list}, week {week}): asked {count}, {allowed.Count} allowed of " +
                $"{candidates.Count} off-board item(s); [{string.Join(", ", goals.Select(g => $"{g.ItemId} x{g.Stack}"))}].",
                LogLevel.Info);
        }

        /// <summary>Data/Objects category of a qualified object id, or null for anything else.</summary>
        private static int? CategoryOfObject(string qualifiedId)
        {
            if (string.IsNullOrEmpty(qualifiedId) || !qualifiedId.StartsWith("(O)", StringComparison.Ordinal)) return null;
            return Game1.objectData != null && Game1.objectData.TryGetValue(BundleParsing.StripQualifier(qualifiedId), out var data)
                ? data.Category
                : null;
        }
    }
}
