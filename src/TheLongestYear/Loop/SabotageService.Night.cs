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
    /// <summary>The night pass: the roll and the pick. The pending strike is in SabotageService.Pending.cs.</summary>
    internal sealed partial class SabotageService
    {
        // ------------------------------------------------------------------ the night pass

        /// <summary>Pick tonight's strike and either leave it waiting for its scene or, with no
        /// scene by design (its kind's scene already played this loop), land it now. The one path
        /// every strike goes through. Nothing is recorded here: the run records a strike when it
        /// commits (its scene stages, or it lands with no scene by design), so a postponed strike
        /// leaves no trace. A strike whose scene is due but cannot show tonight's pick is postponed
        /// at once, effect and scene both (designer, 2026-10-07: never its effect without its scene
        /// while that scene is due). Null when the event found nothing to take.</summary>
        private PendingStrike Strike(NightPlan night, DarknessEvent e, int week, CoreSeason season, int dayOfYear)
        {
            PendingStrike strike = night.Prepare(e, OnStrikeApplied, p => OnStrikeCommitted(p, week, season, dayOfYear));
            if (strike == null) return null;
            SettlePendingIfAny("replaced by a new strike");
            Pending = strike;
            if (!StrikeScenes.IsDue(e, Run.StrikeScenesPlayed ??= new()))
                ApplyPendingIfAny("no scene due");
            else if (SceneCannotShow(e, strike) is string why)
                PostponePendingIfAny(why);
            return strike;
        }

        /// <summary>Why a due scene cannot show tonight's pick, or null when it can. A scene test that
        /// throws must not strand the night, so it counts as "cannot show", and the strike waits.</summary>
        private string SceneCannotShow(DarknessEvent e, PendingStrike strike)
        {
            try
            {
                return SceneCanPlay(strike) ? null : "its scene cannot show tonight's pick";
            }
            catch (Exception ex)
            {
                _monitor.Log($"Darkness: the scene test for {e} threw, so tonight's strike waits with it. {ex}", LogLevel.Error);
                return "its scene test threw";
            }
        }

        /// <summary>One roll for tonight. Effects land before the save; reports queue for the morning.</summary>
        public void RunNight()
        {
            // A strike left over from an earlier night cannot be carried forward: it lands if its
            // scene had the slot, and is postponed otherwise.
            SettlePendingIfAny("a new night began");
            _guaranteedTamperTonight = false;
            _queuedTonight = false;
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
                    // GuaranteedTamperDone is set when it commits (OnStrikeCommitted), so a tamper
                    // postponed by a collision retries tomorrow; FirstWinterTamperSeen is set by
                    // OnStrikeApplied once it lands.
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

            // A strike postponed on an earlier night fires on the next free night instead of the
            // normal roll (designer, 2026-10-07), as tonight's one strike. A queued kind that cannot
            // act tonight (capped, spaced, warded, nothing fair) stays queued and the night rolls
            // normally. An arm takes precedence: that is Jeff asking for a front by hand.
            if (forced == null)
            {
                DarknessEvent? queued = StrikeQueue.Tonight(Run, night.CanAct);
                if (queued != null)
                {
                    forced = queued;
                    _queuedTonight = true;
                    _monitor.Log($"Darkness: the postponed {queued} fires tonight ({season} {day}) instead of the roll.", LogLevel.Info);
                }
                else if (Run.QueuedStrikes is { Count: > 0 })
                    _monitor.Log($"Darkness: queued {string.Join(", ", Run.QueuedStrikes)} cannot act tonight, so it stays queued and the night rolls normally.", LogLevel.Trace);
            }

            // The every-loop guarantee (spec 2026-09-21): a kind that has not struck this loop by
            // day 15 of its debut season is forced on the first night it can act, even on a quiet
            // roll. An arm or a queued strike takes precedence.
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

            /// <summary>Has the thief scene still to play this loop? While it has, the chest draw
            /// holds only chests the scene can show (designer, 2026-10-07).</summary>
            private bool ThiefSceneDue => StrikeScenes.IsDue(DarknessEvent.ChestBlight, Run.StrikeScenesPlayed ??= new());

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
                        return (_stored ??= SpoilagePass.StoredUnits(DarknessLevels.StorageReachesEverything(_level), ThiefSceneDue)) > 0;
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
            /// once when no scene plays by design.</summary>
            public PendingStrike Prepare(DarknessEvent e, Action<PendingStrike> onApplied, Action<PendingStrike> onCommitted)
            {
                switch (e)
                {
                    case DarknessEvent.CropBlight:
                    {
                        List<Vector2> tiles = BlightPass.Pick(BlightRule.Count(_crops ?? BlightPass.LiveCropTiles().Count, _season, _level), _rng);
                        if (tiles.Count == 0) return null;
                        return new PendingStrike(e, () => _s.ReportBlight(BlightPass.Kill(tiles), default) > 0, onApplied, onCommitted) { CropTiles = tiles };
                    }
                    case DarknessEvent.ChestBlight:
                    {
                        bool everything = DarknessLevels.StorageReachesEverything(_level);
                        int units = _stored ?? SpoilagePass.StoredUnits(everything, ThiefSceneDue);
                        List<SpoilagePass.Hit> hits = SpoilagePass.Plan(BlightRule.SpoilCount(units, _season, _level), _rng, everything, ThiefSceneDue);
                        if (hits.Count == 0) return null;
                        return new PendingStrike(e, () => _s.ReportBlight(0, SpoilagePass.Apply(hits)) > 0, onApplied, onCommitted) { Hits = hits };
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
                        }, onApplied, onCommitted);
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
                        }, onApplied, onCommitted);
                    }
                    default:
                        return null;
                }
            }

            /// <summary>Is tonight's Tampering slot still owed to the guaranteed Winter tamper? True
            /// through week 1 of a Winter that has not had it yet.</summary>
            private bool GuaranteedTamperReserved()
                => _season == CoreSeason.Winter && !Run.GuaranteedTamperDone
                   && (_day <= NightRoll.Week1Nights || Run.GuaranteedTamperPostponed);

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
