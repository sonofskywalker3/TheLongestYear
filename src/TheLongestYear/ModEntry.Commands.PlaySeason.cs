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
        /// <summary><c>tly_playseason [goals]</c>: simulate a minimal compliant player for the CURRENT
        /// season (real-play audit, Jeff 2026-08-28: "I need REAL PLAY SIMULATION DATA"). Donates,
        /// through real CC slot flips plus the run ledger, exactly what every bundle's gate demands
        /// by this season's day 28 (nothing more), pays the vault bundles the season ordinal needs,
        /// and with <c>goals</c> also deposits the current week's goal slots. Then reports whether
        /// the season gate would pass. Follow with <c>tly_setday 28</c> and a sleep so the real gate
        /// runs and the next season's hub samples goals from what is actually left open.</summary>
        private void CmdPlaySeason(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var requirements = _runController?.Requirements;
            if (requirements == null || requirements.Count == 0) { this.Monitor.Log("No requirements on this save.", LogLevel.Warn); return; }
            // "goalsonly": deposit this week's goal slots and nothing else (no gate work, no vault),
            // so a sim can play a goal-completing week without finishing the season's gate on day 1.
            bool goalsOnly = args.Length > 0 && string.Equals(args[0], "goalsonly", StringComparison.OrdinalIgnoreCase);
            bool chaseGoals = goalsOnly || (args.Length > 0 && string.Equals(args[0], "goals", StringComparison.OrdinalIgnoreCase));
            // "quarter <k>": donate only the first k/4 of this season's share, so a sim can spread the
            // season's donations across its four weeks. The four calls are cumulative: every call plans
            // from the same season-start baseline, so quarter 4 lands exactly where a plain call would.
            int quarter = 0;
            if (args.Length > 0 && string.Equals(args[0], "quarter", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out quarter) || quarter < 1 || quarter > 4)
                {
                    this.Monitor.Log("Usage: tly_playseason quarter <1-4>", LogLevel.Warn);
                    return;
                }
            }
            bool quarterMode = quarter > 0;

            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null || worldState.Bundles?.FieldDict == null) { this.Monitor.Log("No bundle data.", LogLevel.Warn); return; }

            // Bundle name -> (index, concrete slots in board order, duplicates kept) from the live board.
            var lines = new Dictionary<string, (int Index, List<BundleSlot> Slots)>(StringComparer.Ordinal);
            foreach (var kvp in worldState.BundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                var slots = new List<BundleSlot>();
                for (int i = 0; i < parsed.Ingredients.Count; i++)
                {
                    string refId = parsed.Ingredients[i].ItemRef;
                    if (BundleParsing.IsCategoryRef(refId)) continue;
                    slots.Add(new BundleSlot(i, BundleParsing.NormalizeItemId(refId)));
                }
                if (!lines.ContainsKey(parsed.Name)) lines[parsed.Name] = (parsed.Index, slots);
            }

            static bool Flip(int bundleIndex, int ingredientIndex)
                => TheLongestYear.Integration.CcSlotWriter.TryFill(bundleIndex, ingredientIndex);

            TheLongestYear.Integration.ItemDonationSync.Reconcile(run);
            SlotLedger donated = run.DonatedLedger();
            int flipped = 0;
            var log = new List<string>();

            // Donation order for one bundle: due-now items first (PerItem), obtainable-by-now items
            // first (Percentage), anything undonated (Seasonal).
            IEnumerable<string> Candidates(BundleRequirement req) => req.Kind switch
            {
                BundleKind.PerItem => req.ItemSeasonPins
                    .Where(kv => (int)kv.Value <= (int)season).OrderBy(kv => (int)kv.Value).Select(kv => kv.Key),
                _ => req.Ingredients
                    .OrderBy(id => Enumerable.Range(0, (int)season + 1).Any(s => _runController.IsObtainableInSeason(id, (TheLongestYear.Core.Season)s)) ? 0 : 1),
            };

            // The first open slot of the bundle that wants one of the candidate ids, candidate order.
            static BundleSlot? FirstOpenSlot(IEnumerable<string> candidates, int bundleIndex, List<BundleSlot> slots, SlotLedger ledger)
            {
                foreach (string id in candidates)
                    foreach (BundleSlot s in slots)
                        if (s.ItemId == id && !ledger.IsFilled(bundleIndex, s.IngredientIndex)) return s;
                return null;
            }

            // The bundle's whole season share as slot picks against ONE simulated ledger shared by
            // every bundle (mirroring the plain mode's sequential loop). Slots are per bundle now, so
            // two bundles that both list Salmonberry each plan their own slot, and a doubled id in one
            // bundle plans both of its slots.
            List<BundleSlot> PlanShare(BundleRequirement req, int bundleIndex, List<BundleSlot> slots, SlotLedger sim)
            {
                var picks = new List<BundleSlot>();
                int guard = 0;
                while (!req.IsSatisfiedAtSeasonEnd(season, sim) && guard++ < 32)
                {
                    BundleSlot? pick = FirstOpenSlot(Candidates(req), bundleIndex, slots, sim);
                    if (pick == null) break;
                    sim.Add(bundleIndex, pick.Value.IngredientIndex, pick.Value.ItemId);
                    picks.Add(pick.Value);
                }
                return picks;
            }

            int cumulative = 0;
            int planCount = 0;
            if (quarterMode)
            {
                // Quarter 1 re-baselines (it marks the season start, and survives a tly_reset); later
                // quarters reuse that baseline so their shares match.
                if (quarter == 1 || _playSeasonBaseline?.Season != season || _playSeasonBaseline?.Donated == null)
                {
                    _playSeasonBaseline = (season, donated.Entries.Select(e => new DonatedSlot { BundleIndex = e.BundleIndex, IngredientIndex = e.IngredientIndex, ItemId = e.ItemId }).ToList());
                    _playSeasonDonatedThisSeason = 0;
                }
                // Every slot the season demands, planned bundle by bundle in the same order the plain
                // mode flips them, against ONE simulated ledger that starts at the season baseline and
                // grows as each bundle is planned (so a shared item is planned once, as before).
                var sim = new SlotLedger(_playSeasonBaseline.Value.Donated);
                var perBundle = new List<List<(BundleRequirement Req, int BundleIndex, int SlotIndex, string ItemId)>>();
                foreach (BundleRequirement req in requirements)
                {
                    if (!lines.TryGetValue(req.Name, out var bundle)) { log.Add($"  {req.Name}: not on the live board, skipped"); continue; }
                    var forBundle = new List<(BundleRequirement, int, int, string)>();
                    foreach (BundleSlot pick in PlanShare(req, bundle.Index, bundle.Slots, sim))
                        forBundle.Add((req, bundle.Index, pick.IngredientIndex, pick.ItemId));
                    perBundle.Add(forBundle);
                }
                // Flatten ROUND-ROBIN, not bundle by bundle: pass 1 takes each bundle's first planned
                // slot in board order, pass 2 each bundle's second, and so on. A flat bundle-by-bundle
                // list put the whole Boiler Room share inside quarter 1, which emptied Mining's weekly
                // goal pool in week 1 of every season; round-robin spreads every room across the four
                // quarters. The quarter is still a prefix of the whole list, not a share per bundle.
                var seasonPlan = new List<(BundleRequirement Req, int BundleIndex, int SlotIndex, string ItemId)>();
                int deepest = 0;
                foreach (var forBundle in perBundle) deepest = Math.Max(deepest, forBundle.Count);
                for (int pass = 0; pass < deepest; pass++)
                    foreach (var forBundle in perBundle)
                        if (pass < forBundle.Count) seasonPlan.Add(forBundle[pass]);

                planCount = seasonPlan.Count;
                cumulative = (int)Math.Ceiling(planCount * quarter / 4.0);
                // Budget the steps this quarter still owes, and spend that budget only on steps that
                // are actually undonated. A flat Take(cumulative) donated nothing in goals mode: goal
                // deposits land after the baseline is taken, so the quarter's whole prefix could
                // already be in the ledger and the quarter flipped zero slots while reporting a
                // cumulative position. Skipping a donated step without consuming the budget lets the
                // quarter reach further down the plan and still do its share of real work.
                int budget = Math.Max(0, cumulative - _playSeasonDonatedThisSeason);
                foreach (var step in seasonPlan)
                {
                    if (budget <= 0) break;
                    if (donated.IsFilled(step.BundleIndex, step.SlotIndex)) continue;
                    if (!Flip(step.BundleIndex, step.SlotIndex)) { log.Add($"  {step.Req.Name}: could not flip slot for {DisplayName(step.ItemId)}"); continue; }
                    run.RecordDonation(step.BundleIndex, step.SlotIndex, step.ItemId);
                    donated = run.DonatedLedger();
                    flipped++;
                    budget--;
                    _playSeasonDonatedThisSeason++;
                    log.Add($"  {step.Req.Name} ({step.Req.Kind}): donated {DisplayName(step.ItemId)} ({step.ItemId})");
                }
            }

            foreach (BundleRequirement req in requirements)
            {
                if (goalsOnly || quarterMode) break;
                if (!lines.TryGetValue(req.Name, out var bundle)) { log.Add($"  {req.Name}: not on the live board, skipped"); continue; }
                int guard = 0;
                while (!req.IsSatisfiedAtSeasonEnd(season, donated) && guard++ < 32)
                {
                    BundleSlot? pick = FirstOpenSlot(Candidates(req), bundle.Index, bundle.Slots, donated);
                    if (pick == null) { log.Add($"  {req.Name}: nothing left to donate but the gate is still open"); break; }
                    if (!Flip(bundle.Index, pick.Value.IngredientIndex)) { log.Add($"  {req.Name}: could not flip slot for {DisplayName(pick.Value.ItemId)}"); break; }
                    run.RecordDonation(bundle.Index, pick.Value.IngredientIndex, pick.Value.ItemId);
                    donated = run.DonatedLedger();
                    flipped++;
                    log.Add($"  {req.Name} ({req.Kind}): donated {DisplayName(pick.Value.ItemId)} ({pick.Value.ItemId}) slot {pick.Value.IngredientIndex}");
                }
            }

            if (chaseGoals && !quarterMode)
            {
                // Both lists on a double week (the second is empty otherwise); each deposit lands on its own list.
                var goalLists = new List<List<BonusSlot>> { run.CurrentWeekBonusSlots, run.SecondWeekBonusSlots ?? new List<BonusSlot>() };
                foreach (List<BonusSlot> goalList in goalLists)
                foreach (BonusSlot slot in goalList)
                {
                    if (Flip(slot.BundleIndex, slot.IngredientIndex))
                    {
                        run.RecordDonation(slot.BundleIndex, slot.IngredientIndex, slot.ItemId);
                        WeeklyGoalCredit.RecordDeposit(goalList, slot.BundleIndex, slot.IngredientIndex);
                        flipped++;
                        log.Add($"  goal: deposited {DisplayName(slot.ItemId)} into {slot.BundleName}");
                    }
                }
                _questService?.OnItemDonated();
            }

            // Vault: the season ordinal (1 by Spring, 2 by Summer ...), cheapest first.
            int needVault = VaultRules.RequiredPaid(season, TheLongestYear.Integration.VaultBundleMap.Count());
            foreach (int idx in TheLongestYear.Integration.VaultBundleMap.Indices())
            {
                if (goalsOnly) break;
                if (quarterMode && quarter < 4) break;
                if (run.VaultBundlesPaid.Count >= needVault) break;
                if (run.VaultBundlesPaid.Contains(idx)) continue;
                if (Flip(idx, 0))
                {
                    DonationService.Active?.OnVaultBundlePaid(idx);
                    log.Add($"  vault: paid bundle {idx} ({TheLongestYear.Integration.VaultBundleMap.GoldForIndex(idx):N0}g)");
                }
            }

            donated = run.DonatedLedger();
            bool vaultOk = VaultRules.IsVaultGateSatisfied(season, run, _meta.State, TheLongestYear.Integration.VaultBundleMap.Count());
            bool gateOk = BundleGate.IsSatisfied(season, donated, requirements, vaultOk);
            this.Monitor.Log(
                quarterMode
                    ? $"tly_playseason: {season} quarter {quarter}, {flipped} slot(s) flipped, {_playSeasonDonatedThisSeason} donated this season, plan position {cumulative} of {planCount}"
                    : $"tly_playseason: {season} day {run.DayOfMonth}, {flipped} slot(s) flipped{(chaseGoals ? " (goals chased)" : "")}, vault {run.VaultBundlesPaid.Count}/{needVault}.",
                LogLevel.Info);
            foreach (string l in log) this.Monitor.Log(l, LogLevel.Info);
            if (quarterMode && quarter < 4) return;
            var open = requirements.Where(r => !r.IsSatisfiedAtSeasonEnd(season, donated)).Select(r => r.Name).ToList();
            // goalsonly never touches the vault or a gate bundle, so its failure is normally "vault
            // unpaid and nothing else": an empty bundle list after "open bundles:" read like a bug.
            string failDetail = open.Count == 0
                ? (vaultOk ? "(no open bundles)" : "(vault unpaid, no open bundles)")
                : $"vault {vaultOk}, open bundles: {string.Join(", ", open)}";
            this.Monitor.Log(
                gateOk ? $"tly_playseason: {season} gate WOULD PASS. Ledger {donated.Count} slot(s)."
                       : $"tly_playseason: {season} gate WOULD FAIL: {failDetail}",
                gateOk ? LogLevel.Info : LogLevel.Error);
        }
    }
}
