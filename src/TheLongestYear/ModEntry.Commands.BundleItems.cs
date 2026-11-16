using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        private const int MinAsk = 1;
        private const int MaxAsk = 99;
        private const string MoneySlot = "-1";

        /// <summary>One item a bundle can ask for, with the quantity range the sizing rules allow.</summary>
        private sealed record BundleItemRow(string Id, string Name, int Low, int High, string Earliest, bool CanAskQuality);

        /// <summary>One bundle the board can hold: its shape under this save's difficulty and every
        /// item it can ask for.</summary>
        private sealed record BundleRow(string Room, string Name, int Shown, int Needs, string Source, string Season, List<BundleItemRow> Items);

        /// <summary>tly_bundleitems: every bundle the engine can place on a board under this save's
        /// difficulty, and every item each one can ask for, read from the same candidate lists the
        /// fill draws from. No board is rolled, so the list is what is possible, not what one seed
        /// happened to give. Writes JSON for the bundle guide.</summary>
        private void CmdBundleItems(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first (the pools are derived from live game data).", LogLevel.Warn);
                return;
            }

            MetaState state = _meta.State;
            DifficultyProfile difficulty = state.BoardDifficulty(_config);
            BundleGenerationTuning tuning = DifficultyTuning.Scale(_config.PoolTuning, difficulty);
            ItemPools pools = new GameDataPools(this.Monitor)
                .Build(tuning, YearTwoCrops.ExcludedFor(state.HasUpgrade, difficulty.Steps.ItemRarity),
                    BundleEngine.VanillaOnlyIdsFor(state.ModItemsOnBoard()), LayerMarks(), LayerRandomShopPicks());
            pools = RarityBias.Apply(pools, difficulty.RarityBias, _config.RarityThresholds);

            var engine = new BundleEngine(
                this.Monitor, _config.PoolTuning, _config.EnableNonObjectDonations, _config.RarityThresholds,
                YearTwoCrops.ExcludedFor(state.HasUpgrade, difficulty.Steps.ItemRarity), difficulty, state.HasUpgrade);
            engine.Availability = _availability;
            engine.LayerMarks = LayerMarks();
            engine.LayerRandomShopPicks = LayerRandomShopPicks();
            engine.AllowModItems = state.ModItemsOnBoard();
            int seed = BundleEngineSeed.For(unchecked((ulong)Game1.player.UniqueMultiplayerID), state.EffectiveBundleSeedLoop);

            var rows = new List<BundleRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in engine.BuildCandidatePools(pools, seed).OrderBy(r => r.Key, StringComparer.Ordinal))
            foreach (IReadOnlyList<BundleSpec> position in room.Value)
            foreach (BundleSpec candidate in position)
            {
                if (!seen.Add(room.Key + "/" + candidate.Name)) continue;
                rows.Add(DescribeBundle(room.Key, candidate, pools, tuning, difficulty));
            }

            string fileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : "bundle-items.json";
            this.Helper.Data.WriteJsonFile(fileName, new
            {
                Version = this.ModManifest.Version.ToString(),
                ModelStep = _availability?.Step.ToString(),
                AskBandLow = difficulty.AskBandLow,
                AskBandHigh = difficulty.AskBandHigh,
                StackFactor = difficulty.StackFactor,
                RequireAllSlots = difficulty.RequireAllSlots,
                GoldChance = tuning.GoldQualityChance,
                SilverChance = tuning.SilverQualityChance,
                Steps = difficulty.Steps,
                difficulty.QualityFactor,
                difficulty.RarityBias,
                difficulty.RequiredSlotsDelta,
                YearTwoCropAskCap = difficulty.EffectiveYearTwoCropAskCap(),
                WeeklyGoalDiscount = difficulty.EffectiveWeeklyGoalStackDiscount(),
                difficulty.JpEarnedFactor,
                difficulty.ShrinePriceFactor,
                difficulty.StartingGold,
                difficulty.StartingCartSlots,
                difficulty.HoldPriceFactor,
                BundlesPerRoom = (Game1.netWorldState?.Value?.BundleData?.Keys ?? Enumerable.Empty<string>())
                    .GroupBy(k => k.Split('/')[0]).ToDictionary(g => g.Key, g => g.Count()),
                VaultPrices = TheLongestYear.Integration.VaultBundleMap.PriceLadder().Select(p => p.ToString()).ToList(),
                VaultGate = new[] { CoreSeason.Spring, CoreSeason.Summer, CoreSeason.Fall, CoreSeason.Winter }
                    .Select(s => VaultRules.RequiredPaid(s, TheLongestYear.Integration.VaultBundleMap.Count())).ToList(),
                LegendaryPerBundle = LegendaryFishRules.MaxPerBundle(difficulty.Steps.ItemRarity),
                CappedItemsPerBoard = CappedAsks.BoardAllowance(difficulty.Steps.StackSize),
                Bundles = rows,
            });
            this.Monitor.Log($"tly_bundleitems: {rows.Count} bundles written to {fileName} (model step {_availability?.Step}).", LogLevel.Info);
        }

        private BundleRow DescribeBundle(
            string room, BundleSpec spec, ItemPools pools, BundleGenerationTuning tuning, DifficultyProfile difficulty)
        {
            bool passThrough = BundleEngine.IsPassThroughRoom(room);
            AuthoredBundleDef authored = passThrough ? null : AuthoredBundleCatalog.All.FirstOrDefault(d => d.Name == spec.Name);
            DomainMatch match = PoolDomainClassifier.Classify(spec, pools);
            PoolRecipe recipe = !passThrough && authored == null && match.Domain == PoolDomain.Recipe
                ? BundleSlotFiller.RecipeFor(spec, pools, _availability)
                : null;

            int templateShown = spec.PickCount > 0 ? Math.Min(spec.PickCount, spec.Slots.Count) : spec.Slots.Count;
            (int shown, int needs) = (templateShown, Math.Min(spec.NumberOfSlots, templateShown));
            string source;
            IReadOnlyList<PoolItem> candidates = null;
            if (passThrough)
                source = "vanilla";
            else if (authored != null)
            {
                (shown, needs) = AuthoredBundleComposer.ShapeOf(authored, tuning);
                candidates = AuthoredBundleComposer.PossibleItems(authored, pools, _config.EnableNonObjectDonations, _availability);
                source = "authored";
            }
            else if (match.Domain == PoolDomain.None || recipe?.IsVanillaOnly == true)
                source = "own items";
            else
            {
                if (BundleShapes.For(spec.Name) is (int Shown, int Needs) shape)
                    (shown, needs) = (shape.Shown, Math.Min(shape.Needs, shape.Shown));
                candidates = BundleSlotFiller.PossibleItems(spec, match, pools, _availability, recipe);
                source = recipe != null ? string.Join(", ", recipe.Parts.Select(p => p.Label)) : match.Domain.ToString();
            }
            if (!passThrough)
                needs = difficulty.RequireAllSlots ? shown : Math.Clamp(needs + difficulty.RequiredSlotsDelta, 1, shown);

            CoreSeason? season = match.Season;
            var items = new List<BundleItemRow>();
            if (candidates != null)
            {
                foreach (PoolItem item in candidates.GroupBy(p => p.ItemId, StringComparer.Ordinal).Select(g => g.First()))
                    items.Add(DescribeItem(item.ItemId, MinAsk, season, pools, difficulty));
            }
            else
            {
                foreach (BundleSlotSpec slot in spec.Slots)
                    items.Add(slot.ItemId == MoneySlot
                        ? new BundleItemRow(MoneySlot, $"{slot.Stack:N0}g", slot.Stack, slot.Stack, "", false)
                        : DescribeItem(slot.ItemId, slot.Stack, season, pools, passThrough ? null : difficulty));
            }
            return new BundleRow(room, spec.Name, shown, needs, source, season?.ToString() ?? "",
                items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList());
        }

        /// <summary>The ask range for one item: the difficulty's band of its supply basis when the
        /// item has one (from the earliest deadline it can get to the whole year), otherwise its
        /// base stack times the Stack Size factor; then the same clamps the board applies. A null
        /// <paramref name="difficulty"/> is a bundle kept exactly as vanilla wrote it.</summary>
        private BundleItemRow DescribeItem(string rawId, int baseStack, CoreSeason? bundleSeason, ItemPools pools, DifficultyProfile difficulty)
        {
            string name = DisplayName(rawId);
            if (BundleParsing.IsCategoryRef(rawId) || difficulty == null)
            {
                int kept = difficulty == null ? baseStack : StackScaling.ScaleStack(baseStack, difficulty.StackFactor);
                return new BundleItemRow(rawId, name, kept, kept, "", false);
            }

            string id = BundleParsing.NormalizeItemId(rawId);
            bool placed = _availability != null && _availability.IsPlaced(id);
            CoreSeason? gate = placed ? _availability.For(id).Gate : (CoreSeason?)null;
            int low, high;
            if (LegendaryFishRules.IsLegendary(id))
                (low, high) = (MinAsk, MinAsk);
            else
            {
                CoreSeason? early = bundleSeason ?? gate;
                CoreSeason? late = bundleSeason;   // a season bundle is sized for its season; any other for the year
                double? highBasis = QuantityAskPass.Covers(id, _availability) ? QuantityAskPass.BasisByDeadline(id, late, _availability) : null;
                double? lowBasis = highBasis == null ? null : QuantityAskPass.BasisByDeadline(id, early, _availability) ?? highBasis;
                if (highBasis != null)
                {
                    low = BandEdge(Math.Min(lowBasis.Value, highBasis.Value), difficulty.AskBandLow);
                    high = Math.Max(low, BandEdge(highBasis.Value, Math.Min(difficulty.AskBandHigh, AskBands.Ceiling)));
                }
                else
                    low = high = StackScaling.ScaleStack(baseStack, difficulty.StackFactor);
            }

            int Clamp(int stack) => Math.Clamp(
                CappedAsks.ClampStack(id, UnstackableAsks.ClampStack(id, OncePerLoopAsks.ClampStack(id,
                    YearTwoCrops.ClampAsk(id, stack, difficulty.EffectiveYearTwoCropAskCap(), _meta.State.HasUpgrade, _availability),
                    difficulty.OncePerLoopAsksOne))),
                MinAsk, MaxAsk);
            bool quality = pools.QualityEligibleIds != null && pools.QualityEligibleIds.Contains(id);
            return new BundleItemRow(id, name, Clamp(low), Clamp(high), gate?.ToString() ?? "", quality);
        }

        /// <summary>One edge of <see cref="AskBands.Roll"/>: the basis rounded to a whole number,
        /// times the band fraction, rounded up, never below one.</summary>
        private static int BandEdge(double basis, double fraction)
        {
            int whole = Math.Max(1, (int)Math.Round(basis, MidpointRounding.AwayFromZero));
            return Math.Max(1, (int)Math.Ceiling(whole * fraction));
        }
    }
}
