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
    /// <summary>Tampering: the target, the replacement and the board write.</summary>
    internal sealed partial class SabotageService
    {
        // ------------------------------------------------------------------ tampering

        /// <summary>Debug entry point (<c>tly_sabotage tamper</c>): a fair pick at the current level, written now.</summary>
        public bool Tamper(Random rng, int dayOfYear)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return false;
            TamperPlan plan = PlanFairTamper(rng, dayOfYear);
            return plan != null && WriteTamper(worldState, plan.Target, plan.ItemId, plan.Stack, dayOfYear);
        }

        /// <summary>Debug entry point (<c>tly_sabotage scene cloud</c>): a fresh fair tamper plan,
        /// parked rather than written, so the cloud scene lands it at its own beat exactly as the
        /// overnight path would, and the morning's Junimo scene and popup follow from it. Null when
        /// no unfilled slot has a fair replacement, and then the caller decides whether to watch the
        /// scene against a no-op instead.</summary>
        public PendingStrike PrepareTamper(Random rng, int dayOfYear)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return null;
            TamperPlan plan = PlanFairTamper(rng, dayOfYear);
            if (plan == null) return null;
            _monitor.Log($"Darkness: the cloud scene will rewrite {plan.Target.Bundle.Name} slot {plan.Target.IngredientIndex} ({ExactName(plan.Target.ItemId, plan.Target.Flavor)}) to {plan.Stack} {Strings.ItemName(plan.ItemId)}.", LogLevel.Info);
            return new PendingStrike(DarknessEvent.Tampering, () => WriteTamper(worldState, plan.Target, plan.ItemId, plan.Stack, dayOfYear));
        }

        /// <summary>The fair tamper plan both debug entry points share: the save and obtainability
        /// model the fairness rule reads, at the current level.</summary>
        private TamperPlan PlanFairTamper(Random rng, int dayOfYear)
        {
            SaveSnapshot save = SaveSnapshotReader.Read(msg => _monitor.Log(msg, LogLevel.Trace));
            ObtainabilityModel model = _obtainability();
            return PlanTamper(rng, id => FairnessRule.Counts(id, dayOfYear, FairnessRule.TamperDeadline, Level, save, model));
        }

        /// <summary>The target and replacement a tamper would write, or null when no unfilled slot has
        /// a fair replacement. <paramref name="fair"/> null means the unmoderated roll: every item
        /// the board's pools hold, no check.</summary>
        private TamperPlan PlanTamper(Random rng, Func<string, bool> fair)
        {
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            IReadOnlyList<BundleRequirement> requirements = _requirements();
            List<TamperTarget> targets = TamperRule.Targets(ledger, requirements, BoardFlavor).ToList();
            if (targets.Count == 0)
            {
                // The same "no fair target" path as ever: tonight's roll takes another event, and
                // the guaranteed Winter tamper retries tomorrow.
                _monitor.Log("Darkness: tampering has no target: no open slot in an unfinished bundle asks for an item that no other slot on the board asks for.", LogLevel.Info);
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
                TamperCandidate replacement = TamperRule.PickReplacement(target, effort, candidates, rng, Run.Tampers, requirements);
                if (replacement == null) continue;
                int maxCount = TamperRule.MaxCount(replacement.ItemId, QuantityAskPass.BasisByDeadline(replacement.ItemId, CoreSeason.Winter));
                int stack = TamperRule.Stack(maxCount, weekOfWinter, Level, rng);
                return new TamperPlan { Target = target, ItemId = replacement.ItemId, Stack = stack };
            }
            _monitor.Log("Darkness: tampering found no fair replacement for any open slot (a replacement must be new to the board and never a tainted item).", LogLevel.Info);
            return null;
        }

        /// <summary>The flavour the live board's slot names (bundle index, slot index), from the map
        /// FlavoredSlotPatch applies; null when it names none.</summary>
        private string BoardFlavor(int bundleIndex, int ingredientIndex)
        {
            Dictionary<string, string> map = Meta.WrittenBoardFlavors;
            return map != null && map.TryGetValue(FlavoredSlotPass.KeyFor(bundleIndex, ingredientIndex), out string f) ? f : null;
        }

        /// <summary>The exact item a tamper takes, as the game would build it: a flavoured Dried
        /// Fruit or Smoked Fish when <paramref name="flavor"/> is set, else the plain item. Null when
        /// the game cannot make it.</summary>
        internal static Item CreateExact(string itemId, string flavor)
        {
            if (string.IsNullOrEmpty(flavor)) return ItemRegistry.Create(itemId, 1, 0, allowNull: true);
            string preserveType = BundleParsing.StripQualifier(FlavoredSlotRules.WrittenIdFor(itemId));
            return Utility.CreateFlavoredItem(preserveType, BundleParsing.StripQualifier(flavor));
        }

        /// <summary>The exact item's display name: "Dried Apples" for a flavoured slot, the plain
        /// name otherwise.</summary>
        internal static string ExactName(string itemId, string flavor)
        {
            if (string.IsNullOrEmpty(flavor)) return Strings.ItemName(itemId);
            Item made = CreateExact(itemId, flavor);
            return made != null
                ? made.DisplayName
                : $"{Strings.ItemName(itemId)} ({Strings.ItemName(BundleParsing.NormalizeItemId(flavor))})";
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

            // The slot now asks for something else, so it must not keep the old fruit: FlavoredSlotPatch
            // would pin it onto a new flavoured ask ("Smoked Apple").
            if (TamperRule.ClearFlavor(Meta.WrittenBoardFlavors, target.Bundle.BundleIndex, target.IngredientIndex))
                _monitor.Log($"Darkness: {target.Bundle.Name} slot {target.IngredientIndex} no longer names a flavour (was {target.Flavor ?? "none"}).", LogLevel.Trace);

            // A goal card pointing at the old item would show a slot the board no longer has.
            Run.CurrentWeekBonusSlots?.RemoveAll(s =>
                s.BundleIndex == target.Bundle.BundleIndex && s.IngredientIndex == target.IngredientIndex);

            Run.Tampers.Add(new TamperRecord
            {
                BundleIndex = target.Bundle.BundleIndex,
                IngredientIndex = target.IngredientIndex,
                BundleName = target.Bundle.Name,
                OldItemId = target.ItemId,
                OldFlavor = target.Flavor,
                NewItemId = newItemId,
                Stack = stack,
                DayOfYear = dayOfYear,
            });
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Tampering, Count = stack, BundleName = target.Bundle.Name,
                ItemId = newItemId, OldItemId = target.ItemId, OldFlavor = target.Flavor,
            });
            _monitor.Log(
                $"Darkness: {target.Bundle.Name} slot {target.IngredientIndex} now asks for {stack} {Strings.ItemName(newItemId)} instead of {ExactName(target.ItemId, target.Flavor)} ({Run.Season} {Run.DayOfMonth}).",
                LogLevel.Info);
            _rebuildBoard("darkness tampering");
            return true;
        }
    }
}
