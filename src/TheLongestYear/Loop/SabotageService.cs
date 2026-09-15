using System;
using System.Collections.Generic;
using System.Linq;
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
    /// obtainability model against the real save by Darkness level. Blight kills crops (BlightPass)
    /// or takes stored units (SpoilagePass); reversion opens a filled slot; tampering rewrites an
    /// unfilled slot's item and asks ModEntry to rebuild the catalog and requirements. Host only,
    /// single player or master, never on a day 28 or the win night (RunController decides that).</summary>
    internal sealed class SabotageService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly Func<IReadOnlyList<BundleRequirement>> _requirements;
        private readonly Func<ItemAvailabilityModel> _availability;
        private readonly Func<ObtainabilityModel> _obtainability;
        private readonly Action<string> _rebuildBoard;
        private readonly SabotageMailService _mail;
        /// <summary>Starts the board-changed porch scene; set by ModEntry (the season-turn driver).</summary>
        public Action<string, string, Action> StartTamperScene { get; set; }

        private RunState Run => _store.Run;
        private MetaState Meta => _store.State;
        private DifficultyStep Level => Meta.EffectiveDifficulty(_config).Darkness;

        public SabotageService(
            IMonitor monitor, MetaStore store, GameplayConfig config,
            Func<IReadOnlyList<BundleRequirement>> requirements,
            Func<ItemAvailabilityModel> availability,
            Func<ObtainabilityModel> obtainability,
            Action<string> rebuildBoard,
            SabotageMailService mail)
        {
            _mail = mail;
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
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

        /// <summary>One roll for tonight. Effects land before the save; reports queue for the morning.</summary>
        public void RunNight()
        {
            if (!RunActivation.IsActive || !HostCanAct()) return;
            CoreSeason season = Run.Season;
            int day = Run.DayOfMonth;
            int dayOfYear = Calendar.DayOfYear((int)season, day);
            int week = Run.WeekOfYear;
            DifficultyStep level = Level;
            if (season == CoreSeason.Spring) { _armed.Clear(); _armedBlightTarget = null; return; }

            Random rng = SabotageSchedule.Rng(Run.Seed, dayOfYear);
            SaveSnapshot save = SaveSnapshotReader.Read();
            ObtainabilityModel model = _obtainability();
            var night = new NightPlan(this, season, day, week, dayOfYear, level, save, model, rng);

            // The guaranteed Winter tamper (spec 2.6) comes first and is the night's whole strike.
            if (Enabled(SabotageKind.Tampering) && NightRoll.IsGuaranteedTamperNight(Run, Meta.FirstWinterTamperSeen, season, day))
            {
                if (night.CanAct(DarknessEvent.Tampering) && night.Execute(DarknessEvent.Tampering))
                {
                    Run.GuaranteedTamperDone = true;
                    Meta.FirstWinterTamperSeen = true;
                    NightRoll.RecordStrike(Run, week, season);
                    SabotageSchedule.RecordStrike(SabotageKind.Tampering, Run, week, dayOfYear);
                    _monitor.Log($"Darkness: the guaranteed Winter tamper struck on Winter {day}.", LogLevel.Info);
                    _armed.Clear(); _armedBlightTarget = null;
                    return;
                }
                _monitor.Log($"Darkness: guaranteed Winter tamper had no fair target on Winter {day}; retrying tomorrow.", LogLevel.Info);
            }

            double chance = NightRoll.ChanceTonight(Run, week, season);
            bool dice = rng.NextDouble() < chance;
            DarknessEvent? forced = TakeArmed(night);
            _monitor.Log($"Darkness: night roll {season} {day} at {chance:P0}: {(dice ? "strike" : "quiet")}{(forced != null ? $", armed {forced}" : "")}; level {level}.", LogLevel.Trace);
            if (!dice && forced == null) return;

            DarknessEvent? pick = forced ?? NightRoll.Pick(NightRoll.Options(season), night.CanAct, rng);
            if (pick == null)
            {
                _monitor.Log("Darkness: nothing could act tonight; no strike and the chance does not drop.", LogLevel.Trace);
                return;
            }
            if (!night.Execute(pick.Value))
            {
                _monitor.Log($"Darkness: {pick} was picked but took nothing.", LogLevel.Trace);
                return;
            }
            NightRoll.RecordStrike(Run, week, season);
            SabotageSchedule.RecordStrike(KindOf(pick.Value), Run, week, dayOfYear);
        }

        /// <summary>Everything one night needs, computed lazily so a plan is built once and reused
        /// by the "can it act" test and the strike.</summary>
        private sealed class NightPlan
        {
            private readonly SabotageService _s;
            private readonly CoreSeason _season;
            private readonly int _day, _week, _dayOfYear;
            private readonly DifficultyStep _level;
            private readonly SaveSnapshot _save;
            private readonly ObtainabilityModel _model;
            private readonly Random _rng;
            private int? _crops, _stored;
            private DonatedSlot _reversion; private bool _reversionPlanned, _reversionUnmoderated;
            private TamperPlan _tamper; private bool _tamperPlanned, _tamperUnmoderated;

            public NightPlan(SabotageService s, CoreSeason season, int day, int week, int dayOfYear, DifficultyStep level, SaveSnapshot save, ObtainabilityModel model, Random rng)
            { _s = s; _season = season; _day = day; _week = week; _dayOfYear = dayOfYear; _level = level; _save = save; _model = model; _rng = rng; }

            private RunState Run => _s.Run;

            public bool CanAct(DarknessEvent e)
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
                        if (SabotageSchedule.IsQuietDay(SabotageKind.Tampering, _day) || !SabotageSchedule.WithinCaps(SabotageKind.Tampering, Run, _week, _dayOfYear)) return false;
                        return PlanTamper() != null;
                    default:
                        return false;
                }
            }

            public bool Execute(DarknessEvent e)
            {
                switch (e)
                {
                    case DarknessEvent.CropBlight:
                        return _s.Blight(BlightRule.Count(_crops ?? BlightPass.LiveCropTiles().Count, _season, _level), 0, _rng) > 0;
                    case DarknessEvent.ChestBlight:
                    {
                        bool everything = DarknessLevels.StorageReachesEverything(_level);
                        int units = _stored ?? SpoilagePass.StoredUnits(everything);
                        return _s.Blight(0, BlightRule.SpoilCount(units, _season, _level), _rng) > 0;
                    }
                    case DarknessEvent.Reversion:
                    {
                        DonatedSlot pick = PlanReversion();
                        if (pick == null || !_s.RevertSlot(pick)) return false;
                        if (_reversionUnmoderated) Run.UnmoderatedReversionSpent = true;
                        return true;
                    }
                    case DarknessEvent.Tampering:
                    {
                        TamperPlan plan = PlanTamper();
                        if (plan == null || !_s.WriteTamper(Game1.netWorldState.Value, plan.Target, plan.ItemId, plan.Stack, _dayOfYear)) return false;
                        if (_tamperUnmoderated) Run.UnmoderatedTamperSpent = true;
                        return true;
                    }
                    default:
                        return false;
                }
            }

            private DonatedSlot PlanReversion()
            {
                if (_reversionPlanned) return _reversion;
                _reversionPlanned = true;
                _reversionUnmoderated = NightRoll.UnmoderatedFires(_level, Run.UnmoderatedReversionSpent, _rng);
                int deadline = FairnessRule.ReversionDeadline(_dayOfYear, _level);
                _reversion = _s.PickReversion(_rng, _reversionUnmoderated ? null : (Func<string, bool>)(id => FairnessRule.Counts(id, _dayOfYear, deadline, _level, _save, _model)));
                if (_reversionUnmoderated) _s._monitor.Log("Darkness: this reversion is the loop's unmoderated one.", LogLevel.Info);
                return _reversion;
            }

            private TamperPlan PlanTamper()
            {
                if (_tamperPlanned) return _tamper;
                _tamperPlanned = true;
                _tamperUnmoderated = NightRoll.UnmoderatedFires(_level, Run.UnmoderatedTamperSpent, _rng);
                _tamper = _s.PlanTamper(_rng, _tamperUnmoderated ? null : (Func<string, bool>)(id => FairnessRule.Counts(id, _dayOfYear, FairnessRule.TamperDeadline, _level, _save, _model)));
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

        /// <summary>What tonight's blight must go after, when a playtest named it; null leaves it to
        /// the night's even split. Cleared after tonight's roll.</summary>
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
                DarknessEvent? able = candidates.Cast<DarknessEvent?>().FirstOrDefault(e => night.CanAct(e.Value));
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
        /// now, then queue the report. Returns how many things were taken in all. Also the debug
        /// entry point (<c>tly_sabotage blight [crops] [spoil]</c>).</summary>
        public int Blight(int crops, int spoil, Random rng)
        {
            int killed = BlightPass.Strike(crops, rng);
            SpoilagePass.Taken taken = SpoilagePass.Strike(spoil, rng, DarknessLevels.StorageReachesEverything(Level));
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
            SaveSnapshot save = SaveSnapshotReader.Read();
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
            SaveSnapshot save = SaveSnapshotReader.Read();
            ObtainabilityModel model = _obtainability();
            TamperPlan plan = PlanTamper(rng, id => FairnessRule.Counts(id, dayOfYear, FairnessRule.TamperDeadline, Level, save, model));
            return plan != null && WriteTamper(worldState, plan.Target, plan.ItemId, plan.Stack, dayOfYear);
        }

        /// <summary>The target and replacement a tamper would write, or null when no unfilled slot has
        /// a fair replacement. <paramref name="fair"/> null means the unmoderated roll: the whole
        /// catalog, no check.</summary>
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

        /// <summary>The replacement pool: every catalog item (the board's own universe), with its room
        /// theme and the existing model's effort for closeness, filtered by <paramref name="fair"/>.</summary>
        private IReadOnlyList<TamperCandidate> Candidates(Func<string, bool> fair)
        {
            ItemAvailabilityModel availability = _availability();
            var result = new List<TamperCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CcItem item in CcItemCatalog.Items)
            {
                string id = BundleParsing.NormalizeItemId(item.Id);
                if (!seen.Add(id)) continue;
                if (fair != null && !fair(id)) continue;
                int effort = availability.IsPlaced(id) ? availability.For(id).Effort : 0;
                result.Add(new TamperCandidate(id, item.Theme, effort));
            }
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
                _mail?.SendFirstStrikeLetter(report.Kind);
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
            SaveSnapshot save = SaveSnapshotReader.Read();
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
            };
            foreach (TamperRecord t in Run.Tampers)
                lines.Add($"  tampered: {t.BundleName} slot {t.IngredientIndex}: {Strings.ItemName(t.OldItemId)} -> {t.Stack} {Strings.ItemName(t.NewItemId)} (day {t.DayOfYear})");
            if (Run.PendingSabotageReports.Count > 0)
                lines.Add($"  pending morning reports: {Run.PendingSabotageReports.Count}");
            return string.Join("\n", lines);
        }
    }
}
