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
        private void PrintMeta(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            MetaState s = _meta.State;
            int stashSlots = s.StashSlotCount;
            int stashItems = s.StashItems.Count;
            string stashTile = (_config.StashTileX == 0 && _config.StashTileY == 0)
                ? "auto (relative to farmhouse entry)"
                : $"({_config.StashTileX}, {_config.StashTileY})";

            this.Monitor.Log(
                $"JP={s.JunimoPoints}, " +
                $"StashTier={s.HighestKeptTier("stash_", 3)} ({stashSlots} slots, {stashItems} items banked, tile {stashTile}), " +
                $"Upgrades=[{string.Join(", ", s.OwnedUpgrades)}]",
                LogLevel.Info);
        }

        private void AddJp(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1 || !long.TryParse(args[0], out long amount))
            {
                this.Monitor.Log("Usage: tly_addjp <amount>", LogLevel.Warn);
                return;
            }

            _meta.State.JunimoPoints += amount;
            this.Monitor.Log($"JP is now {_meta.State.JunimoPoints} (in memory — persists on next save).", LogLevel.Info);
        }

        /// <summary>Debug: add gold to the loaded farmer. Mirrors <see cref="AddJp"/>; used for
        /// playtest setup (e.g. enough to upgrade the farmhouse). Usage: tly_addmoney &lt;amount&gt;.</summary>
        private void AddMoney(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1 || !int.TryParse(args[0], out int amount))
            {
                this.Monitor.Log("Usage: tly_addmoney <amount>", LogLevel.Warn);
                return;
            }

            Game1.player.Money += amount;
            this.Monitor.Log($"Gold is now {Game1.player.Money}.", LogLevel.Info);
        }

        /// <summary>Debug: grant an item to the farmer (overflow goes to the item-grab menu so
        /// nothing is lost). Usage: <c>tly_additem &lt;qualifiedId&gt; [count]</c> — e.g.
        /// <c>tly_additem (O)709 100</c> for the 100 Hardwood a Stable build needs.</summary>
        private void CmdAddItem(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_additem <qualifiedId> [count]", LogLevel.Warn); return; }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            Item item;
            try { item = ItemRegistry.Create(args[0], count); }
            catch (Exception ex)
            {
                this.Monitor.Log($"tly_additem: couldn't create '{args[0]}': {ex.Message}", LogLevel.Warn);
                return;
            }

            Game1.player.addItemByMenuIfNecessary(item);
            this.Monitor.Log($"tly_additem: granted {count}x {args[0]} ({item.DisplayName}).", LogLevel.Info);
        }

        private void CmdActiveEffects(string command, string[] args)
        {
            string bonus = TheLongestYear.Core.ActiveEffectsProvider.BonusId ?? "(none)";
            string liability = TheLongestYear.Core.ActiveEffectsProvider.LiabilityId ?? "(none)";
            this.Monitor.Log(
                $"Active effects: bonus={bonus}, liability={liability}. " +
                $"Selection={_meta?.Run.CurrentSelection?.ToString() ?? "none"}." +
                (TheLongestYear.Core.ActiveEffectsProvider.LiabilitySuppressed ? " (liability lifted)" : ""),
                LogLevel.Info);
            if (TheLongestYear.Core.ActiveEffectsProvider.SecondBonusId != null)
                this.Monitor.Log(
                    $"Double week second entry: bonus={TheLongestYear.Core.ActiveEffectsProvider.SecondBonusId}, " +
                    $"liability={TheLongestYear.Core.ActiveEffectsProvider.SecondLiabilityId ?? "(none)"}. " +
                    $"Selection={_meta?.Run.SecondSelection?.ToString() ?? "none"}." +
                    (TheLongestYear.Core.ActiveEffectsProvider.SecondLiabilitySuppressed ? " (liability lifted)" : ""),
                    LogLevel.Info);
            if (_meta == null) return;
            int today = TodayDayOfYear();
            foreach (TheLongestYear.Core.ActiveBoost b in _meta.Run.ActiveBoosts)
                this.Monitor.Log(
                    $"  boost {b.Id}: bought day {b.BoughtDay}, through day {b.ExpiresAfterDay}" +
                    $"{(b.Skill >= 0 ? $", skill {b.Skill}" : "")}{(b.IsActiveOn(today) ? " (active)" : " (expired)")}",
                    LogLevel.Info);
            string[] bonusIds = { "forage_yield_up", "fish_bite_up", "crop_growth_up", "mine_drops_up", "all_drops_up",
                "monster_drops_double", "machines_fast", "animal_double_product" };
            var stacks = new System.Collections.Generic.List<string>();
            foreach (string id in bonusIds)
            {
                int n = TheLongestYear.Core.ActiveEffectsProvider.BonusStacks(id);
                if (n > 0) stacks.Add($"{id}={n}");
            }
            this.Monitor.Log($"  stacks: {(stacks.Count == 0 ? "(none)" : string.Join(", ", stacks))}; " +
                $"crash course bought {_meta.Run.SkillLevelsBoughtTotal}; weather override day {_meta.Run.WeatherOverrideDay} = {_meta.Run.WeatherOverride ?? "(none)"}.",
                LogLevel.Info);
        }

        private void CmdTestDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_testdonate <qualifiedId> [count]", LogLevel.Warn); return; }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            // The ledger mirrors the board, so fill the board slot first and pay with slot identity.
            var slot = TheLongestYear.Integration.CcSlotWriter.FirstOpenSlotFor(args[0]);
            if (slot == null) { this.Monitor.Log($"tly_testdonate: no open slot wants '{args[0]}'.", LogLevel.Warn); return; }
            TheLongestYear.Integration.CcSlotWriter.TryFill(slot.Value.BundleIndex, slot.Value.IngredientIndex);
            DonationService.Active?.OnItemDonated(args[0], count, slot.Value.BundleIndex, slot.Value.IngredientIndex);
        }

        /// <summary>Wildcard days debug: the week's plan, or set/clear today's twist.</summary>
        private void CmdWildcard(string command, string[] args)
        {
            if (!Context.IsWorldReady || _wildcardDays == null) { this.Monitor.Log("Load a TLY save first.", LogLevel.Warn); return; }
            this.Monitor.Log("tly_wildcard: " + _wildcardDays.Debug(args), LogLevel.Info);
        }

        /// <summary>Random shrine donations debug: list this week's shrine goals.</summary>
        private void CmdShrineGoals(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            ShrineDonationDebug.List(this.Monitor);
        }

        /// <summary>Random shrine donations debug: donate goal N through ShrineDonationService.</summary>
        private void CmdShrineDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            ShrineDonationDebug.Donate(args, this.Monitor);
        }

        private void CmdHubCards(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.activeClickableMenu is not TheLongestYear.UI.WeeklyHubMenu hub)
            {
                this.Monitor.Log("tly_hubcards: the planning hub is not open.", LogLevel.Warn);
                return;
            }
            hub.LogCards();
        }

        /// <summary>Headless re-roll check: presses the hub's re-roll button, or closes and reopens
        /// the hub so a run can see the re-rolled pair restored.</summary>
        private void CmdReroll(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.activeClickableMenu is not TheLongestYear.UI.WeeklyHubMenu hub)
            {
                this.Monitor.Log("tly_reroll: the planning hub is not open.", LogLevel.Warn);
                return;
            }
            if (args.Length > 0 && args[0].Equals("reopen", StringComparison.OrdinalIgnoreCase))
            {
                Game1.activeClickableMenu = null;
                _launcher?.OpenWeeklyHub();
                return;
            }
            if (args.Length > 0 && args[0].Equals("paid", StringComparison.OrdinalIgnoreCase))
            {
                // The reroll button's own code: price check, JP spend, RerollCanChange gate.
                this.Monitor.Log(hub.RerollPaidForDebug(), LogLevel.Info);
                return;
            }
            int count = args.Length > 0 && int.TryParse(args[0], out int n) && n > 0 ? n : 1;
            for (int i = 0; i < count; i++)
                hub.RerollForDebug();
        }

        /// <summary>Diagnostics only: the maximum JP the CURRENT loop's board can pay out, per
        /// season and in total, under two models — "donate as soon as obtainable" and the
        /// "strong player" (meet each checkpoint minimum with the cheapest obtainable slots, hoard
        /// the rest for Winter's 4×) — plus a hoard-everything ceiling. Pure maths in
        /// <see cref="JpBudgetCalculator"/>; this reduces the live BundleData + CcItem catalog +
        /// resolved requirements to its inputs. Baseline economy — no jp_boost tiers applied.
        /// Usage: tly_jpbudget [verbose]</summary>
        private void CmdJpBudget(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            bool verbose = args.Length > 0 && string.Equals(args[0], "verbose", StringComparison.OrdinalIgnoreCase);

            var catalogById = new Dictionary<string, CcItem>(StringComparer.Ordinal);
            foreach (CcItem item in _catalog)
                catalogById[item.Id] = item;
            var reqByName = new Dictionary<string, BundleRequirement>(StringComparer.Ordinal);
            foreach (BundleRequirement req in _requirements)
                if (!reqByName.ContainsKey(req.Name))
                    reqByName[req.Name] = req;

            var bundles = new List<BudgetBundle>();
            var notInCatalog = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> kvp in Game1.netWorldState.Value.BundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                int vaultGold = TheLongestYear.Integration.VaultBundleMap.GoldForIndex(parsed.Index);
                if (vaultGold > 0)
                {
                    bundles.Add(new BudgetBundle(parsed.Room, parsed.Name, parsed.NumberOfSlots, new List<BudgetSlot>(), vaultGold));
                    continue;
                }

                reqByName.TryGetValue(parsed.Name, out BundleRequirement requirement);
                var slots = new List<BudgetSlot>();
                foreach (BundleIngredient ing in parsed.Ingredients)
                {
                    if (BundleParsing.IsCategoryRef(ing.ItemRef)) continue;
                    string id = BundleParsing.NormalizeItemId(ing.ItemRef);
                    Rarity rarity;
                    int earliest;
                    if (catalogById.TryGetValue(id, out CcItem cc))
                    {
                        rarity = cc.Rarity;
                        earliest = cc.ObtainableSeasons.Min(x => (int)x);
                    }
                    else
                    {
                        notInCatalog.Add(id);
                        rarity = TheLongestYear.Donations.ItemRarityResolver.Resolve(id, _config.RarityThresholds);
                        earliest = 0;
                    }
                    int? pin = null;
                    if (requirement?.Kind == BundleKind.PerItem && requirement.ItemSeasonPins != null
                        && requirement.ItemSeasonPins.TryGetValue(id, out TheLongestYear.Core.Season pinned))
                        pin = (int)pinned;
                    slots.Add(new BudgetSlot(id, rarity, earliest, pin));
                    if (verbose)
                        this.Monitor.Log(
                            $"  {parsed.Room}/{parsed.Index} '{parsed.Name}' {id} {rarity} earliest={(TheLongestYear.Core.Season)earliest}" +
                            (pin.HasValue ? $" pin={(TheLongestYear.Core.Season)pin.Value}" : "") +
                            (catalogById.ContainsKey(id) ? "" : " (not in catalog)"),
                            LogLevel.Info);
                }

                int? seasonal = requirement?.Kind == BundleKind.Seasonal && requirement.SeasonalSeason.HasValue
                    ? (int)requirement.SeasonalSeason.Value : (int?)null;
                IReadOnlyList<int> quota = requirement?.Kind == BundleKind.Percentage
                    ? requirement.CumulativeRequiredBySeason : null;
                bundles.Add(new BudgetBundle(parsed.Room, parsed.Name, parsed.NumberOfSlots, slots, 0, seasonal, quota));
            }

            JpBudgetReport report = JpBudgetCalculator.Compute(
                bundles, _config.Jp, _config.SelectionBonusMultiplier, BonusItemSampler.DefaultMaxCountBySeason,
                _meta.State.EffectiveDifficulty(_config).JpEarnedFactor);

            this.Monitor.Log(
                $"tly_jpbudget: loop {_meta.State.CompletedResets} (run {_meta.Run.RunNumber}), {bundles.Count} bundles, " +
                $"{bundles.Sum(b => b.Slots.Count)} item slots ({bundles.Sum(b => b.VaultGold > 0 ? 0 : Math.Min(b.NumberOfSlots, b.Slots.Count))} payable). " +
                "Baseline economy, no jp_boost.",
                LogLevel.Info);
            if (notInCatalog.Count > 0)
                this.Monitor.Log($"  not in the CcItem catalog (Spring/price-rarity fallback): {string.Join(", ", notInCatalog)}", LogLevel.Info);
            LogModel("EARLIEST model (donate as soon as obtainable)", report.Earliest, report);
            LogModel("STRONG model (checkpoint minimums only, hoard the rest for Winter)", report.Strong, report);
            this.Monitor.Log(
                $"  TOTALS: earliest = {report.EarliestTotal} JP; strong = {report.StrongTotal} JP; " +
                $"hoard-everything ceiling = {report.HoardCeiling} JP (ignores checkpoints — upper bound only). " +
                $"Fixed awards inside each total: {report.FixedAwards} (weekly {report.WeeklyQuest.Sum()}, checkpoints {report.Checkpoint.Sum()}, vault {report.Vault.Sum()}).",
                LogLevel.Info);
            foreach (string gate in report.ImpossibleGates)
                this.Monitor.Log($"  IMPOSSIBLE GATE: {gate}", LogLevel.Warn);
        }

        private void LogModel(string title, JpBudgetModel model, JpBudgetReport report)
        {
            this.Monitor.Log($"  {title}:", LogLevel.Info);
            this.Monitor.Log("    season  slots  donation  selBonus  bundles  rooms  weekly  checkpoint  vault  TOTAL", LogLevel.Info);
            for (int s = 0; s < Calendar.MonthsPerYear; s++)
            {
                long total = model.Donation[s] + model.SelectionBonus[s] + model.BundleBonus[s] + model.RoomBonus[s] + report.FixedAwardsFor(s);
                this.Monitor.Log(
                    $"    {(TheLongestYear.Core.Season)s,-6}  {model.Slots[s],5}  {model.Donation[s],8}  {model.SelectionBonus[s],8}  " +
                    $"{model.BundleBonus[s],7}  {model.RoomBonus[s],5}  {report.WeeklyQuest[s],6}  {report.Checkpoint[s],10}  " +
                    $"{report.Vault[s],5}  {total,5}",
                    LogLevel.Info);
            }
        }

        private void CmdListUpgrades(string command, string[] args)
        {
            this.Monitor.Log($"Upgrade catalog: {UpgradeCatalog.All.Count} entries.", LogLevel.Info);
            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                var rows = UpgradeCatalog.ByCategory(cat);
                this.Monitor.Log($"  {cat} ({rows.Count}):", LogLevel.Info);
                foreach (var u in rows)
                {
                    string owned = _meta != null && _meta.State.HasUpgrade(u.Id) ? " [OWNED]" : "";
                    string prereq = u.PrerequisiteId != null ? $" (req {u.PrerequisiteId})" : "";
                    this.Monitor.Log($"    - {u.Id}: {u.DisplayName} — {TheLongestYear.Core.UpgradePricing.EffectiveCost(u, _meta.State.EffectiveDifficulty(_config))} JP{prereq}{owned}", LogLevel.Info);
                }
            }
        }

        private void CmdBuyUpgrade(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_buyupgrade <id>", LogLevel.Warn); return; }
            _purchases?.TryPurchase(args[0]);
        }

        /// <summary>Debug: run the shrine board's boost purchase without clicking it, so the
        /// headless bridge can exercise the same callback the Buy button uses.
        /// Usage: tly_boost &lt;yeartwoseeds|sneakpeek&gt;</summary>
        private void CmdBoost(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (_boostPurchases == null) { this.Monitor.Log("tly_boost: boost service not wired yet.", LogLevel.Warn); return; }
            if (args.Length < 1 || args[0].Equals("list", System.StringComparison.OrdinalIgnoreCase))
            {
                foreach (TheLongestYear.Core.BoostDefinition def in TheLongestYear.Core.BoostCatalog.All)
                {
                    int skill = def.Id == BoostId.CrashCourse ? 0 : -1;
                    TheLongestYear.Core.BoostContext ctx = _boostPurchases.Context(skill);
                    this.Monitor.Log(
                        $"  {def.Id,-14} {def.Duration,-7} {TheLongestYear.Core.BoostPricing.CostOf(def, _meta.Run, ctx),5} JP  " +
                        $"{TheLongestYear.Core.BoostPurchase.StateOf(_meta.State, _meta.Run, def.Id, ctx)}" +
                        (def.Id == BoostId.CrashCourse ? " (farming shown; pass a skill name)" : ""),
                        LogLevel.Info);
                }
                this.Monitor.Log("Usage: tly_boost <id> [farming|fishing|foraging|mining|combat]", LogLevel.Info);
                return;
            }
            if (!System.Enum.TryParse(args[0], ignoreCase: true, out BoostId id))
            {
                this.Monitor.Log($"tly_boost: unknown boost '{args[0]}'. Run tly_boost list.", LogLevel.Warn);
                return;
            }
            int skillArg = -1;
            if (args.Length > 1)
            {
                skillArg = args[1].ToLowerInvariant() switch
                {
                    "farming" => 0, "fishing" => 1, "foraging" => 2, "mining" => 3, "combat" => 4, _ => -1,
                };
                if (skillArg < 0) { this.Monitor.Log($"tly_boost: unknown skill '{args[1]}'.", LogLevel.Warn); return; }
            }
            _boostPurchases.TryBuy(id, skillArg);
        }

        private void CmdHold(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (mode)
            {
                case "keep":
                case "reshuffle":
                    bool keep = mode == "keep";
                    var result = BundleHold.Apply(s, keep: keep, _config.BundleHoldCosts, s.EffectiveDifficulty(_config).HoldPriceFactor);
                    this.Monitor.Log($"tly_hold {mode}: {result}. JP {s.JunimoPoints}, consecutive holds {s.ConsecutiveHolds}, seed loop {s.BundleSeedLoop}, choice stamped {s.HoldChoiceMadeForReset}.", LogLevel.Info);
                    this.Monitor.Log("tly_hold: run tly_reset before sleeping or this choice goes stale.", LogLevel.Warn);
                    break;
                default:
                    this.Monitor.Log($"tly_hold status: CompletedResets {s.CompletedResets}, seed loop {s.EffectiveBundleSeedLoop} (stored {s.BundleSeedLoop}), consecutive holds {s.ConsecutiveHolds}, next hold costs {BundleHold.NextCost(s, _config.BundleHoldCosts, s.EffectiveDifficulty(_config).HoldPriceFactor)} JP, choice stamped {s.HoldChoiceMadeForReset}.", LogLevel.Info);
                    break;
            }
        }

        private void CmdPayVault(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1)
            {
                this.Monitor.Log("Usage: tly_payvault <Spring|Summer|Fall|Winter|index>", LogLevel.Warn);
                return;
            }

            int bundleIndex;
            if (int.TryParse(args[0], out bundleIndex))
            {
                // direct index
            }
            else if (System.Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.Season s))
            {
                // Resolve against THIS save's actual vault indices (remix-aware), not the vanilla 23–26.
                bundleIndex = TheLongestYear.Integration.VaultBundleMap.IndexForSeason(s);
                if (bundleIndex < 0)
                {
                    this.Monitor.Log("No vault bundle data available for that season on this save.", LogLevel.Warn);
                    return;
                }
            }
            else
            {
                this.Monitor.Log($"Unknown argument '{args[0]}'.", LogLevel.Warn);
                return;
            }

            if (!_meta.Run.VaultBundlesPaid.Contains(bundleIndex))
                _meta.Run.VaultBundlesPaid.Add(bundleIndex);
            this.Monitor.Log(
                $"Vault bundle {bundleIndex} marked paid. Paid this run: [{string.Join(", ", _meta.Run.VaultBundlesPaid)}]",
                LogLevel.Info);
        }
    }
}
