using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Loop
{
    internal sealed partial class RunController
    {
        /// <summary>Select one of this week's offered themes (driven by the UI; debug command + UI).</summary>
        /// <param name="slot">The hub card position the pick came from (0 left, 1 right); it sets
        /// this week's goal multiplier. -1 (console and debug picks) pays 1x.</param>
        public void SelectByName(string themeName, bool skipOfferCheck = false, int slot = -1)
        {
            if (!Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
            {
                _monitor.Log($"Unknown theme '{themeName}'. Options: {string.Join(", ", Enum.GetNames(typeof(Theme)))}.", LogLevel.Warn);
                return;
            }

            // skipOfferCheck = true: invoked from the playtest re-roll path on the hub. The
            // canonical OfferForWeek is seeded-deterministic and reflects only the originally
            // rolled pair, so a rerolled theme would always be rejected here. The reroll path
            // already filters against SelectedThemesThisMonth, so the only invariant we'd
            // lose by skipping is "the theme was in this week's official offer" — by design.
            if (!skipOfferCheck)
            {
                var offer = OfferFor(Run.WeekOfYear, Run.Season, Run.SelectedThemesThisMonth);
                if (!offer.Contains(theme))
                {
                    _monitor.Log($"{theme} is not offered this week. Offer: {string.Join(", ", offer)}.", LogLevel.Warn);
                    return;
                }
            }

            // A re-pick or re-roll replaces the goal lines, so the old ones go back to full first.
            RevertWeekDiscount("re-pick");
            Run.Select(theme);
            RandomizerSettings rand = RandomizerForWeekPeek(Run.WeekOfYear);
            Run.CurrentLiabilityId = RandomPairing.LiabilityFor(Run.Seed, Run.WeekOfYear, theme, rand.RandomPairings);
            Run.CurrentGoalMultiplier = slot < 0 ? 1.0 : CardMultiplier.ForCard(Run.Seed, Run.WeekOfYear, theme, slot, rand);
            CommitSelection();
        }

        /// <summary>Double theme week (spec section 6): take both cards. The first card keeps every
        /// single-pick rule (left slot multiplier, its own drawback); the second gets its own drawback,
        /// right slot multiplier and goal list (minus any line the first card already owns).</summary>
        public void SelectBoth(Theme first, Theme second, bool skipOfferCheck = false)
        {
            if (!skipOfferCheck)
            {
                var offer = OfferFor(Run.WeekOfYear, Run.Season, Run.SelectedThemesThisMonth);
                if (!offer.Contains(first) || !offer.Contains(second))
                {
                    _monitor.Log($"{first} + {second} is not this week's offer. Offer: {string.Join(", ", offer)}.", LogLevel.Warn);
                    return;
                }
            }

            RevertWeekDiscount("re-pick");
            Run.Select(first);
            RandomizerSettings rand = RandomizerForWeekPeek(Run.WeekOfYear);
            Run.CurrentLiabilityId = RandomPairing.LiabilityFor(Run.Seed, Run.WeekOfYear, first, rand.RandomPairings, otherCard: second);
            Run.CurrentGoalMultiplier = CardMultiplier.ForCard(Run.Seed, Run.WeekOfYear, first, FirstCardSlot, rand, doubleWeek: true);
            Run.SelectSecond(second);
            Run.SecondLiabilityId = RandomPairing.LiabilityFor(Run.Seed, Run.WeekOfYear, second, rand.RandomPairings, otherCard: first);
            Run.SecondGoalMultiplier = CardMultiplier.ForCard(Run.Seed, Run.WeekOfYear, second, SecondCardSlot, rand, doubleWeek: true);
            CommitSelection();
        }

        private const int FirstCardSlot = 0, SecondCardSlot = 1;
        private const string ForageOffLiability = "forage_off";

        /// <summary>The shared tail of a pick: consume the offer, sample the goals, set the effects,
        /// add the quest(s) and sweep forage. Run.Select (and SelectSecond on a double week) already ran.</summary>
        private void CommitSelection()
        {
            Theme theme = Run.CurrentSelection.Value;
            // A made pick CONSUMES the week's offer, however it was made (hub card, rerolled
            // card, console). Mark the week presented and drop any deferred re-present for it —
            // otherwise a stale deferred offer (stashed while a picker was already up) drains the
            // moment the pick closes the hub and opens a SECOND picker, whose next pick then
            // overwrites this one (2026-07-09 playtest: picked Farming, ghost picker popped,
            // Mixed overwrote Farming).
            Run.OfferPresentedWeek = Run.WeekOfYear;
            if (_deferredOffer is { } stale && stale.week == Run.WeekOfYear)
                _deferredOffer = null;
            PopulateBonusSlotsForCurrentSelection();
            var (bonus, liability) = RandomPairing.EffectsFor(Run, theme);
            ActiveEffectsProvider.Set(bonus, liability);
            string liability2 = null;
            if (Run.SecondSelection is Theme second)
            {
                var (bonus2, secondLiability) = RandomPairing.SecondEffectsFor(Run, second);
                liability2 = secondLiability;
                ActiveEffectsProvider.SetSecond(bonus2, liability2);
            }
            ApplyEmptyPoolLiftIfNeeded();
            _monitor.Log(
                $"Selected {theme} (bonus {bonus}, liability {liability}, goal JP {CardMultiplier.Format(Run.CurrentGoalMultiplier)}). " +
                $"Goal slots this week: [{string.Join(", ", Run.CurrentWeekBonusSlots.Select(s => $"{s.ItemId}@{s.BundleName}#{s.IngredientIndex}"))}].",
                LogLevel.Info);
            if (Run.SecondSelection is Theme also)
                _monitor.Log(
                    $"Double week: also selected {also} (bonus {ActiveEffectsProvider.SecondBonusId}, liability {liability2}, " +
                    $"goal JP {CardMultiplier.Format(Run.SecondGoalMultiplier)}). Second goal slots: " +
                    $"[{string.Join(", ", Run.SecondWeekBonusSlots.Select(s => $"{s.ItemId}@{s.BundleName}#{s.IngredientIndex}"))}].",
                    LogLevel.Info);

            // Surface the weekly theme + bonus checklist as a quest entry.
            _questService?.OnThemeSelected();

            // Sweep already-spawned forage if forage_off just activated — vanilla's day-start
            // spawnObjects already ran on each outdoor location during the load/sleep sequence
            // (well before the player got to the planning hub), so the Harmony prefix on
            // future spawnObjects calls is too late for today's wild forage on the maps.
            if (liability == ForageOffLiability || liability2 == ForageOffLiability)
                SweepExistingForage();
        }

        /// <summary>Sample the per-week goal slots for the current selection and store them on
        /// RunState. Clears the legacy id list so post-migration saves stop carrying it.</summary>
        private void PopulateBonusSlotsForCurrentSelection()
        {
            RevertWeekDiscount("goals re-sampled");
            Run.CurrentWeekBonusSlots.Clear();
            Run.CurrentWeekBonusItems.Clear();
            if (!Run.CurrentSelection.HasValue) return;
            var sample = SampleSlotsForTheme(Run.CurrentSelection.Value, Run.Season, Run.WeekOfYear);
            Run.CurrentWeekBonusSlots.AddRange(sample);
            // Double week: the second card's list, without any line the first card already owns.
            (Run.SecondWeekBonusSlots ??= new System.Collections.Generic.List<BonusSlot>()).Clear();
            if (Run.SecondSelection is Theme second)
                Run.SecondWeekBonusSlots.AddRange(GoalLists.Dedupe(
                    Run.CurrentWeekBonusSlots, SampleSlotsForTheme(second, Run.Season, Run.WeekOfYear)));
            ApplyWeekDiscount();
            // Random shrine donations: top each list up (only lists that have none, so a reload never re-rolls).
            RollShrineGoals();
        }

        /// <summary>Empty goal pool (everything for this theme already donated): no quest this
        /// week, drawback auto-lifted, no weekly JP bonus (spec 2026-07-09 §3). The
        /// LiabilitySuppressedThisWeek flag doubles as the completion-reward idempotency guard,
        /// so setting it here also prevents a later JP payout.</summary>
        private void ApplyEmptyPoolLiftIfNeeded()
        {
            LiftFirstListIfEmpty();
            LiftSecondListIfEmpty();
        }

        private void LiftFirstListIfEmpty()
        {
            if (!Run.CurrentSelection.HasValue) return;
            // Shrine goals (Randomizer) keep the list alive: the lift needs both kinds empty.
            if (!ShrineGoalRules.ListIsEmpty(Run.CurrentWeekBonusSlots.Count, ShrineGoalRules.OfList(Run.CurrentWeekShrineGoals, FirstShrineList))) return;
            if (Run.LiabilitySuppressedThisWeek) return;

            Run.LiabilitySuppressedThisWeek = true;
            ActiveEffectsProvider.SuppressLiability();
            // A double week names the theme, since the other list's drawback stays on.
            Game1.addHUDMessage(new HUDMessage(
                Run.IsDoubleWeekSelection
                    ? Strings.Get("hud.nothing-to-donate-named", new Dictionary<string, string> { ["theme"] = ThemeDisplay.Name(Run.CurrentSelection.Value) })
                    : Strings.Get("hud.nothing-to-donate"),
                HUDMessage.newQuest_type));
            _monitor.Log(
                $"Weekly goal pool for {Run.CurrentSelection} is empty (all in-play slots donated) - " +
                "no quest this week; drawback auto-lifted, no weekly JP bonus.",
                LogLevel.Info);
        }

        /// <summary>Double week: the second list lifts its own drawback when it is empty (every line
        /// it could ask for is donated, or the first card owns them all).</summary>
        private void LiftSecondListIfEmpty()
        {
            if (Run.SecondSelection is not Theme second) return;
            if (!ShrineGoalRules.ListIsEmpty(Run.SecondWeekBonusSlots.Count, ShrineGoalRules.OfList(Run.CurrentWeekShrineGoals, SecondShrineList))) return;
            if (Run.SecondLiabilitySuppressedThisWeek) return;

            Run.SecondLiabilitySuppressedThisWeek = true;
            ActiveEffectsProvider.SuppressSecondLiability();
            Game1.addHUDMessage(new HUDMessage(
                Strings.Get("hud.nothing-to-donate-named", new Dictionary<string, string> { ["theme"] = ThemeDisplay.Name(second) }),
                HUDMessage.newQuest_type));
            _monitor.Log(
                $"Second weekly goal pool for {second} is empty - no second quest; its drawback auto-lifted, no weekly JP bonus for it.",
                LogLevel.Info);
        }

        /// <summary>Day-28 Sunday-night flow: store the player's pick for week 1 of next month.</summary>
        /// <param name="slot">The hub card position (0 left, 1 right); -1 pays 1x. The multiplier is
        /// stored now with the live settings the day-28 hub showed, and applied by BeginNewMonth.</param>
        public void PreSelectForNextMonth(Theme theme, int slot = -1)
        {
            Run.NextMonthSelection = theme;
            Run.NextMonthGoalMultiplier = slot < 0
                ? 1.0
                : CardMultiplier.ForCard(Run.Seed, Run.WeekOfYear + 1, theme, slot, _config.Randomizer ?? new RandomizerSettings());
            _monitor.Log(
                $"Pre-pick set: {theme} will be the week-1 selection of {NextSeason(Run.Season)} " +
                $"(goal JP {CardMultiplier.Format(Run.NextMonthGoalMultiplier)}).",
                LogLevel.Info);
        }
    }
}
