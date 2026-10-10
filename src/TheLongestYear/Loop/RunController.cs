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
    /// <summary>
    /// Drives the run loop: syncs RunState from the game date, opens the Sunday-night planning
    /// hub with the weekly selection offer, evaluates the day-end gate via RunManager + bundle
    /// requirements, and executes the action (fail → reset next morning, advance month → consume
    /// any day-28 pre-pick, win → log). JP banks live as donations happen (DonationService); nothing extra is awarded at run end.
    /// </summary>
    internal sealed partial class RunController
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly WorldResetService _reset;
        private readonly RunManager _runManager = new RunManager(new GateEvaluator());
        /// <summary>Built per call from the run's STAMPED difficulty profile, not cached in the
        /// constructor: a reset re-stamps the profile mid-session, so a calculator captured at
        /// construction time would keep paying the previous loop's rate for the rest of the
        /// session. The object is tiny, so building one per award costs nothing worth saving.</summary>
        private JpCalculator Jp =>
            new JpCalculator(_config.Jp, _store.State.EffectiveDifficulty(_config).JpEarnedFactor);
        private System.Collections.Generic.IReadOnlyList<CcItem> _catalog;
        // Not readonly: ReplaceRequirements (owned-bundle engine wiring) swaps this after a
        // reset regenerates the manifest -- see that method's doc comment.
        private System.Collections.Generic.IReadOnlyList<BundleRequirement> _requirements;

        /// <summary>Which day-28 bedtime cutscene is queued for the next morning (set in
        /// OnDayEnding from the gate's RunAction). The <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/>
        /// reads this, plays the in-bed Junimo scene, and calls <see cref="OnCutsceneEnded"/> when
        /// it ends. None = no cutscene (normal day). Replaces the old _pendingReset bool: the reset
        /// now runs AFTER the FAIL cutscene's JP shop instead of straight out of OnDayStarted.</summary>
        // Backed by the run-state (not a field) so tonight's queued outcome is written by the night
        // save and survives a quit before the morning scene resolves.
        private Day28Branch _pendingCutscene
        {
            get => Run.PendingDay28;
            set => Run.PendingDay28 = value;
        }

        /// <summary>Exposed for the driver's per-tick decision.</summary>
        public Day28Branch PendingCutscene => _pendingCutscene;
        private TheLongestYear.UI.MenuLauncher _launcher;
        private WeeklyThemeQuestService _questService;

        /// <summary>A planning-hub offer that couldn't open because the menu surface was busy
        /// (e.g. the post-win keep-playing <c>DialogueBox</c> still closing when the new loop's
        /// reset fired). Held here and re-attempted by <see cref="TryDrainDeferredOffer"/> once
        /// the surface clears, so a blocked open is retried instead of silently lost.</summary>
        private (int week, CoreSeason? season)? _deferredOffer;

        /// <summary>Classified bundle requirements for this run; exposed for the UI + donation layer.</summary>
        public System.Collections.Generic.IReadOnlyList<BundleRequirement> Requirements => _requirements;

        /// <summary>Swaps the run's requirement manifest. Exists solely for reset-time owned-
        /// bundle engine regeneration (see FinalizeReset, which calls this right after
        /// WorldResetService.PerformReset returns its freshly-generated manifest) -- requirements
        /// are otherwise fixed for the lifetime of a RunController instance.</summary>
        public void ReplaceRequirements(System.Collections.Generic.IReadOnlyList<BundleRequirement> requirements)
            => _requirements = requirements;

        /// <summary>Swaps the CcItem catalog — Vanilla mode re-classifies when another mod
        /// rewrites the board mid-day (ModEntry.ReclassifyIfBoardChanged).</summary>
        public void ReplaceCatalog(System.Collections.Generic.IReadOnlyList<CcItem> catalog)
            => _catalog = catalog;

        /// <summary>CcItem catalog (rarity/season/theme metadata); exposed so the UI can look up
        /// per-season obtainability when computing the bonus-item preview per card.</summary>
        public System.Collections.Generic.IReadOnlyList<CcItem> Catalog => _catalog;

        public RunController(IMonitor monitor, MetaStore store, GameplayConfig config, WorldResetService reset,
            System.Collections.Generic.IReadOnlyList<CcItem> catalog,
            System.Collections.Generic.IReadOnlyList<BundleRequirement> requirements = null)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _reset = reset;
            _catalog = (catalog != null && catalog.Count > 0) ? catalog : CcItemCatalog.Items;
            _requirements = requirements ?? new System.Collections.Generic.List<BundleRequirement>();
        }

        private RunState Run => _store.Run;

        /// <summary>This week's Randomizer settings (snapshotted at the first read of the week).</summary>
        internal RandomizerSettings Randomizer => Run.RandomizerFor(Run.WeekOfYear, _config.Randomizer);

        /// <summary>Settings for a week. For the current week this is <see cref="Randomizer"/>, which
        /// stores the week's snapshot on first read; a week that has not started is never
        /// snapshotted early (the pre-pick preview reads the live config).</summary>
        internal RandomizerSettings RandomizerForWeekPeek(int week)
            => week == Run.WeekOfYear ? Randomizer : _config.Randomizer;

        private const int DoubleWeekCards = 2;

        /// <summary>True when the offer for <paramref name="week"/> is a double theme week: the option
        /// is on for that week, it is the season's double week, the offer has both cards, and it is
        /// not the day-28 pre-pick hub. A double offer with fewer cards is a normal week.</summary>
        internal bool IsDoubleWeekOffer(int week, int offerCount, bool prePick)
            => !prePick
               && offerCount == DoubleWeekCards
               && DoubleWeek.Is(Run.Seed, week, (RandomizerForWeekPeek(week) ?? new RandomizerSettings()).DoubleThemeWeek);

        /// <summary>Per-group caps on a theme's weekly goal list: at most one fruit-tree fruit
        /// (Data/FruitTrees) and at most one crab-pot catch (Data/Fish trap rows); Jeff,
        /// 2026-08-28. Null = no caps.</summary>
        public System.Collections.Generic.IReadOnlyList<GoalGroupCap> GoalCaps { get; set; }

        /// <summary>Item kind classifier for the activity themes (Data/Objects Category and Type
        /// through ItemKindClassifier). Wired by ModEntry; the default matches nothing, which
        /// keeps Mixed at its Bulletin Board room until the game data is available.</summary>
        public Func<string, ItemKind> ItemKindOf { get; set; } = _ => ItemKind.Other;

        /// <summary>The current attempt count (loop the player is on / won), surfaced so the
        /// <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/> can pass it into the
        /// <see cref="TheLongestYear.UI.VictoryMenu"/> loop-count line.</summary>
        public int CurrentRunNumber => Run.RunNumber;

        /// <summary>Called from OnSaveLoaded: ensure the run has a seed.</summary>
        public void OnRunLoaded()
        {
            if (Run.Seed == 0)
                Run.Seed = NewSeed();

            // The ledger is a mirror of the CC board (spec 2026-08-29-per-slot-ledger). Re-read it
            // now: this is also the migration for saves whose ledger was the old id-only list.
            int mirrored = TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            if (mirrored >= 0)
                _monitor.Log($"Ledger mirrored from the CC board: {mirrored} slot(s) filled.", LogLevel.Info);

            // Month-rollover-on-load (khauser13 soft lock, 2026-06-10): when the player slept
            // into a new month and quit BEFORE completing its first day, the on-disk run-state
            // predates the rollover (vanilla saves before OnDayStarted runs BeginNewMonth). The
            // old blind `Run.Season = calendar` sync here destroyed the season mismatch that
            // DoDayStartSeasonAndHub uses to detect the rollover, so SelectedThemesThisMonth
            // kept LAST month's picks, accumulated past 4 themes, and OfferForWeek eventually
            // returned an EMPTY offer — an unclosable theme picker. Run the same rollover the
            // live day-start path would have: clear month state + consume the day-28 pre-pick,
            // then re-sample the bonus list for the new season. The effect/quest restore below
            // then operates on the rolled-over (correct) selection state.
            var calendarSeason = (CoreSeason)(int)Game1.season;
            if (Run.PendingDay28 != Day28Branch.None)
            {
                // Quit after the day-28 night save but before its morning scene resolved. Leave the
                // season and day alone: the driver replays the scene and its continuation decides
                // (Fail rewinds, Continue rolls over via DoDayStartSeasonAndHub, Win shows the screen).
                _monitor.Log(
                    $"Load is in {calendarSeason} with the day-28 {Run.PendingDay28} outcome still pending " +
                    $"(run-state {Run.Season} {Run.DayOfMonth}). Replaying it instead of rolling the month over.",
                    LogLevel.Info);
            }
            else if (Run.OwesMonthRolloverOnLoad(calendarSeason))
            {
                _monitor.Log(
                    $"Load is in {calendarSeason} but run-state was saved in {Run.Season} — the quit " +
                    "raced the month rollover. Running BeginNewMonth now (clears last month's theme " +
                    "selections; consumes any day-28 pre-pick).",
                    LogLevel.Info);
                RevertWeekDiscount("month rollover on load");
                Run.BeginNewMonth(calendarSeason);
                if (Run.CurrentSelection.HasValue)
                {
                    PopulateBonusSlotsForCurrentSelection();
                    // Same drawback the card showed, as the live rollover picks it (DoDayStartSeasonAndHub);
                    // BeginNewMonth's Select left it null, which fell back to the theme's default
                    // drawback with Random Pairings on.
                    Run.CurrentLiabilityId = RandomPairing.LiabilityFor(Run.Seed, Run.WeekOfYear,
                        Run.CurrentSelection.Value, RandomizerForWeekPeek(Run.WeekOfYear).RandomPairings);
                    // Empty new-season pool for the pre-picked theme: lift the drawback now, same
                    // as the live rollover paths (SelectByName/DoDayStartSeasonAndHub). Safe re
                    // ordering: the effects-restore block below calls ActiveEffectsProvider.Set
                    // (which clears suppression) but then re-applies SuppressLiability because
                    // this sets Run.LiabilitySuppressedThisWeek.
                    ApplyEmptyPoolLiftIfNeeded();
                }
            }
            if (Run.PendingDay28 == Day28Branch.None)
                Run.DayOfMonth = Game1.dayOfMonth;

            // A pre-Randomizer save loaded mid-week: that week ran with everything off, so a
            // Randomizer option turned on since must wait for the next weekly offer (final review M1).
            if (Run.SnapshotOffIfWeekAlreadyOffered())
                _monitor.Log(
                    $"No Randomizer snapshot for week {Run.WeekOfYear}, whose offer was already shown: " +
                    "keeping the Randomizer off until next week's offer.",
                    LogLevel.Info);

            // 2026-07-09 slot redesign migration: a mid-week save from an older version has the
            // legacy id-only bonus list but no slot goals. Re-sample once (the week's goals
            // re-roll — one-time, beta-acceptable) and rebuild the quest from slots.
            if (Run.CurrentSelection.HasValue
                && Run.CurrentWeekBonusSlots.Count == 0
                && Run.CurrentWeekBonusItems.Count > 0)
            {
                _monitor.Log(
                    "Migrating this week's bonus list to slot-based goals (one-time re-sample).",
                    LogLevel.Info);
                PopulateBonusSlotsForCurrentSelection();
                ApplyEmptyPoolLiftIfNeeded();
                _questService?.OnThemeSelected();
            }

            // One-time migration of the plan-4 boost fields (0.16.117 to 0.16.158, never released)
            // into the ActiveBoosts list (spec 2026-08-29 shrine tabs + JP Boosts, 1.2).
            int boostsBefore = Run.ActiveBoosts.Count;
            BoostState.MigrateLegacy(Run, Calendar.DayOfYear((int)Run.Season, Run.DayOfMonth));
            if (Run.ActiveBoosts.Count > boostsBefore)
                _monitor.Log($"Migrated {Run.ActiveBoosts.Count - boostsBefore} legacy boost flag(s) into ActiveBoosts.", LogLevel.Info);

            // One-time XP ladder respec (2026-08-30 rebalance, Nexus feedback from gazumbrado).
            // The x2..x5 tiers no longer exist, so anything bought under them is refunded at the
            // OLD prices and removed; the player re-buys under the +25%-per-tier ladder with the
            // same points. See XpLadderRespec for the ruling behind it.
            if (XpLadderRespec.IsOwed(_store.State))
            {
                long refunded = XpLadderRespec.Respec(_store.State, out int cleared);
                if (cleared > 0)
                    _monitor.Log(
                        $"XP upgrades were rebalanced: refunded {refunded} JP and cleared {cleared} " +
                        "skill experience upgrade(s). Re-buy them at the shrine under the new prices.",
                        LogLevel.Info);
            }

            _monitor.Log($"Run {Run.RunNumber} ready (seed {Run.Seed}). {DescribeWeek()}", LogLevel.Info);

            // Diagnostic: surface this save's resolved Vault (bus-repair) bundle indices + gold so a
            // remixed-bundle save's renumbered vault room (any indices, not only the vanilla 23-26)
            // is visible in the log. Confirms VaultBundleMap derived the right indices from live
            // bundle data without needing a vault-payment playtest.
            var vaultParts = new System.Collections.Generic.List<string>();
            foreach (int idx in TheLongestYear.Integration.VaultBundleMap.Indices())
                vaultParts.Add($"{idx}={TheLongestYear.Integration.VaultBundleMap.GoldForIndex(idx):N0}g");
            _monitor.Log(
                $"Vault bundles (this save): {(vaultParts.Count > 0 ? string.Join(", ", vaultParts) : "none in bundle data")}",
                LogLevel.Info);

            // Restore active effects from persisted selection (if any).
            if (Run.CurrentSelection.HasValue)
            {
                var (bonus, liability) = RandomPairing.EffectsFor(Run, Run.CurrentSelection.Value);
                ActiveEffectsProvider.Set(bonus, liability);
                _monitor.Log(
                    $"Restored active effects for week {Run.WeekOfYear}: theme={Run.CurrentSelection}, " +
                    $"bonus={bonus}, liability={liability}" +
                    (Run.LiabilitySuppressedThisWeek ? " (suppressed)" : ""),
                    LogLevel.Info);

                // Sync the persisted "quest complete = liability lifted" state — the provider
                // resets to unsuppressed on Set above, so re-apply if the run-state flag is on.
                if (Run.LiabilitySuppressedThisWeek)
                    ActiveEffectsProvider.SuppressLiability();

                // Double week: the second card's entry, with its own lifted state.
                if (Run.SecondSelection is Theme second)
                {
                    var (bonus2, liability2) = RandomPairing.SecondEffectsFor(Run, second);
                    ActiveEffectsProvider.SetSecond(bonus2, liability2);
                    if (Run.SecondLiabilitySuppressedThisWeek)
                        ActiveEffectsProvider.SuppressSecondLiability();
                    _monitor.Log(
                        $"Restored double-week second entry: theme={second}, bonus={bonus2}, liability={liability2}" +
                        (Run.SecondLiabilitySuppressedThisWeek ? " (suppressed)" : ""),
                        LogLevel.Info);
                }

                // 2026-05-28 fix: do NOT re-sweep on save load even if forage_off is active.
                // The sweep only needs to run when the theme is FRESHLY selected (via
                // SelectByName or BeginNewMonth's pre-pick activation) — that's when vanilla
                // has just spawned forage that we need to clean up. On a save reload the
                // forage state already reflects whatever was on the map at save time, and
                // ForageOffPatch's per-spawn suppression continues to block future spawns.
                // Re-sweeping on reload destroyed a user's intent to re-pick the week's
                // theme: they reloaded Spring 1 to switch from Mining → Foraging, but the
                // sweep had already wiped forage from a prior Mining-pick session.
            }
            else
            {
                ActiveEffectsProvider.Clear();
            }

            // Refresh the weekly quest's objective text against the restored ledger so a
            // save+reload mid-week reflects already-donated items.
            _questService?.OnRunLoaded();

            // Wildcard day: re-publish a twist already revealed today (never re-rolled).
            _wildcard?.OnRunLoaded();

            // Clear the stale vanilla "Rat Problem" quest (the CC is already open this run). The
            // Harmony prefix stops new adds; this strips it from a save that already received it.
            RatProblemQuestPatch.StripFromLog(_monitor);
        }

        /// <summary>
        /// Remove already-spawned wild forage from every outdoor non-mine location. Called when
        /// the active liability becomes forage_off, since vanilla's per-day forage spawn runs
        /// during newDay/save-load BEFORE the player picks the week's theme — meaning ForageOffPatch
        /// (which suppresses future spawns) can't catch day-1 forage. Without this sweep, the
        /// 2026-05-27 playtest found leek + horseradish on Spring 1 even after picking Mining.
        /// </summary>
        private void SweepExistingForage()
        {
            int removed = 0;
            int locationsTouched = 0;
            foreach (var loc in Game1.locations)
            {
                if (loc is StardewValley.Locations.MineShaft) continue;
                if (!loc.IsOutdoors) continue;

                var toRemove = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>();
                foreach (var pair in loc.objects.Pairs)
                {
                    if (pair.Value.IsSpawnedObject && pair.Value.isForage())
                        toRemove.Add(pair.Key);
                }
                foreach (var tile in toRemove)
                    loc.objects.Remove(tile);

                if (toRemove.Count > 0)
                {
                    locationsTouched++;
                    removed += toRemove.Count;
                }
            }

            _monitor.Log(
                $"forage_off sweep: removed {removed} spawned-forage objects from {locationsTouched} locations.",
                LogLevel.Info);
        }

        /// <summary>Wired by ModEntry after the launcher is constructed.</summary>
        public void AttachLauncher(TheLongestYear.UI.MenuLauncher launcher) => _launcher = launcher;

        /// <summary>Wired by ModEntry after the quest service is constructed. Drives the per-week
        /// quest in the player's quest log — created on theme selection, refreshed on donation,
        /// auto-completed when every goal slot is complete.</summary>
        public void AttachQuestService(WeeklyThemeQuestService quest) => _questService = quest;

        private WildcardDayService _wildcard;

        /// <summary>Wildcard days (Randomizer): planned and revealed from the day start.</summary>
        public void AttachWildcardService(WildcardDayService wildcard) => _wildcard = wildcard;

        private string DescribeWeek() => $"{Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}).";

        private static int NewSeed() => Guid.NewGuid().GetHashCode();
    }
}
