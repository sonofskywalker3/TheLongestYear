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
    /// <summary>Debug arming and the tly_sabotage readouts.</summary>
    internal sealed partial class SabotageService
    {
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


        // ------------------------------------------------------------------ readouts (debug)

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
                 + $"tampering (deadline day {FairnessRule.TamperDeadline}): {asTamper.Summary}\n"
                 + TargetLine(itemId);
        }

        /// <summary>Can a tamper take this item as its target? Only an item the board asks for in
        /// exactly one slot (designer, 2026-10-07). A flavoured item is counted per flavour, so this
        /// gives one line for each flavour the board names.</summary>
        private string TargetLine(string itemId)
        {
            string id = BundleParsing.NormalizeItemId(itemId);
            IReadOnlyList<BundleRequirement> requirements = _requirements();
            var flavors = new List<string>();
            foreach (BundleRequirement req in requirements)
                foreach (BundleSlot slot in req.Slots)
                    if (BundleParsing.NormalizeItemId(slot.ItemId) == id)
                    {
                        string f = FlavoredSlotRules.IsFlavored(slot.ItemId) ? BoardFlavor(req.BundleIndex, slot.IngredientIndex) : null;
                        if (!flavors.Contains(f)) flavors.Add(f);
                    }
            if (flavors.Count == 0) return "tamper target: no, the board does not ask for it";
            return string.Join("\n", flavors.Select(f =>
            {
                int n = TamperRule.SlotsAsking(requirements, id, f, BoardFlavor);
                return $"tamper target ({ExactName(id, f)}): {(n == 1 ? "yes, asked in exactly one slot" : $"no, asked in {n} slots (multi-slot items are never targets)")}";
            }));
        }

        /// <summary>Status lines on what a tamper may take tonight: the open slots whose item is asked
        /// once, and the open items it may not take because another slot asks for them too.</summary>
        private IEnumerable<string> TargetSummary()
        {
            SlotLedger ledger = Run.DonatedLedger();
            IReadOnlyList<BundleRequirement> requirements = _requirements();
            IReadOnlyList<TamperTarget> targets = TamperRule.Targets(ledger, requirements, BoardFlavor);
            var single = new HashSet<(int, int)>(targets.Select(t => (t.Bundle.BundleIndex, t.IngredientIndex)));
            var multi = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (BundleRequirement req in requirements)
            {
                if (req.BundleIndex < 0 || !ReversionRule.IsItemRoomTheme(req.Theme) || req.IsFullyComplete(ledger)) continue;
                foreach (BundleSlot slot in req.Slots)
                {
                    if (ledger.IsFilled(req.BundleIndex, slot.IngredientIndex) || single.Contains((req.BundleIndex, slot.IngredientIndex))) continue;
                    string f = FlavoredSlotRules.IsFlavored(slot.ItemId) ? BoardFlavor(req.BundleIndex, slot.IngredientIndex) : null;
                    multi[ExactName(slot.ItemId, f)] = TamperRule.SlotsAsking(requirements, slot.ItemId, f, BoardFlavor);
                }
            }
            string named = string.Join(", ", targets.Take(StatusListCap).Select(t => ExactName(t.ItemId, t.Flavor)));
            yield return $"  tamper targets (open slots whose exact item, flavour included, is asked in exactly one slot on the board): {targets.Count}"
                + (targets.Count == 0 ? "" : $": {named}{(targets.Count > StatusListCap ? ", ..." : "")}");
            yield return $"  not targets (asked in 2+ slots, filled or open; multi-slot items are never tampered): {(multi.Count == 0 ? "none" : string.Join(", ", multi.Select(kv => $"{kv.Key} x{kv.Value}")))}";
        }

        /// <summary>How many tamper targets the status line names before it trails off.</summary>
        private const int StatusListCap = 12;

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
                $"  Scenes played this loop: {string.Join(", ", (Run.StrikeScenesPlayed ?? Enumerable.Empty<string>()).DefaultIfEmpty("none"))}",
                $"  Scenes seen on save: {string.Join(", ", (Meta.StrikeScenesSeen ?? Enumerable.Empty<string>()).DefaultIfEmpty("none"))}",
                $"  Struck this loop: {string.Join(", ", (Run.StruckEvents ?? Enumerable.Empty<string>()).DefaultIfEmpty("none"))}",
                $"  Owed tonight (every-loop guarantee, from day {StrikeGuarantee.ForceFromDay} of the debut season; forced only if it can act): {string.Join(", ", StrikeGuarantee.Owed(Run.Season, Run.DayOfMonth, Run.StruckEvents ?? new HashSet<string>()).Select(e => e.ToString()).DefaultIfEmpty("none"))}",
                $"  Witness lines pending: {string.Join(", ", (Run.WitnessLines ?? new List<WitnessRecord>()).Where(r => !r.Said).Select(r => $"{r.Npc} (scene day {r.SceneDayOfYear}, until day {r.SceneDayOfYear + WitnessLines.WindowDays})").DefaultIfEmpty("none"))}",
            };
            lines.AddRange(TargetSummary());
            foreach (TamperRecord t in Run.Tampers)
                lines.Add($"  tampered: {t.BundleName} slot {t.IngredientIndex}: {ExactName(t.OldItemId, t.OldFlavor)} [flavour {t.OldFlavor ?? "none"}] -> {t.Stack} {Strings.ItemName(t.NewItemId)} (day {t.DayOfYear})");
            if (Run.PendingSabotageReports.Count > 0)
                lines.Add($"  pending morning reports: {Run.PendingSabotageReports.Count}");
            return string.Join("\n", lines);
        }
    }
}
