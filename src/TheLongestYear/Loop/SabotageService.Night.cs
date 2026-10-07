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
    /// <summary>The night pass: the roll, the pick, the pending strike and when it lands.</summary>
    internal sealed partial class SabotageService
    {
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
        /// slot stay spent: the night is over either way.
        ///
        /// The guaranteed Winter tamper marks the save's first Winter as reached only here, once it
        /// has landed. Marking it at the pick would send a failed apply's retry to the seeded
        /// random week-1 night instead of tomorrow.</summary>
        private void OnStrikeApplied(PendingStrike strike)
        {
            if (strike.Landed)
            {
                if (_guaranteedTamperTonight && strike.Event == DarknessEvent.Tampering)
                    Meta.FirstWinterTamperSeen = true;
                return;
            }
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
                    // FirstWinterTamperSeen is set by OnStrikeApplied once the tamper lands.
                    Run.GuaranteedTamperDone = true;
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


        private bool CropsWarded(CoreSeason season)
        {
            string ward = WardIds.CropWardFor(season);
            return ward != null && Meta.HasUpgrade(ward);
        }
    }
}
