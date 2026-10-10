using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    internal sealed partial class WeeklyHubMenu
    {
        /// <summary>The re-roll button's click: a paid reroll that cannot change the offer, or that
        /// the player cannot afford, is refused and takes nothing (final review T2: no charge, no
        /// count, no price step). On success the free or paid reroll runs. <paramref name="message"/>
        /// is the outcome line (also used by the tly_reroll paid command).</summary>
        private bool TryRerollFromButton(out string message)
        {
            // Off has no button; the tly_reroll paid route must not slip a free reroll past it.
            if (_rand.Rerolls == RerollMode.Off)
            {
                message = "Reroll refused: rerolls are off.";
                return false;
            }
            long cost = CurrentRerollCost();
            long before = _getJp?.Invoke() ?? 0;
            if (cost > 0)
            {
                if (cost > before)
                {
                    message = $"Reroll refused: costs {cost} JP, only {before} JP.";
                    return false;
                }
                if (!RerollCanChange())
                {
                    message = "Reroll refused: no other pair to show.";
                    return false;
                }
                _spendJp?.Invoke(cost);
            }
            RerollOffer();
            message = cost > 0
                ? $"Reroll paid {cost} JP (JP {before} -> {_getJp?.Invoke() ?? 0})"
                : "Reroll (free)";
            return true;
        }

        /// <summary>The reroll button's own click path, for tly_reroll paid.</summary>
        public string RerollPaidForDebug()
        {
            TryRerollFromButton(out string message);
            return message;
        }

        /// <summary>The card click for one side, for tly_select &lt;theme&gt; &lt;left|right&gt;: the real card
        /// path (goal multiplier and mystery card included). Fails if the theme is not on that side.</summary>
        public bool TryPickSide(string themeName, string side, out string error)
        {
            error = null;
            int slot;
            if (side.Equals("left", System.StringComparison.OrdinalIgnoreCase)) slot = LeftSlot;
            else if (side.Equals("right", System.StringComparison.OrdinalIgnoreCase)) slot = RightSlot;
            else { error = $"side must be left or right, got '{side}'."; return false; }
            if (!System.Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
            { error = $"unknown theme '{themeName}'."; return false; }
            if (_offer.Count <= slot || _offer[slot] != theme)
            {
                // The face-down card never shows its theme at Warn/Info; the real offer goes to Trace.
                error = $"{theme} is not on the {side} card (offer: [{string.Join(", ", CardMultiplier.OfferLabels(_offer, _run.Seed, OfferWeek, _rand, _double))}]).";
                _monitor.Log($"tly_select: real offer = [{string.Join(", ", _offer)}].", LogLevel.Trace);
                return false;
            }
            ConfirmSelection(theme, slot);
            if (!LastPickTook)
            { error = $"{theme} was rejected (already picked this month, or not a valid offer)."; return false; }
            double mult = _isPreSelectForNextMonth ? _run.NextMonthGoalMultiplier : _run.CurrentGoalMultiplier;
            if (_run.IsDoubleWeekSelection && !_isPreSelectForNextMonth)
                _monitor.Log(
                    $"Double week: selected {_run.CurrentSelection} (slot {LeftSlot}, goal JP {CardMultiplier.Format(_run.CurrentGoalMultiplier)}) " +
                    $"and {_run.SecondSelection} (slot {RightSlot}, goal JP {CardMultiplier.Format(_run.SecondGoalMultiplier)})",
                    LogLevel.Info);
            else
                _monitor.Log($"Selected {theme} (slot {slot}, goal JP {CardMultiplier.Format(mult)})", LogLevel.Info);
            return true;
        }

        /// <summary>One line per card for tly_hubcards: slot, theme (? plus the real theme at Trace when
        /// face down), drawback id and multiplier.</summary>
        public void LogCards()
        {
            for (int slot = 0; slot < _offer.Count; slot++)
            {
                Theme theme = _offer[slot];
                bool sealedCard = IsSealed(slot);
                string drawback = RandomPairing.LiabilityFor(_run.Seed, OfferWeek, theme, _rand.RandomPairings, OtherCard(slot));
                double mult = CardMultiplier.ForCard(_run.Seed, OfferWeek, theme, slot, _rand, _double);
                _monitor.Log(
                    $"Hub card slot {slot}: {(sealedCard ? CardMultiplier.SealedLabel : theme.ToString())}, " +
                    $"drawback {drawback}, goal JP {CardMultiplier.Format(mult)}{(sealedCard ? " (face down)" : "")}",
                    LogLevel.Info);
                if (sealedCard)
                    _monitor.Log($"Hub card slot {slot} is {theme}.", LogLevel.Trace);
            }
        }

        /// <summary>The week whose offer this hub shows: next month's week 1 on the day-28 pre-pick hub.</summary>
        private int OfferWeek => _isPreSelectForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;

        /// <summary>JP the next reroll charges: rerolls already made this week set the price.</summary>
        private long CurrentRerollCost()
            => RerollPricing.CostOf(_rand.Rerolls, _run.RerollWeek == OfferWeek ? _run.RerollCount : 0);

        /// <summary>Cached <see cref="RerollCycle.CanChange"/> for the offer on screen; cleared by
        /// <see cref="RerollOffer"/>. The candidates do not change while the hub is open.</summary>
        private bool? _rerollCanChange;

        private bool RerollCanChange()
            => _rerollCanChange ??= RerollCycle.CanChange(
                _runController.OfferCandidates(OfferWeek, _offerSeason, SelectionsForOffer), _offer);

        /// <summary>The picks the offer excludes. The day-28 pre-pick is for next month, so none
        /// (the same rule <see cref="MenuLauncher.OpenWeeklyHub"/> uses for the first offer).</summary>
        private IReadOnlyCollection<Theme> SelectionsForOffer => _isPreSelectForNextMonth
            ? System.Array.Empty<Theme>()
            : _run.SelectedThemesThisMonth;

        /// <summary>
        /// Regenerate the offer (Randomizer Rerolls setting). Salt the underlying seed with the
        /// week's re-roll count so the offer stays deterministic. Candidates are every theme not
        /// picked this month that can ask for at least one goal, and <see cref="RerollCycle"/>
        /// never repeats a pair shown this week until every pair has been shown (Nijah, Nexus
        /// 2026-09-28). The re-rolled offer, seen pairs and count are kept on the RunState for the
        /// offer week, so reopening the hub that week shows the same pair.
        /// </summary>
        private void RerollOffer()
        {
            int week = OfferWeek;
            if (_run.RerollWeek != week)
            {
                // Stale state from another week (or none): start this week's cycle.
                _run.ClearReroll();
                _rerollCounter = 0;
            }
            // The offer on screen counts as shown (the first offer, on the first re-roll).
            string onScreen = RerollCycle.PairKey(_offer);
            if (_offer.Count > 0 && !_run.RerollSeenPairs.Contains(onScreen))
                _run.RerollSeenPairs.Add(onScreen);

            _rerollCounter++;
            IReadOnlyList<Theme> candidates = _runController
                .OfferCandidates(week, _offerSeason, SelectionsForOffer);
            var rng = new System.Random(_run.Seed ^ (week * 7919) ^ (_rerollCounter * RerollSaltPrime));
            _offer = RerollCycle.Next(candidates, _run.RerollSeenPairs, _offer, rng).ToList();
            _double = ComputeDouble();
            _run.RecordReroll(week, _offer, _rerollCounter);
            _rerollCanChange = null;
            ResolvePerCardData();
            RecomputeBoundsAndLayout();
            // The face-down card shows as "?" at Info; the real offer goes to Trace (final review I2).
            _monitor.Log(
                $"WeeklyHubMenu reroll #{_rerollCounter}: offer = " +
                $"[{string.Join(", ", CardMultiplier.OfferLabels(_offer, _run.Seed, OfferWeek, _rand, _double))}].",
                LogLevel.Info);
            if (CardMultiplier.AnySealed(_offer.Count, _run.Seed, OfferWeek, _rand, _double))
                _monitor.Log($"WeeklyHubMenu reroll #{_rerollCounter}: offer with the face-down card = [{string.Join(", ", _offer)}].",
                    LogLevel.Trace);
        }

        /// <summary>The re-roll button, for the tly_reroll console command (works whether or not the
        /// button is enabled, so a headless run can press it).</summary>
        public void RerollForDebug() => RerollOffer();
    }
}
