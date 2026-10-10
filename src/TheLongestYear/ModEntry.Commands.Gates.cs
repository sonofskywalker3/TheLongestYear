using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary><c>tly_gateneeds</c>: the season gate's remaining demand per bundle, from the
        /// same MissingForSeason the Season Goals page draws, after mirroring the ledger from the
        /// board. Read-only.</summary>
        private void CmdGateNeeds(string command, string[] args)
        {
            if (!Context.IsWorldReady || _runController == null) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            TheLongestYear.Integration.ItemDonationSync.Reconcile(run);
            SlotLedger ledger = run.DonatedLedger();
            string nextSeason = season == TheLongestYear.Core.Season.Winter ? "the win" : $"{(TheLongestYear.Core.Season)((int)season + 1)} 1";
            int open = 0;
            foreach (BundleRequirement req in _runController.Requirements)
            {
                var (count, ids) = req.MissingForSeason(season, ledger);
                if (count == 0) continue;
                open++;
                string names = string.Join(", ", ids.Distinct().Select(id => $"{DisplayName(id)} ({id})"));
                this.Monitor.Log($"  {req.Name} ({req.Kind}, {ledger.FilledCount(req.BundleIndex)}/{req.NumberOfSlots} filled): needs {count} before {nextSeason}: {names}", LogLevel.Info);
            }
            bool vaultOk = VaultRules.IsVaultGateSatisfied(season, run, _meta.State, TheLongestYear.Integration.VaultBundleMap.Count());
            this.Monitor.Log($"  vault: paid {VaultRules.PaidCount(run)} of {VaultRules.RequiredPaid(season, TheLongestYear.Integration.VaultBundleMap.Count())} needed{(vaultOk ? " (satisfied)" : "")}", vaultOk ? LogLevel.Info : LogLevel.Warn);
            this.Monitor.Log($"tly_gateneeds: {season} day {run.DayOfMonth}: {open} bundle(s) still owed before {nextSeason}, {ledger.Count} slot(s) filled on the board.", LogLevel.Info);
        }

        private void CmdGateCheck(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var requirements = _runController?.Requirements;
            if (requirements == null || requirements.Count == 0)
            {
                this.Monitor.Log("No requirements on this save yet.", LogLevel.Warn);
                return;
            }

            // Earliest season each item can exist: the curated pins merged over the pins derived
            // from live game data. Anything unpinned is treated as Spring-obtainable, which is the
            // same assumption the generator's own ramp clamp makes.
            var pins = new Dictionary<string, TheLongestYear.Core.Season>(StringComparer.Ordinal);
            MetaState state = _meta.State;
            try
            {
                TheLongestYear.Core.DifficultyProfile gateCheckDifficulty = state.BoardDifficulty(_config);
                BundleGenerationTuning tuning = TheLongestYear.Core.DifficultyTuning.Scale(
                    _config.PoolTuning, gateCheckDifficulty);
                foreach (var kv in new TheLongestYear.Loop.GameDataPools(this.Monitor)
                        .Build(tuning, TheLongestYear.Core.YearTwoCrops.ExcludedFor(
                            state.HasUpgrade, gateCheckDifficulty.Steps.ItemRarity))
                        .DerivedSeasonPins)
                    pins[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"tly_gatecheck: could not derive season pins ({ex.Message}); using curated pins only.", LogLevel.Warn);
            }
            foreach (var kv in ParseItemSeasonPins())
                pins[kv.Key] = kv.Value;

            LogGateAudit(requirements, pins, "tly_gatecheck");
        }

        /// <summary><c>tly_themepool [theme]</c>: rule C's askable count per theme for the current
        /// week, or the full candidate list for one theme with due/filler, effort, tier and weight.</summary>
        private void CmdThemePool(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (_runController == null) { this.Monitor.Log("No run controller yet.", LogLevel.Warn); return; }

            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            int week = run.WeekOfYear;
            this.Monitor.Log(
                $"tly_themepool: {season} week {week}, filler allowance {_config.FillerAllowanceFor(season)}, "
                + $"selected this month [{string.Join(",", run.SelectedThemesThisMonth)}].",
                LogLevel.Info);

            if (args.Length == 0)
            {
                foreach (TheLongestYear.Core.Theme theme in Enum.GetValues(typeof(TheLongestYear.Core.Theme)))
                {
                    int askable = _runController.AskableCount(theme, season, week);
                    string mark = askable >= SelectionService.MinAskableToOffer ? "offerable" : "not offered";
                    this.Monitor.Log($"  {theme}: askable {askable} ({mark})", LogLevel.Info);
                }
                return;
            }

            if (!Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.Theme picked))
            {
                this.Monitor.Log("Usage: tly_themepool [theme]", LogLevel.Info);
                return;
            }
            IReadOnlyList<GoalWeight> weights = _runController.DescribeGoalPool(picked, season, _meta.Run.WeekOfYear, out IReadOnlyList<BonusSlot> pool);
            var weightById = weights.ToDictionary(w => w.ItemId, w => w, StringComparer.Ordinal);
            this.Monitor.Log($"  {picked}: {pool.Count} open line(s), askable {_runController.AskableCount(picked, season, week)}", LogLevel.Info);
            foreach (BonusSlot slot in pool.OrderByDescending(s => s.Due).ThenBy(s => s.ItemId, StringComparer.Ordinal))
            {
                GoalWeight w = weightById[slot.ItemId];
                string effort = w.Effort.HasValue ? w.Effort.Value.ToString() : "price";
                this.Monitor.Log(
                    $"    {(slot.Due ? "DUE   " : "filler")} {DisplayName(slot.ItemId)} ({slot.ItemId}) effort {effort} tier {w.Tier} weight {w.Weight}  [{slot.BundleName} #{slot.BundleIndex}/{slot.IngredientIndex}]",
                    LogLevel.Info);
            }
        }

        /// <summary><c>tly_goals [season] [weekOfYear]</c>: the weekly goals each theme would offer on the
        /// live board, through the same sampler the planning hub uses, so a season's goals can be
        /// audited from the log without sleeping to that season and opening the hub. Defaults to
        /// the run's own season and week; another season defaults to its week 1.</summary>
        private void CmdGoals(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (_runController == null) { this.Monitor.Log("No run controller yet.", LogLevel.Warn); return; }

            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            if (args.Length > 0 && !Enum.TryParse(args[0], ignoreCase: true, out season))
            {
                this.Monitor.Log("Usage: tly_goals [spring|summer|fall|winter] [weekOfYear]", LogLevel.Info);
                return;
            }
            int week = season == run.Season ? run.WeekOfYear : Calendar.WeekOfYear((int)season, 1);
            if (args.Length > 1 && int.TryParse(args[1], out int explicitWeek))
                week = explicitWeek;

            this.Monitor.Log($"tly_goals: {season} week {week} (run season {run.Season} day {run.DayOfMonth}, loop seed {run.Seed}).", LogLevel.Info);
            foreach (TheLongestYear.Core.Theme theme in Enum.GetValues(typeof(TheLongestYear.Core.Theme)))
            {
                IReadOnlyList<BonusSlot> slots = _runController.PreviewSlotsForTheme(theme, season, week);
                this.Monitor.Log($"  {theme}: {slots.Count} goal(s)", LogLevel.Info);
                foreach (BonusSlot slot in slots)
                {
                    string quality = slot.Quality > 0 ? $" q{slot.Quality}" : "";
                    this.Monitor.Log(
                        $"    - {DisplayName(slot.ItemId)} ({slot.ItemId}) x{slot.Stack}{quality}  [{slot.BundleName} #{slot.BundleIndex}/{slot.IngredientIndex}]",
                        LogLevel.Info);
                }
            }

            // This week's committed lists (both on a double week), with each goal's done state.
            LogCommittedGoals("This week's goals", run.CurrentSelection, run.CurrentGoalMultiplier,
                run.LiabilitySuppressedThisWeek, run.CurrentWeekBonusSlots);
            if (run.SecondSelection.HasValue)
                LogCommittedGoals("Double week second list", run.SecondSelection, run.SecondGoalMultiplier,
                    run.SecondLiabilitySuppressedThisWeek, run.SecondWeekBonusSlots);
        }

        private void LogCommittedGoals(string label, TheLongestYear.Core.Theme? theme, double multiplier, bool lifted,
            IReadOnlyList<BonusSlot> slots)
        {
            if (!theme.HasValue) { this.Monitor.Log($"{label}: no theme picked.", LogLevel.Info); return; }
            this.Monitor.Log(
                $"{label}: {theme} ({slots?.Count ?? 0} goal(s), goal JP {CardMultiplier.Format(multiplier)}{(lifted ? ", drawback lifted" : "")})",
                LogLevel.Info);
            if (slots == null) return;
            foreach (BonusSlot slot in slots)
                this.Monitor.Log(
                    $"    - {DisplayName(slot.ItemId)} ({slot.ItemId}) x{slot.Stack}  [{slot.BundleName} #{slot.BundleIndex}/{slot.IngredientIndex}]" +
                    $"{(slot.Deposited ? " deposited" : "")}{(slot.Paid ? " paid" : "")}",
                    LogLevel.Info);
        }

        /// <summary>The season-gate audit shared by <c>tly_gatecheck</c> (live board) and
        /// <c>tly_genbundles</c> (diagnostic board): demanded vs obtainable per season, IMPOSSIBLE
        /// and FREE flags, and the blockers named per gate. <paramref name="pins"/> is the merged
        /// earliest-season table (derived under curated); anything unpinned counts as Spring.</summary>
        /// <param name="vanillaOnlyRecipes">Names of the bundles whose recipe had no pool of its
        /// own and offered their vanilla items only; tagged [no recipe]. Null when the caller has
        /// no recipe data (the live-board audit).</param>
        private void LogGateAudit(
            IReadOnlyList<BundleRequirement> requirements,
            IReadOnlyDictionary<string, TheLongestYear.Core.Season> pins,
            string label,
            IReadOnlySet<string> vanillaOnlyRecipes = null)
        {
            int impossible = 0, free = 0, tight = 0, stretchLineCount = 0, noHardItemCount = 0, springTightCount = 0;
            var lines = new List<string>();
            var blocked = new List<string>();

            foreach (BundleRequirement req in requirements.OrderBy(r => r.Theme).ThenBy(r => r.Name, StringComparer.Ordinal))
            {
                int[] obtainable = new int[Calendar.MonthsPerYear];
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                    obtainable[season] = req.Ingredients.Count(id =>
                        !pins.TryGetValue(id, out TheLongestYear.Core.Season pinned) || (int)pinned <= season);

                int[] demanded = new int[Calendar.MonthsPerYear];
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                    demanded[season] = DemandAtSeason(req, (TheLongestYear.Core.Season)season, pins);

                var cells = new List<string>();
                string worst = "ok";
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                {
                    string flag = "";
                    if (demanded[season] > obtainable[season])
                    {
                        flag = " IMPOSSIBLE";
                        worst = "IMPOSSIBLE";
                        impossible++;
                        // Name the culprits: an audit that says "4/3" without saying WHICH
                        // ingredient is out of reach leaves the reader to re-derive it by hand.
                        string blockers = string.Join(", ", req.Ingredients
                            .Where(id => pins.TryGetValue(id, out var p2) && (int)p2 > season)
                            .Select(id => _availability != null
                                ? $"{DisplayName(id)} (needs {pins[id]}) [{_availability.For(id).Basis}]"
                                : $"{DisplayName(id)} (needs {pins[id]})"));
                        if (blockers.Length > 0)
                            blocked.Add($"      {req.Name} at {(TheLongestYear.Core.Season)season}: blocked by {blockers}");
                    }
                    else if (demanded[season] == obtainable[season] && demanded[season] > 0)
                    {
                        flag = " tight";
                        if (worst == "ok") worst = "tight";
                        tight++;
                    }
                    cells.Add($"{(TheLongestYear.Core.Season)season}: {demanded[season]}/{obtainable[season]}{flag}");
                }
                if (demanded[Calendar.MonthsPerYear - 1] == 0) { free++; worst = "FREE ALL YEAR"; }

                var tags = new List<string>();
                if (vanillaOnlyRecipes != null && vanillaOnlyRecipes.Contains(req.Name))
                    tags.Add("[no recipe]");
                foreach (KeyValuePair<string, TheLongestYear.Core.Season> stretch in req.StretchLines.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    tags.Add($"[stretch: {DisplayName(stretch.Key)} {stretch.Value}]");
                    stretchLineCount++;
                }

                bool rolled = req.Kind != BundleKind.Seasonal;
                if (_availability != null && BundleSlotFiller.HardItemRuleApplies(_availability)
                    && req.NumberOfSlots >= BundleSlotFiller.MinSlotsForHardItem && rolled
                    && !req.Ingredients.Any(id => EffortTiers.IsHard(_availability.For(id).Effort)))
                {
                    tags.Add("[no hard item]");
                    noHardItemCount++;
                }

                int springDemanded = demanded[(int)TheLongestYear.Core.Season.Spring];
                int springObtainable = obtainable[(int)TheLongestYear.Core.Season.Spring];
                if (springDemanded == springObtainable && springDemanded > 0)
                {
                    tags.Add("[spring tight]");
                    springTightCount++;
                }

                string tagText = tags.Count > 0 ? "  " + string.Join(" ", tags) : "";
                lines.Add($"  [{worst,-13}] {req.Name,-26} {req.Kind,-10} X={req.NumberOfSlots} Y={req.Ingredients.Count}  {string.Join("  |  ", cells)}{tagText}");
            }

            this.Monitor.Log($"=== {label}: season gate audit (demanded / obtainable, by day 28 of each season) ===", LogLevel.Info);
            foreach (string line in lines)
                this.Monitor.Log(line, LogLevel.Info);

            if (blocked.Count > 0)
            {
                this.Monitor.Log("  Ingredients out of reach at the gate that demands them:", LogLevel.Error);
                foreach (string b in blocked)
                    this.Monitor.Log(b, LogLevel.Error);
            }

            this.Monitor.Log(
                $"  Vault gate: pay at least {VaultGateLadder()} " +
                $"(this board's Vault, cheapest first: {string.Join(", ", TheLongestYear.Integration.VaultBundleMap.PriceLadder())}). Owning '{VaultRules.KeepBusUnlockedId}' satisfies it outright.",
                LogLevel.Info);

            this.Monitor.Log(
                impossible > 0
                    ? $"  {label} RESULT: {impossible} IMPOSSIBLE season gate(s) -- these brick the run and must be fixed."
                    : $"  {label} RESULT: no impossible gates. {tight} tight (demands everything obtainable by then), {free} bundle(s) never gated.",
                impossible > 0 ? LogLevel.Error : LogLevel.Info);
            this.Monitor.Log(
                $"  {label} RESULT: {stretchLineCount} stretch line(s), {noHardItemCount} without a hard item, {springTightCount} Spring tight.",
                LogLevel.Info);
            this.Monitor.Log(
                "  NOTE: this checks CALENDAR feasibility only. An item that exists in Spring but needs a keg, "
                + "a fish pond or a tool upgrade counts as obtainable here.",
                LogLevel.Info);
        }

        /// <summary>How many distinct ingredients this bundle's gate demands by the end of
        /// <paramref name="season"/>, expressed the same way for all three bundle kinds so they can
        /// be compared against obtainability on one scale.</summary>
        private static int DemandAtSeason(
            BundleRequirement req, TheLongestYear.Core.Season season,
            IReadOnlyDictionary<string, TheLongestYear.Core.Season> pins)
        {
            switch (req.Kind)
            {
                case BundleKind.Seasonal:
                    // Its X slots (every slot unless pick X of Y), once its named season has arrived.
                    return (int)req.SeasonalSeason.Value <= (int)season ? req.NumberOfSlots : 0;

                case BundleKind.PerItem:
                    // Each pinned ingredient is due at its own pin.
                    return req.ItemSeasonPins.Count(kv => (int)kv.Value <= (int)season);

                case BundleKind.Percentage:
                    // Capped by the slot count, the same way SeasonNeed.For caps it. The ramp is
                    // clamped at build time (GeneratedBundleSet.ClampRampForObtainability), so
                    // this is the second layer rather than the fix: it keeps the audit honest
                    // about a ramp that arrives over-deep from anywhere else, which is what
                    // Nexus 1137357 looked like from the player's side (a gate demanding 9 of an
                    // 8-slot bundle, unfillable however green the bundle went).
                    return req.NumberOfSlots > 0
                        ? Math.Min(req.CumulativeRequiredBySeason[(int)season], req.NumberOfSlots)
                        : req.CumulativeRequiredBySeason[(int)season];

                default:
                    return 0;
            }
        }

        /// <summary>"1 money bundle(s) by Spring 28, 2 by Summer ..." for this board's Vault count
        /// (an Easy board has three, so Winter asks for three).</summary>
        private static string VaultGateLadder()
        {
            int count = TheLongestYear.Integration.VaultBundleMap.Count();
            string Need(TheLongestYear.Core.Season s) => VaultRules.RequiredPaid(s, count).ToString();
            return $"{Need(TheLongestYear.Core.Season.Spring)} money bundle(s) by Spring 28, {Need(TheLongestYear.Core.Season.Summer)} by Summer, " +
                $"{Need(TheLongestYear.Core.Season.Fall)} by Fall, {Need(TheLongestYear.Core.Season.Winter)} by Winter";
        }
    }
}
