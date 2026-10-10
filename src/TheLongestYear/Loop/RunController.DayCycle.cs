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
        public void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Runs before the cutscene early-return: vanilla's morning ownership pass has already
            // blanked an unowned/unnamed horse by now, and it must be repaired on day-28 mornings too.
            HorseCarryoverService.EnsureHorseNamed(_store.State, _monitor);
            // Same slot: a Keep Pet save where an extra pet has no bowl (pre-0.16.3 restores)
            // gets one now rather than at the next rewind.
            PetCarryoverService.EnsureBowlsForAllPets(_store.State, _monitor);

            if (_pendingCutscene != Day28Branch.None)
            {
                // A day-28 outcome is queued. The Day28CutsceneDriver plays the in-bed Junimo
                // scene this morning (UpdateTicked), then calls OnCutsceneEnded() to open the
                // JP shop + reset (Fail) or roll into the next season (Continue). Suppress the
                // normal season-sync/hub flow until the scene resolves — same shape as the old
                // _pendingReset early-return. Manual tly_reset intentionally stays raw.
                // Releases the driver's Restart branch (DayStartedWhileBranchPending).
                _dayStartedWhileBranchPending = true;
                // Yesterday's wildcard twist (max luck) must not reach the boosts' morning pass.
                DayEffects.Clear();
                // A voluntary restart resets as soon as the morning is clear; keep the new day's
                // date off the screen until the world is back on Spring 1.
                if (_pendingCutscene == Day28Branch.Restart)
                    Game1.displayHUD = false;
                _monitor.Log($"Day start: the {_pendingCutscene} branch is pending; the day-start flow waits for it.", LogLevel.Trace);
                return;
            }

            DoDayStartSeasonAndHub();
        }

        /// <summary>The post-reset (or no-reset-this-day) day-start work: sync season from
        /// Game1.season, advance month if it changed, fire the week-start planning hub if
        /// it's a week-start morning. Extracted 2026-05-29 so the reset path can defer
        /// through a JP-spend popup and still re-enter this same logic via the menu's
        /// exitFunction callback.</summary>
        private void DoDayStartSeasonAndHub()
        {
            // Sync state from the game date; a new month clears the month's selections (the
            // previous-day's Sunday-night day-28 pre-pick is consumed inside BeginNewMonth →
            // CurrentSelection).
            var season = (CoreSeason)(int)Game1.season;
            // Last week's discounted goal lines get their full ask back before the month rolls
            // over and before the hub previews this week's goals (spec 2026-09-29-theme-week-discount).
            RevertWeekDiscountIfStale(Calendar.WeekOfYear((int)season, Game1.dayOfMonth));
            if (season != Run.Season)
            {
                Run.BeginNewMonth(season);
                // BeginNewMonth may have just installed NextMonthSelection as CurrentSelection;
                // the bonus list still reflects last month's season, so re-sample now.
                if (Run.CurrentSelection.HasValue)
                {
                    PopulateBonusSlotsForCurrentSelection();
                    // The card was shown with this week's settings; pick the drawback the same way.
                    Run.CurrentLiabilityId = RandomPairing.LiabilityFor(Run.Seed, Run.WeekOfYear,
                        Run.CurrentSelection.Value, RandomizerForWeekPeek(Run.WeekOfYear).RandomPairings);
                    var (bonus, liability) = RandomPairing.EffectsFor(Run, Run.CurrentSelection.Value);
                    ActiveEffectsProvider.Set(bonus, liability);
                    _monitor.Log(
                        $"Day-28 pre-pick applied: {Run.CurrentSelection} is the week-1 selection of {season} " +
                        $"(goal JP {CardMultiplier.Format(Run.CurrentGoalMultiplier)}).",
                        LogLevel.Info);

                    ApplyEmptyPoolLiftIfNeeded();

                    // Spawn the weekly tracker for the freshly-activated pre-pick.
                    _questService?.OnThemeSelected();

                    // Same reasoning as SelectByName/OnRunLoaded: vanilla already ran the day's
                    // spawnObjects pass, so a freshly-activated forage_off has to clean up.
                    if (liability == "forage_off")
                        SweepExistingForage();
                }
            }
            Run.Season = season;
            Run.DayOfMonth = Game1.dayOfMonth;

            // Wildcard days: plan the week (week start) and reveal today's twist, before the offer.
            _wildcard?.OnDayStarted(() => RandomizerForWeekPeek(Run.WeekOfYear));

            // Open the weekly planning hub on every week-start morning (days 1, 8, 15, 22).
            // This replaces the prior Sunday-night DayEnding trigger, which fired during the
            // sleep/save sequence when Game1.player.CanMove == false — MenuLauncher.CanOpen
            // blocked the menu and the hub never appeared (2026-05-26 playtest:
            // "Cannot open menu: cutscene or input lock" right before "starting spring 8").
            // OnDayStarted runs after the wake-up cutscene resolves, so the menu opens cleanly.
            //
            // OfferPresentedWeek guards against re-firing within the same week (e.g. if the
            // player saves + reloads on day 8 morning).
            // On the fresh-run intro morning the IntroSequenceDriver opens the picker at the end
            // of the Lewis->Junimo chain. Suppress the normal auto-open here so it doesn't pop on
            // the black save-creation screen (and so it isn't opened twice).
            bool introWillOpenPicker = TheLongestYear.Core.Intro.IntroGate.IsFreshIntroMorning(
                _store.State.HasSeenIntro, Run.Season, Run.DayOfMonth);

            if (!introWillOpenPicker
                && Calendar.IsWeekStart(Run.DayOfMonth)
                && Run.OfferPresentedWeek != Run.WeekOfYear)
            {
                // CurrentSelection from a previous week intentionally persists until the next
                // pick overwrites it — the hub still opens to let the player choose this week's
                // theme. OfferPresentedWeek is per-week + persists across save reload, so the
                // hub fires exactly once per week (no repeat on load).
                PresentOffer(targetWeekOfYear: Run.WeekOfYear);
            }
        }

        public void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            // Kitchen bonus: tonight's FarmAnimal.dayUpdate writes new records; yesterday's are done.
            (Run.DoubleProduceToday ??= new System.Collections.Generic.List<DoubleProduceRecord>()).Clear();
            // A new night: this morning's DayStarted mark (RunController.Restart.cs) no longer counts.
            _dayStartedWhileBranchPending = false;
            // Wildcard extra growth: tonight's crop pass sees tomorrow's date, so flag it now.
            _wildcard?.OnDayEnding();
            // Deja-vu familiarity: read today's talk/gift flags before vanilla clears them overnight.
            TheLongestYear.Integration.FamiliarityGlue.Rollup(_store.State, Run, _monitor);
            TheLongestYear.Integration.VaultPaymentSync.Reconcile(Run);
            // Mirror the ledger from the board before the gate reads it, so the gate judges exactly
            // what the player sees on the board (a deposit the observer missed cannot fail an
            // otherwise-complete season, beta report khauser13; a phantom credit cannot pass one).
            int mirroredTonight = TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            _monitor.Log($"Day end: {mirroredTonight} filled slot(s) mirrored from the board for the gate.", LogLevel.Trace);
            if (_pendingCutscene == Day28Branch.Restart)
            {
                // Voluntary restart (RunController.Restart.cs): the player chose to rewind tonight.
                // The gate does not judge this night: no checkpoint JP, and no second outcome may
                // overwrite the queued Restart. A room finished today must not play its restoration
                // scene just before the rewind undoes it, same as a Fail night.
                SuppressResetDoomedRoomScenes();
                QuietRestartNight();
                _monitor.Log("Voluntary restart night: the day-end gate is skipped; the rewind runs in the morning.", LogLevel.Info);
                return;
            }
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State, TheLongestYear.Integration.VaultBundleMap.Count());
            RunAction action = _runManager.EvaluateDayEnd(Run, _requirements, vaultGateSatisfied);
            switch (action)
            {
                case RunAction.Continue:
                    break;

                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    // Season-checkpoint award (spec 2026-07-14 economy Change 2): pays at the ENTERING
                    // season's multiplier so progressing always out-earns re-farming spring.
                    long checkpointJp = JpBoostHelper.Apply(_store.State, Jp.CheckpointBonus(Run.WeekOfYear + 1));
                    _store.State.JunimoPoints += checkpointJp;
                    _monitor.Log(
                        $"Season checkpoint passed -> +{checkpointJp} JP (now {_store.State.JunimoPoints}).",
                        LogLevel.Info);
                    Game1.addHUDMessage(new HUDMessage(
                        Strings.Get("hud.checkpoint-award", new Dictionary<string, string> { ["jp"] = checkpointJp.ToString() }),
                        HUDMessage.newQuest_type));
                    // Queue the "great job, next season" Junimo cutscene for the morning. The
                    // game still advances the date; OnCutsceneEnded → DoDayStartSeasonAndHub
                    // clears the month's selections and opens the planning hub after the scene.
                    _pendingCutscene = Day28Branch.Continue;
                    break;

                case RunAction.FailReset:
                    // The morning rewind un-restores every CC room. Strip any room the player
                    // FINISHED TODAY out of mailForTomorrow so its overnight restoration scene
                    // (the bus/greenhouse/minecart WorldChangeEvent) never plays — otherwise the
                    // player watches the Junimos lovingly fix the bus seconds before the loop
                    // rewinds it broken again (user feedback 2026-06-08). This also removes the
                    // bus-scene-vs-reset race that was the mechanism of #1b.
                    SuppressResetDoomedRoomScenes();
                    _pendingCutscene = Day28Branch.Fail;
                    break;

                case RunAction.Win:
                    // 2026-05-29 continue-after-victory: only award the win-JP + queue the
                    // post-win choice popup on the FIRST win this playthrough. Subsequent
                    // Winter 28 wins (after the player chose Keep playing) re-fire RunAction.Win
                    // but should be silent — we don't want to double-pay JP or re-ask the
                    // question we already answered.
                    if (!_store.State.VictoryAcknowledged)
                    {
                        // Queue the win screen for the morning. Routes through the same
                        // Day28CutsceneDriver/OnCutsceneEnded path as Fail/Continue (the driver
                        // opens VictoryMenu for the Win branch); OnCutsceneEnded then opens the
                        // JP shrine and the keep-playing choice. Replaces the old _pendingWinChoice.
                        _pendingCutscene = Day28Branch.Win;
                    }
                    break;
            }
            // Hub trigger now lives in OnDayStarted (above) — see note there. Sunday-night
            // DayEnding fires while the player can't open menus.
        }

        /// <summary>Remove this-night's CC room-restoration mail so the matching overnight
        /// WorldChangeEvent doesn't play on a fail loop (the rewind un-restores the room, so the
        /// scene would show a repair the world is about to undo). Only rooms completed TODAY carry a
        /// mailForTomorrow entry — rooms finished on earlier days already played their scene and are
        /// untouched. A no-op (other than a trace) when nothing was finished today. The list +
        /// removal live in <see cref="CcRestorationMail"/> (shared with the reset purge).</summary>
        private void SuppressResetDoomedRoomScenes()
        {
            var stripped = CcRestorationMail.PurgeFromMailForTomorrow(Game1.player);
            if (stripped.Count > 0)
                _monitor.Log(
                    $"Fail loop: suppressed {stripped.Count} reset-doomed CC restoration scene(s) " +
                    $"([{string.Join(", ", stripped)}]) — no \"fix the bus\" cutscene before the rewind undoes it.",
                    LogLevel.Info);
            else
                _monitor.Log("Fail loop: no CC room finished today, so no overnight restoration scene to suppress.", LogLevel.Trace);
        }

        private static CoreSeason NextSeason(CoreSeason s) => s switch
        {
            CoreSeason.Spring => CoreSeason.Summer,
            CoreSeason.Summer => CoreSeason.Fall,
            CoreSeason.Fall => CoreSeason.Winter,
            _ => CoreSeason.Spring
        };
    }
}
