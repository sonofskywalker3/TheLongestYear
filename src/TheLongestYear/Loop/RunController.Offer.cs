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
        /// <summary>Open the planning hub for a specific upcoming week. Sunday-night flow passes
        /// <c>Run.WeekOfYear + 1</c> (and a <paramref name="seasonOverride"/> on day 28) so the
        /// offer pool reflects what the player is actually choosing for.</summary>
        public void PresentOffer(int? targetWeekOfYear = null, CoreSeason? seasonOverride = null)
        {
            int week = targetWeekOfYear ?? Run.WeekOfYear;

            if (Run.OfferPresentedWeek == week)
            {
                _monitor.Log($"PresentOffer: already shown for week {week}, skipping.", LogLevel.Trace);
                return;
            }

            var selectionsForOffer = seasonOverride.HasValue
                ? (System.Collections.Generic.IReadOnlyCollection<Theme>)System.Array.Empty<Theme>()
                : Run.SelectedThemesThisMonth;
            var offer = OfferFor(week, seasonOverride ?? Run.Season, selectionsForOffer);

            // Backstop (khauser13 soft lock): the hub has no close button by design — a forced
            // 1-of-N choice — so an EMPTY offer would hard-lock the player. The month rollover
            // fix in OnRunLoaded removes the known cause (a stale month exhausting all 5 themes),
            // but if any state ever exhausts the pool again, skip the week instead of locking:
            // mark it presented (no theme, no bonus, no liability) and move on.
            if (offer.Count == 0)
            {
                _monitor.Log(
                    $"Week {week} offer is EMPTY (selected this month: " +
                    $"[{string.Join(",", Run.SelectedThemesThisMonth)}]). Skipping the theme pick " +
                    "this week instead of opening an unclosable hub.",
                    LogLevel.Warn);
                Run.OfferPresentedWeek = week;
                _deferredOffer = null;
                // This week's own offer (not a day-28 pre-pick for next month, which BeginNewMonth
                // clears anyway): end last week's pick, goals and drawback so the week really runs
                // with none of them.
                if (!seasonOverride.HasValue && week == Run.WeekOfYear)
                {
                    Run.SkipWeek();
                    ActiveEffectsProvider.Clear();
                    _questService?.OnThemeSelected();   // drops last week's quest, adds none
                }
                return;
            }

            string seasonTag = seasonOverride.HasValue ? $" (for {seasonOverride.Value})" : "";
            // The face-down card shows as "?" at Info; the real offer goes to Trace (final review I2).
            RandomizerSettings offerRand = RandomizerForWeekPeek(week);
            // A stored re-roll replaces the seeded pair when the hub opens, so log the pair that will show.
            var restored = Run.RerolledOfferFor(week, selectionsForOffer);
            var shown = restored ?? offer;
            string restoredTag = restored != null ? " [restored re-roll]" : "";
            bool doubleOffer = IsDoubleWeekOffer(week, shown.Count, prePick: seasonOverride.HasValue);
            _monitor.Log(
                $"Week {week}{seasonTag} selection offer: " +
                $"{string.Join(" OR ", CardMultiplier.OfferLabels(shown, Run.Seed, week, offerRand, doubleOffer))}{restoredTag}{(doubleOffer ? " [double week]" : "")} (opening planning hub).",
                LogLevel.Info);
            if (CardMultiplier.AnySealed(shown.Count, Run.Seed, week, offerRand, doubleOffer))
                _monitor.Log($"Week {week} offer with the face-down card: {string.Join(" OR ", shown)}.", LogLevel.Trace);

            bool opened = _launcher?.OpenWeeklyHub(seasonOverride) ?? false;
            if (opened)
            {
                // Mark the week presented ONLY once the hub is genuinely on screen. Advancing
                // it before a confirmed open (the original bug) marks the week "offered" even
                // when the open was refused, so the day-start guard never re-fires it and the
                // theme picker is lost for the week (2026-06-05 playtest: win → "start a new
                // loop" → no theme picker, because the keep-playing DialogueBox was still the
                // active menu when this ran).
                Run.OfferPresentedWeek = week;
                _deferredOffer = null;
            }
            else
            {
                // Surface busy (menu/cutscene up). Keep OfferPresentedWeek unadvanced and stash
                // the offer; TryDrainDeferredOffer re-attempts it once activeClickableMenu clears.
                _deferredOffer = (week, seasonOverride);
                _monitor.Log(
                    $"Planning hub blocked (a menu/cutscene is up); deferring the week {week} offer to a free tick.",
                    LogLevel.Trace);
            }
        }

        /// <summary>Re-attempt a planning-hub open that <see cref="PresentOffer"/> deferred because
        /// the menu surface was busy. ModEntry's update loop calls this each tick once
        /// <c>Game1.activeClickableMenu</c> is clear; a no-op when nothing is pending.</summary>
        public void TryDrainDeferredOffer()
        {
            if (_deferredOffer is not { } pending)
                return;
            // Clear first so a still-blocked re-open simply re-stashes via PresentOffer rather
            // than looping. The week is not yet marked presented, so PresentOffer's own guard
            // lets the retry through.
            _deferredOffer = null;
            // A deferred offer for a week that's no longer current is garbage (the week rolled
            // over, or a reset replaced the run while it sat pending) — drop it instead of
            // presenting a picker for a week the player is no longer in.
            if (pending.week != Run.WeekOfYear)
            {
                _monitor.Log(
                    $"Dropping stale deferred offer for week {pending.week} (current week {Run.WeekOfYear}).",
                    LogLevel.Trace);
                return;
            }
            PresentOffer(pending.week, pending.season);
        }
    }
}
