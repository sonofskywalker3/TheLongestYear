using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>The darkness (spec 2026-09-09 pushback; rework 2026-09-14; Part B wiring
    /// 2026-09-15): ONE roll a night with a decaying weekly chance, one event per strike split
    /// evenly among what the season offers, and reversion and tampering that consult the
    /// obtainability model against the real save by Darkness level. Since 2026-09-21 the night pass
    /// PICKS at day end and the damage lands exactly once afterwards: in the overnight scene, or at
    /// once when no scene will play (<see cref="Pending"/>). Blight kills crops (BlightPass)
    /// or empties one chest (SpoilagePass); reversion opens a filled slot; tampering rewrites an
    /// unfilled slot's item and asks ModEntry to rebuild the catalog and requirements. Host only,
    /// single player or master, never on a day 28 or the win night (RunController decides that).</summary>
    internal sealed class SabotageService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly Func<IReadOnlyList<BundleRequirement>> _requirements;
        private readonly Func<ItemAvailabilityModel> _availability;
        private readonly Func<ItemPools> _pools;
        private readonly Func<ObtainabilityModel> _obtainability;
        private readonly Action<string> _rebuildBoard;
        /// <summary>Starts the board-changed porch scene; set by ModEntry (the season-turn driver).</summary>
        public Action<string, string, Action> StartTamperScene { get; set; }

        private RunState Run => _store.Run;
        private MetaState Meta => _store.State;
        private DifficultyStep Level => Meta.EffectiveDifficulty(_config).Darkness;

        public SabotageService(
            IMonitor monitor, MetaStore store, GameplayConfig config,
            Func<IReadOnlyList<BundleRequirement>> requirements,
            Func<ItemAvailabilityModel> availability,
            Func<ItemPools> pools,
            Func<ObtainabilityModel> obtainability,
            Action<string> rebuildBoard)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
            _obtainability = obtainability ?? throw new ArgumentNullException(nameof(obtainability));
            _rebuildBoard = rebuildBoard ?? throw new ArgumentNullException(nameof(rebuildBoard));
        }

        private static bool HostCanAct() => Game1.IsMasterGame && !Game1.IsMultiplayer;

        public bool Enabled(SabotageKind kind) => kind switch
        {
            SabotageKind.Blight => _config.EnableBlight,
            SabotageKind.Reversion => _config.EnableBundleReversion,
            SabotageKind.Tampering => _config.EnableRequirementTampering,
            _ => false,
        };

        private static SabotageKind KindOf(DarknessEvent e) => e switch
        {
            DarknessEvent.Reversion => SabotageKind.Reversion,
            DarknessEvent.Tampering => SabotageKind.Tampering,
            _ => SabotageKind.Blight,
        };

        // ------------------------------------------------------------------ the night pass

        /// <summary>Tonight's strike, picked but not yet applied, waiting for its scene. Null when
        /// none is waiting.</summary>
        public PendingStrike Pending { get; private set; }

        /// <summary>Set by ModEntry once the overnight scenes exist (Task 6). Until then no scene
        /// can play, so every strike lands at once exactly as it did before the split.</summary>
        public Func<PendingStrike, bool> SceneCanPlay { get; set; } = _ => false;

        /// <summary>Land a waiting strike now. The net under every path that does not play the
        /// scene: no scene due, another overnight event won the slot, the scene threw, the save
        /// began.</summary>
        public bool ApplyPendingIfAny(string why)
        {
            PendingStrike p = Pending;
            if (p == null) return false;
            Pending = null;
            if (p.Applied) return false;
            _monitor.Log($"Darkness: applying tonight's {p.Event} without its scene ({why}) at tick {Game1.ticks}.", LogLevel.Trace);
            p.Apply();
            return true;
        }

        /// <summary>Was the guaranteed Winter tamper the strike picked tonight? Cleared at the top of
        /// every night pass, so it only ever describes tonight.</summary>
        private bool _guaranteedTamperTonight;

        /// <summary>Told once a strike has run, whatever came of it, from every apply path: the
        /// immediate one, the nets, and a scene calling Apply itself. A strike that found nothing to
        /// do when it came to it does not count toward the every-loop guarantee, so its name comes
        /// back off the run's list and the kind is still owed. The week's chance drop and the cap
        /// slot stay spent: the night is over either way.</summary>
        private void OnStrikeApplied(PendingStrike strike)
        {
            if (strike.Landed) return;
            (Run.StruckEvents ??= new()).Remove(strike.Event.ToString());
            _monitor.Log($"Darkness: tonight's {strike.Event} found nothing to do when it came to it, so the kind is still owed this loop.", LogLevel.Info);
            if (_guaranteedTamperTonight && strike.Event == DarknessEvent.Tampering)
            {
                Run.GuaranteedTamperDone = false;
                _monitor.Log("Darkness: the guaranteed Winter tamper did not land, so it retries tomorrow.", LogLevel.Info);
            }
        }

        /// <summary>Pick tonight's strike, record it, and either leave it waiting for its scene or
        /// land it now. The one path every strike goes through. Null when the event found nothing
        /// to take.</summary>
        private PendingStrike Strike(NightPlan night, DarknessEvent e, int week, CoreSeason season, int dayOfYear)
        {
            PendingStrike strike = night.Prepare(e, OnStrikeApplied);
            if (strike == null) return null;
            NightRoll.RecordStrike(Run, week, season);
            SabotageSchedule.RecordStrike(KindOf(e), Run, week, dayOfYear);
            (Run.StruckEvents ??= new()).Add(e.ToString());
            ApplyPendingIfAny("replaced by a new strike");
            Pending = strike;
            string reason = SceneReasonToSkip(e, strike);
            if (reason != null) ApplyPendingIfAny(reason);
            return strike;
        }

        /// <summary>Why tonight's scene will not play, or null when it will. A scene test that
        /// throws must not strand the night, so it counts as "cannot play".</summary>
        private string SceneReasonToSkip(DarknessEvent e, PendingStrike strike)
        {
            if (!StrikeScenes.IsDue(e, Run.StrikeScenesPlayed ??= new())) return "no scene due";
            try
            {
                return SceneCanPlay(strike) ? null : "its scene cannot play";
            }
            catch (Exception ex)
            {
                _monitor.Log($"Darkness: the scene test for {e} threw, so tonight's strike lands without it. {ex}", LogLevel.Error);
                return "its scene test threw";
            }
        }

        /// <summary>One roll for tonight. Effects land before the save; reports queue for the morning.</summary>
        public void RunNight()
        {
            // A strike left over from a night whose scene never played cannot be carried forward.
            ApplyPendingIfAny("a new night began");
            _guaranteedTamperTonight = false;
            if (!RunActivation.IsActive || !HostCanAct()) return;
            CoreSeason season = Run.Season;
            int day = Run.DayOfMonth;
            int dayOfYear = Calendar.DayOfYear((int)season, day);
            int week = Run.WeekOfYear;
            DifficultyStep level = Level;
            if (season == CoreSeason.Spring) { _armed.Clear(); _armedBlightTarget = null; return; }

            Random rng = SabotageSchedule.Rng(Run.Seed, dayOfYear);
            // The snapshot walks every map, so it is built on the first fairness question and never
            // on a quiet night. It reads no rng, so the night's outcome is unchanged either way.
            var save = new Lazy<SaveSnapshot>(() => SaveSnapshotReader.Read(msg => _monitor.Log(msg, LogLevel.Trace)));
            ObtainabilityModel model = _obtainability();
            var night = new NightPlan(this, season, day, week, dayOfYear, level, save, model, rng);

            // The guaranteed Winter tamper (spec 2.6) comes first and is the night's whole strike.
            // The flag says a Winter has already been reached, so "first Winter ever" is its negation.
            bool guaranteedTonight = Enabled(SabotageKind.Tampering)
                && NightRoll.IsGuaranteedTamperNight(Run, !Meta.FirstWinterTamperSeen, season, day);
            // Tonight's decision is made above with the old flag, so the flag can be set as soon as
            // week 1 is spent: a first Winter that found no fair target still counts as reached, and
            // every later Winter rolls the random week-1 date instead of Winter 1.
            if (season == CoreSeason.Winter && day >= NightRoll.Week1Nights)
                Meta.FirstWinterTamperSeen = true;
            if (guaranteedTonight)
            {
                _guaranteedTamperTonight = true;
                PendingStrike tamper = night.CanAct(DarknessEvent.Tampering, ignoreTamperReservation: true)
                    ? Strike(night, DarknessEvent.Tampering, week, season, dayOfYear)
                    : null;
                // Either it is still waiting for its scene, or it has already landed. A write that
                // failed the moment it ran leaves the flags alone and falls through to tomorrow.
                if (tamper != null && (!tamper.Applied || tamper.Landed))
                {
                    Run.GuaranteedTamperDone = true;
                    Meta.FirstWinterTamperSeen = true;
                    _monitor.Log($"Darkness: the guaranteed Winter tamper struck on Winter {day}.", LogLevel.Info);
                    _armed.Clear(); _armedBlightTarget = null;
                    return;
                }
                _guaranteedTamperTonight = false;
                _monitor.Log($"Darkness: guaranteed Winter tamper had no fair target on Winter {day}, retrying tomorrow.", LogLevel.Info);
            }

            double chance = NightRoll.ChanceTonight(Run, week, season);
            bool dice = rng.NextDouble() < chance;
            DarknessEvent? forced = TakeArmed(night);
            _monitor.Log($"Darkness: night roll {season} {day} at {chance:P0}: {(dice ? "strike" : "quiet")}{(forced != null ? $", armed {forced}" : "")}; level {level}.", LogLevel.Trace);

            // The every-loop guarantee (spec 2026-09-21): a kind that has not struck this loop by
            // day 15 of its debut season is forced on the first night it can act, even on a quiet
            // roll. An arm takes precedence: that is Jeff asking for a front by hand.
            if (forced == null)
            {
                // Asked only when nothing was armed: CanAct can plan a reversion, which spends rng,
                // and an armed night must roll exactly as it did before the guarantee existed.
                DarknessEvent? owed = StrikeGuarantee.ForcedTonight(season, day, Run.StruckEvents ??= new(), night.CanAct);
                if (owed != null)
                {
                    forced = owed;
                    _monitor.Log($"Darkness: {owed} has not struck this loop by {season} {day}, so it is forced tonight.", LogLevel.Info);
                }
            }

            if (!dice && forced == null) return;

            DarknessEvent? pick = forced ?? NightRoll.Pick(NightRoll.Options(season), night.CanAct, rng);
            if (pick == null)
            {
                _monitor.Log("Darkness: nothing could act tonight; no strike and the chance does not drop.", LogLevel.Trace);
                return;
            }
            if (Strike(night, pick.Value, week, season, dayOfYear) == null)
                _monitor.Log($"Darkness: {pick} was picked but took nothing.", LogLevel.Trace);
            else if (Pending != null)
                _monitor.Log($"Darkness: night pass done at tick {Game1.ticks}, leaving {Pending.Event} waiting for its scene.", LogLevel.Trace);
        }

        /// <summary>Everything one night needs, computed lazily so a plan is built once and reused
        /// by the "can it act" test and the strike.</summary>
        private sealed class NightPlan
        {
            private readonly SabotageService _s;
            private readonly CoreSeason _season;
            private readonly int _day, _week, _dayOfYear;
            private readonly DifficultyStep _level;
            private readonly Lazy<SaveSnapshot> _save;
            private readonly ObtainabilityModel _model;
            private readonly Random _rng;
            private int? _crops, _stored;
            private DonatedSlot _reversion; private bool _reversionPlanned, _reversionUnmoderated;
            private TamperPlan _tamper; private bool _tamperPlanned, _tamperUnmoderated;

            public NightPlan(SabotageService s, CoreSeason season, int day, int week, int dayOfYear, DifficultyStep level, Lazy<SaveSnapshot> save, ObtainabilityModel model, Random rng)
            { _s = s; _season = season; _day = day; _week = week; _dayOfYear = dayOfYear; _level = level; _save = save; _model = model; _rng = rng; }

            private RunState Run => _s.Run;

            /// <summary>Can this event act tonight? The ordinary nightly roll asks this, so it keeps
            /// the guaranteed Winter tamper's slot reserved through week 1.</summary>
            public bool CanAct(DarknessEvent e) => CanAct(e, ignoreTamperReservation: false);

            /// <summary>As <see cref="CanAct(DarknessEvent)"/>, but the guaranteed attempt itself and
            /// a debug arm ask without the reservation, since they ARE the reserved strike or an
            /// explicit request for one.</summary>
            public bool CanAct(DarknessEvent e, bool ignoreTamperReservation)
            {
                switch (e)
                {
                    case DarknessEvent.CropBlight:
                        if (!_s.Enabled(SabotageKind.Blight) || _s.CropsWarded(_season)) return false;
                        if (!SabotageSchedule.WithinCaps(SabotageKind.Blight, Run, _week, _dayOfYear)) return false;
                        return (_crops ??= BlightPass.LiveCropTiles().Count) > 0;
                    case DarknessEvent.ChestBlight:
                        if (!_s.Enabled(SabotageKind.Blight)) return false;
                        if (!SabotageSchedule.WithinCaps(SabotageKind.Blight, Run, _week, _dayOfYear)) return false;
                        return (_stored ??= SpoilagePass.StoredUnits(DarknessLevels.StorageReachesEverything(_level))) > 0;
                    case DarknessEvent.Reversion:
                        if (!_s.Enabled(SabotageKind.Reversion) || !SabotageSchedule.IsOpen(SabotageKind.Reversion, _season)) return false;
                        if (SabotageSchedule.IsQuietDay(SabotageKind.Reversion, _day) || !SabotageSchedule.WithinCaps(SabotageKind.Reversion, Run, _week, _dayOfYear)) return false;
                        return PlanReversion() != null;
                    case DarknessEvent.Tampering:
                        if (!_s.Enabled(SabotageKind.Tampering) || !SabotageSchedule.IsOpen(SabotageKind.Tampering, _season)) return false;
                        if (!ignoreTamperReservation && GuaranteedTamperReserved())
                        {
                            // Without this, an ordinary Winter roll can spend the week on Tampering,
                            // and the guaranteed strike then fails the five-day spacing or the
                            // two-per-Winter cap and week 1 runs out (spec 2.6).
                            _s._monitor.Log($"Darkness: tampering is held for the guaranteed Winter tamper through week 1 ({_season} {_day}); the ordinary roll may not take it.", LogLevel.Trace);
                            return false;
                        }
                        if (SabotageSchedule.IsQuietDay(SabotageKind.Tampering, _day) || !SabotageSchedule.WithinCaps(SabotageKind.Tampering, Run, _week, _dayOfYear)) return false;
                        return PlanTamper() != null;
                    default:
                        return false;
                }
            }

            /// <summary>Pick what this event does tonight without doing it (spec 2026-09-21). Null
            /// means it found nothing to take, the old Execute's false. The returned strike carries
            /// the effect as a closure, so the damage lands when its scene reaches the beat, or at
            /// once when no scene plays.</summary>
            public PendingStrike Prepare(DarknessEvent e, Action<PendingStrike> onApplied)
            {
                switch (e)
                {
                    case DarknessEvent.CropBlight:
                    {
                        List<Vector2> tiles = BlightPass.Pick(BlightRule.Count(_crops ?? BlightPass.LiveCropTiles().Count, _season, _level), _rng);
                        if (tiles.Count == 0) return null;
                        return new PendingStrike(e, () => _s.ReportBlight(BlightPass.Kill(tiles), default) > 0, onApplied) { CropTiles = tiles };
                    }
                    case DarknessEvent.ChestBlight:
                    {
                        bool everything = DarknessLevels.StorageReachesEverything(_level);
                        int units = _stored ?? SpoilagePass.StoredUnits(everything);
                        List<SpoilagePass.Hit> hits = SpoilagePass.Plan(BlightRule.SpoilCount(units, _season, _level), _rng, everything);
                        if (hits.Count == 0) return null;
                        return new PendingStrike(e, () => _s.ReportBlight(0, SpoilagePass.Apply(hits)) > 0, onApplied) { Hits = hits };
                    }
                    case DarknessEvent.Reversion:
                    {
                        DonatedSlot pick = PlanReversion();
                        if (pick == null) return null;
                        bool unmoderated = _reversionUnmoderated;
                        return new PendingStrike(e, () =>
                        {
                            if (!_s.RevertSlot(pick)) return false;
                            if (unmoderated) Run.UnmoderatedReversionSpent = true;
                            return true;
                        }, onApplied);
                    }
                    case DarknessEvent.Tampering:
                    {
                        var worldState = Game1.netWorldState?.Value;
                        if (worldState?.BundleData == null) return null;
                        TamperPlan plan = PlanTamper();
                        if (plan == null) return null;
                        bool unmoderated = _tamperUnmoderated;
                        int dayOfYear = _dayOfYear;
                        return new PendingStrike(e, () =>
                        {
                            if (!_s.WriteTamper(worldState, plan.Target, plan.ItemId, plan.Stack, dayOfYear)) return false;
                            if (unmoderated) Run.UnmoderatedTamperSpent = true;
                            return true;
                        }, onApplied);
                    }
                    default:
                        return null;
                }
            }

            /// <summary>Is tonight's Tampering slot still owed to the guaranteed Winter tamper? True
            /// through week 1 of a Winter that has not had it yet.</summary>
            private bool GuaranteedTamperReserved()
                => _season == CoreSeason.Winter && !Run.GuaranteedTamperDone && _day <= NightRoll.Week1Nights;

            private DonatedSlot PlanReversion()
            {
                if (_reversionPlanned) return _reversion;
                _reversionPlanned = true;
                _reversionUnmoderated = NightRoll.UnmoderatedFires(_level, Run.UnmoderatedReversionSpent, _rng);
                int deadline = FairnessRule.ReversionDeadline(_dayOfYear, _level);
                _reversion = _s.PickReversion(_rng, _reversionUnmoderated ? null : (Func<string, bool>)(id => FairnessRule.Counts(id, _dayOfYear, deadline, _level, _save.Value, _model)));
                if (_reversionUnmoderated) _s._monitor.Log("Darkness: this reversion is the loop's unmoderated one.", LogLevel.Info);
                return _reversion;
            }

            private TamperPlan PlanTamper()
            {
                if (_tamperPlanned) return _tamper;
                _tamperPlanned = true;
                _tamperUnmoderated = NightRoll.UnmoderatedFires(_level, Run.UnmoderatedTamperSpent, _rng);
                _tamper = _s.PlanTamper(_rng, _tamperUnmoderated ? null : (Func<string, bool>)(id => FairnessRule.Counts(id, _dayOfYear, FairnessRule.TamperDeadline, _level, _save.Value, _model)));
                if (_tamperUnmoderated) _s._monitor.Log("Darkness: this tamper is the loop's unmoderated one.", LogLevel.Info);
                return _tamper;
            }
        }

        private sealed class TamperPlan
        {
            public TamperTarget Target;
            public string ItemId;
            public int Stack;
        }

        // ------------------------------------------------------------------ arming (debug)

        /// <summary>Fronts a playtest has armed to strike on the next real night.</summary>
        private readonly HashSet<SabotageKind> _armed = new HashSet<SabotageKind>();

        /// <summary>What tonight's blight must go after, when a playtest named it; null leaves the
        /// choice to NightRoll's even split over the season's events. Cleared after tonight's
        /// roll.</summary>
        private BlightTarget? _armedBlightTarget;

        /// <summary>Debug: make <paramref name="kind"/> strike on tonight's real roll, so a playtest
        /// sleeps into it exactly as a player would (Jeff, 2026-09-14). Clears any waiting report.
        /// In memory only: a relaunch disarms.</summary>
        public string Arm(SabotageKind kind, BlightTarget? blightTarget = null)
        {
            int cleared = Run.PendingSabotageReports?.Count ?? 0;
            Run.PendingSabotageReports?.Clear();
            _armed.Add(kind);
            if (kind == SabotageKind.Blight) _armedBlightTarget = blightTarget;
            string target = kind == SabotageKind.Blight ? $", target {(blightTarget?.ToString() ?? "either (coin flip)")}" : "";
            string closed = SabotageSchedule.IsOpen(kind, Run.Season) ? "" : $" WARNING: {kind} is not open in {Run.Season}, so tonight will not strike.";
            string off = Enabled(kind) ? "" : $" WARNING: {kind} is switched off in the config.";
            return $"Darkness: {kind} armed for tonight's roll ({Run.Season} {Run.DayOfMonth}{target}, level {Level}); cleared {cleared} waiting report(s).{closed}{off}";
        }

        /// <summary>The armed event for tonight, if any and if it can act; consumes every arm.</summary>
        private DarknessEvent? TakeArmed(NightPlan night)
        {
            if (_armed.Count == 0) return null;
            DarknessEvent? result = null;
            foreach (SabotageKind kind in _armed.ToList())
            {
                DarknessEvent[] candidates = kind switch
                {
                    SabotageKind.Reversion => new[] { DarknessEvent.Reversion },
                    SabotageKind.Tampering => new[] { DarknessEvent.Tampering },
                    _ => _armedBlightTarget switch
                    {
                        BlightTarget.Crops => new[] { DarknessEvent.CropBlight },
                        BlightTarget.Chests => new[] { DarknessEvent.ChestBlight },
                        _ => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight },
                    },
                };
                // An arm is Jeff asking for this front tonight, so it ignores the guaranteed tamper's
                // week-1 reservation: the point of arming is to sleep into the strike now.
                DarknessEvent? able = candidates.Cast<DarknessEvent?>().FirstOrDefault(e => night.CanAct(e.Value, ignoreTamperReservation: true));
                _monitor.Log(able != null
                    ? $"Darkness: {kind} was armed; striking tonight as {able}."
                    : $"Darkness: {kind} was armed but cannot act tonight (closed, quiet, capped, warded or nothing fair).", LogLevel.Info);
                result ??= able;
            }
            _armed.Clear();
            _armedBlightTarget = null;
            return result;
        }

        private bool CropsWarded(CoreSeason season)
        {
            string ward = WardIds.CropWardFor(season);
            return ward != null && Meta.HasUpgrade(ward);
        }

        // ------------------------------------------------------------------ blight

        /// <summary>Kill <paramref name="crops"/> crops and take <paramref name="spoil"/> stored units
        /// now, then queue the report. Returns how many things were taken in all. The debug
        /// entry point (<c>tly_sabotage blight [crops] [spoil]</c>), which strikes at once and never
        /// goes through <see cref="Pending"/>.</summary>
        public int Blight(int crops, int spoil, Random rng)
            => ReportBlight(BlightPass.Strike(crops, rng), SpoilagePass.Strike(spoil, rng, DarknessLevels.StorageReachesEverything(Level)));

        /// <summary>Queue the morning report for what a blight took, and log it. Returns how many
        /// things were taken in all, 0 when nothing was.</summary>
        private int ReportBlight(int killed, SpoilagePass.Taken taken)
        {
            if (killed <= 0 && taken.Total <= 0)
            {
                _monitor.Log("Darkness: blight rolled but found nothing to strike or take.", LogLevel.Trace);
                return 0;
            }
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Blight, Count = killed, Spoiled = taken.Spoiled, Missing = taken.Missing,
            });
            _monitor.Log($"Darkness: {killed} crop(s) struck down, {taken.Spoiled} stored unit(s) spoiled, {taken.Missing} gone missing on {Run.Season} {Run.DayOfMonth}.", LogLevel.Info);
            return killed + taken.Total;
        }

        // ------------------------------------------------------------------ reversion

        /// <summary>Debug entry point (<c>tly_sabotage revert</c>): a fair pick at the current level, opened now.</summary>
        public bool Revert(Random rng)
        {
            int dayOfYear = Calendar.DayOfYear((int)Run.Season, Run.DayOfMonth);
            int deadline = FairnessRule.ReversionDeadline(dayOfYear, Level);
            SaveSnapshot save = SaveSnapshotReader.Read(msg => _monitor.Log(msg, LogLevel.Trace));
            ObtainabilityModel model = _obtainability();
            DonatedSlot pick = PickReversion(rng, id => FairnessRule.Counts(id, dayOfYear, deadline, Level, save, model));
            return pick != null && RevertSlot(pick);
        }

        private DonatedSlot PickReversion(Random rng, Func<string, bool> fair)
        {
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            DonatedSlot pick = fair == null
                ? ReversionRule.Pick(ledger, _requirements(), rng)
                : ReversionRule.Pick(ledger, _requirements(), fair, rng);
            if (pick == null)
                _monitor.Log("Darkness: reversion found no slot it may fairly empty.", LogLevel.Trace);
            return pick;
        }

        private bool RevertSlot(DonatedSlot pick)
        {
            if (!TheLongestYear.Integration.CcSlotWriter.TryUnfill(pick.BundleIndex, pick.IngredientIndex))
            {
                _monitor.Log($"Darkness: reversion could not open slot {pick.BundleIndex}/{pick.IngredientIndex} on the board.", LogLevel.Warn);
                return false;
            }
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            string bundleName = _requirements().FirstOrDefault(r => r.BundleIndex == pick.BundleIndex)?.Name ?? "";
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Reversion, Count = 1, BundleName = bundleName, ItemId = pick.ItemId,
            });
            _monitor.Log($"Darkness: {Strings.ItemName(pick.ItemId)} came undone from {bundleName} (slot {pick.BundleIndex}/{pick.IngredientIndex}) on {Run.Season} {Run.DayOfMonth}.", LogLevel.Info);
            return true;
        }

        // ------------------------------------------------------------------ tampering

        /// <summary>Debug entry point (<c>tly_sabotage tamper</c>): a fair pick at the current level, written now.</summary>
        public bool Tamper(Random rng, int dayOfYear)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return false;
            SaveSnapshot save = SaveSnapshotReader.Read(msg => _monitor.Log(msg, LogLevel.Trace));
            ObtainabilityModel model = _obtainability();
            TamperPlan plan = PlanTamper(rng, id => FairnessRule.Counts(id, dayOfYear, FairnessRule.TamperDeadline, Level, save, model));
            return plan != null && WriteTamper(worldState, plan.Target, plan.ItemId, plan.Stack, dayOfYear);
        }

        /// <summary>The target and replacement a tamper would write, or null when no unfilled slot has
        /// a fair replacement. <paramref name="fair"/> null means the unmoderated roll: every item
        /// the board's pools hold, no check.</summary>
        private TamperPlan PlanTamper(Random rng, Func<string, bool> fair)
        {
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            IReadOnlyList<BundleRequirement> requirements = _requirements();
            List<TamperTarget> targets = TamperRule.Targets(ledger, requirements).ToList();
            if (targets.Count == 0)
            {
                _monitor.Log("Darkness: tampering has no unfilled slot in an unfinished bundle.", LogLevel.Trace);
                return null;
            }
            IReadOnlyList<TamperCandidate> candidates = Candidates(fair);
            HashSet<string> held = HeldItemIds();
            var ordered = new List<TamperTarget>();
            while (targets.Count > 0)
            {
                TamperTarget next = TamperRule.PickTarget(targets, held.Contains, rng);
                if (next == null) break;
                ordered.Add(next);
                targets.Remove(next);
            }
            ItemAvailabilityModel availability = _availability();
            int weekOfWinter = Run.WeekInMonth;
            foreach (TamperTarget target in ordered)
            {
                int effort = availability.For(target.ItemId).Effort;
                TamperCandidate replacement = TamperRule.PickReplacement(target, effort, candidates, rng);
                if (replacement == null) continue;
                int maxCount = TamperRule.MaxCount(replacement.ItemId, QuantityAskPass.BasisByDeadline(replacement.ItemId, CoreSeason.Winter));
                int stack = TamperRule.Stack(maxCount, weekOfWinter, Level, rng);
                return new TamperPlan { Target = target, ItemId = replacement.ItemId, Stack = stack };
            }
            _monitor.Log("Darkness: tampering found no fair replacement for any open slot.", LogLevel.Info);
            return null;
        }

        /// <summary>The replacement pool: the board's own universe, which is the live generation
        /// pools, each under the room theme it feeds, with the existing model's effort for closeness
        /// and filtered by <paramref name="fair"/> when a rule applies. Not the curated
        /// CcItemCatalog: that table is deliberately a short list, so tampering drew from a fraction
        /// of what the board itself can ask for.</summary>
        private IReadOnlyList<TamperCandidate> Candidates(Func<string, bool> fair)
        {
            ItemPools pools = _pools();
            if (pools == null)
            {
                _monitor.Log("Darkness: the generation pools are not built, so tampering has no replacement pool tonight.", LogLevel.Warn);
                return Array.Empty<TamperCandidate>();
            }
            ItemAvailabilityModel availability = _availability();
            var result = new List<TamperCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            void Add(IReadOnlyList<PoolItem> pool, Theme theme)
            {
                foreach (PoolItem item in pool)
                {
                    string id = BundleParsing.NormalizeItemId(item.ItemId);
                    if (!seen.Add(id)) continue;   // an id in two pools keeps its first theme
                    if (fair != null && !fair(id)) continue;
                    // Mid scale for an id no rule placed, not 0: a 0 would make every unplaced item the
                    // closest match to a cheap slot and PickReplacement's "five closest" alphabetical.
                    // For() is only called when the id IS placed; it records a lookup miss otherwise.
                    int effort = availability.IsPlaced(id) ? availability.For(id).Effort : ItemAvailabilityModel.UnrecognisedEffort;
                    result.Add(new TamperCandidate(id, theme, effort));
                }
            }
            Add(pools.Crops, Theme.Farming);
            Add(pools.ArtisanGoods, Theme.Farming);
            Add(pools.Forage, Theme.Foraging);
            Add(pools.TapperGoods, Theme.Foraging);
            Add(pools.Fish, Theme.Fishing);
            Add(pools.CrabPot, Theme.Fishing);
            Add(pools.Metals, Theme.Mining);
            Add(pools.MonsterDrops, Theme.Mining);
            Add(pools.GeodeMinerals, Theme.Mining);
            Add(pools.Cooking, Theme.Mixed);
            return result;
        }

        /// <summary>Ids the player is holding right now: inventory plus every chest on every map.</summary>
        private static HashSet<string> HeldItemIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Farmer player = Game1.player;
            if (player != null)
                foreach (Item item in player.Items)
                    if (item != null) ids.Add(item.QualifiedItemId);
            Utility.ForEachLocation(loc =>
            {
                foreach (StardewValley.Object obj in loc.objects.Values)
                    if (obj is Chest chest)
                        foreach (Item item in chest.Items)
                            if (item != null) ids.Add(item.QualifiedItemId);
                return true;
            });
            return ids;
        }

        private bool WriteTamper(StardewValley.Network.NetWorldState worldState, TamperTarget target, string newItemId, int stack, int dayOfYear)
        {
            Dictionary<string, string> live = worldState.BundleData;
            string key = BundleDataTamper.KeyForIndex(live, target.Bundle.BundleIndex);
            if (key == null) return false;
            Dictionary<string, string> tampered = BundleDataTamper.Apply(
                live, key, target.IngredientIndex, newItemId, stack, SabotageTuning.TamperQuality);
            if (tampered == null) return false;

            // The board, then the stored copy in lockstep: the next load's manifest check compares
            // the two, and a mismatch demotes the save to the legacy read path (ModEntry.ResolveRequirements).
            worldState.SetBundleData(tampered);
            if (Meta.WrittenBoard != null && Meta.WrittenBoard.Count > 0 && Meta.WrittenBoard.ContainsKey(key))
                Meta.WrittenBoard[key] = tampered[key];

            // A goal card pointing at the old item would show a slot the board no longer has.
            Run.CurrentWeekBonusSlots?.RemoveAll(s =>
                s.BundleIndex == target.Bundle.BundleIndex && s.IngredientIndex == target.IngredientIndex);

            Run.Tampers.Add(new TamperRecord
            {
                BundleIndex = target.Bundle.BundleIndex,
                IngredientIndex = target.IngredientIndex,
                BundleName = target.Bundle.Name,
                OldItemId = target.ItemId,
                NewItemId = newItemId,
                Stack = stack,
                DayOfYear = dayOfYear,
            });
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Tampering, Count = stack, BundleName = target.Bundle.Name,
                ItemId = newItemId, OldItemId = target.ItemId,
            });
            _monitor.Log(
                $"Darkness: {target.Bundle.Name} slot {target.IngredientIndex} now asks for {stack} {Strings.ItemName(newItemId)} instead of {Strings.ItemName(target.ItemId)} ({Run.Season} {Run.DayOfMonth}).",
                LogLevel.Info);
            _rebuildBoard("darkness tampering");
            return true;
        }

        // ------------------------------------------------------------------ the morning

        /// <summary>The morning: HUD lines and letters for what the night took, and when the board
        /// changed, the Junimos' porch scene first. Returns true when the scene was started and
        /// <paramref name="continueWith"/> will run after it; false when the caller continues now.</summary>
        public bool ShowMorning(Action continueWith)
        {
            // The morning cannot report what has not happened: a strike whose scene never played
            // lands here at the latest.
            ApplyPendingIfAny("morning");
            if (!RunActivation.IsActive) return false;
            SabotageReport tamper = Run.PendingSabotageReports?.Find(r => r.Kind == SabotageKind.Tampering);
            if (tamper == null || StartTamperScene == null)
            {
                ShowMorningReports();
                return false;
            }
            string oldName = Strings.ItemName(tamper.OldItemId);
            string ask = tamper.Count > 1 ? $"{tamper.Count} {Strings.ItemName(tamper.ItemId)}" : Strings.ItemName(tamper.ItemId);
            Run.PendingSabotageReports.RemoveAll(r => r.Kind == SabotageKind.Tampering);
            StartTamperScene(oldName, ask, () => { ShowMorningReports(); continueWith?.Invoke(); });
            return true;
        }

        /// <summary>Show what the night took, as HUD lines, then forget them.</summary>
        public void ShowMorningReports()
        {
            if (!RunActivation.IsActive) return;
            List<SabotageReport> reports = Run.PendingSabotageReports;
            if (reports == null || reports.Count == 0) return;
            // The hall fronts share one line and say it once, however many struck (Jeff, 2026-09-09:
            // the player wakes with a feeling, the board tells the rest).
            bool hallSaid = false;
            foreach (SabotageReport report in reports)
            {
                switch (report.Kind)
                {
                    case SabotageKind.Blight:
                        // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
                        if (report.Count == 1)
                            Hud(Strings.Get("hud.sabotage.blight.one"));
                        else if (report.Count > 1)
                            Hud(Strings.Get("hud.sabotage.blight.other", new Dictionary<string, string> { ["count"] = report.Count.ToString() }));
                        if (report.Spoiled > 0)
                            Hud(Strings.Get("hud.sabotage.spoiled", new Dictionary<string, string> { ["count"] = report.Spoiled.ToString() }));
                        if (report.Missing > 0)
                            Hud(Strings.Get("hud.sabotage.missing", new Dictionary<string, string> { ["count"] = report.Missing.ToString() }));
                        break;
                    case SabotageKind.Reversion:
                    case SabotageKind.Tampering:
                        if (!hallSaid) Game1.addHUDMessage(new HUDMessage(Strings.Get("hud.sabotage.hall"), HUDMessage.error_type));
                        hallSaid = true;
                        break;
                }
            }
            reports.Clear();
            Game1.playSound("shadowDie");
        }

        private static void Hud(string text) => Game1.addHUDMessage(new HUDMessage(text, HUDMessage.error_type));

        /// <summary>tly_sabotage fair: the fairness readout for one item at the current (or a named) level.</summary>
        public string Explain(string itemId, DifficultyStep? level)
        {
            DifficultyStep at = level ?? Level;
            int dayOfYear = Calendar.DayOfYear((int)Run.Season, Run.DayOfMonth);
            SaveSnapshot save = SaveSnapshotReader.Read(msg => _monitor.Log(msg, LogLevel.Trace));
            ObtainabilityModel model = _obtainability();
            int reversion = FairnessRule.ReversionDeadline(dayOfYear, at);
            FairnessVerdict asReversion = FairnessRule.Judge(itemId, dayOfYear, reversion, at, save, model);
            FairnessVerdict asTamper = FairnessRule.Judge(itemId, dayOfYear, FairnessRule.TamperDeadline, at, save, model);
            return $"{Strings.ItemName(itemId)} at {at}, hit day {dayOfYear}:\n"
                 + $"reversion (deadline day {reversion}): {FairnessRule.Explain(asReversion)}\n"
                 + $"tampering (deadline day {FairnessRule.TamperDeadline}): {asTamper.Summary}";
        }

        /// <summary>One-screen status for tly_sabotage.</summary>
        public string Status()
        {
            int week = Run.WeekOfYear;
            var lines = new List<string>
            {
                $"Darkness: level {Level}; blight={(Enabled(SabotageKind.Blight) ? "on" : "off")}, reversion={(Enabled(SabotageKind.Reversion) ? "on" : "off")}, tampering={(Enabled(SabotageKind.Tampering) ? "on" : "off")}; model {(_obtainability() == null ? "MISSING (no fairness filter)" : "published")}",
                $"  season {Run.Season} day {Run.DayOfMonth}: options = {string.Join(", ", NightRoll.Options(Run.Season))}; chance tonight {NightRoll.ChanceTonight(Run, week, Run.Season):P0}",
                $"  unmoderated this loop: reversion {(Run.UnmoderatedReversionSpent ? "spent" : "available")}, tamper {(Run.UnmoderatedTamperSpent ? "spent" : "available")}; guaranteed Winter tamper {(Run.GuaranteedTamperDone ? "done" : "pending")} (first Winter ever: {(!Meta.FirstWinterTamperSeen)})",
                $"  wards owned: {string.Join(", ", WardIds.All.Where(Meta.HasUpgrade).DefaultIfEmpty("none"))}",
                $"  blight week {Run.BlightWeek} nights {Run.BlightNightsThisWeek}; last reversion week {Run.LastReversionWeek}; tamper days [{string.Join(",", Run.TamperDays)}]",
                $"  live crops on the farm: {BlightPass.LiveCropTiles().Count}; units in chests (stash excluded): {SpoilagePass.StoredUnits(DarknessLevels.StorageReachesEverything(Level))}",
                $"  Scenes played this loop: {string.Join(", ", (Run.StrikeScenesPlayed ??= new()).DefaultIfEmpty("none"))}",
                $"  Scenes seen on save: {string.Join(", ", (Meta.StrikeScenesSeen ??= new()).DefaultIfEmpty("none"))}",
            };
            foreach (TamperRecord t in Run.Tampers)
                lines.Add($"  tampered: {t.BundleName} slot {t.IngredientIndex}: {Strings.ItemName(t.OldItemId)} -> {t.Stack} {Strings.ItemName(t.NewItemId)} (day {t.DayOfYear})");
            if (Run.PendingSabotageReports.Count > 0)
                lines.Add($"  pending morning reports: {Run.PendingSabotageReports.Count}");
            return string.Join("\n", lines);
        }
    }
}
