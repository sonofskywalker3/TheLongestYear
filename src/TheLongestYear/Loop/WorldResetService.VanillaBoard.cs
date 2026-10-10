using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.UI;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    internal sealed partial class WorldResetService
    {
        /// <summary>Gives a vanilla-generated board the three ask-side modifiers it can honour:
        /// stack size, quality asks, and required slots. Never changes which item a slot asks for,
        /// so a Standard or Remixed board keeps its identity and only its numbers move.
        ///
        /// When those three are all Normal the dials are skipped, and only a capped ask above one
        /// (Prismatic Shard, Mystery Box: Remixed's Helper's "5 Mystery Box") is lowered to one.
        /// A board without one gets zero writes and no extra log line, as before.
        ///
        /// Seeded from the same basis as the Engine path, so a replayed reset reproduces the same
        /// board and the anti-save-scum guarantee still holds.</summary>
        private void ApplyVanillaBoardDifficulty()
        {
            TheLongestYear.Core.DifficultyProfile difficulty = _meta.Difficulty;
            if (difficulty == null || difficulty.Steps.AsksAllNormal())
            {
                ClampVanillaCappedAsks();
                return;
            }

            Dictionary<string, string> live = Game1.netWorldState.Value.BundleData;
            if (live == null || live.Count == 0)
            {
                _monitor.Log(
                    "Reset: Vanilla difficulty pass skipped — no bundle data to adjust.",
                    LogLevel.Warn);
                return;
            }

            // Quality eligibility is derived from the game's own data (crop harvests, rod-caught
            // non-jelly fish, spawned forage), and it is what stops a gold star landing on Fiber
            // or on algae (Nexus 1122358). Only built when the quality modifier is actually above
            // Normal, because deriving the pools reads several data assets.
            IReadOnlySet<string> qualityEligibleIds = null;
            if (difficulty.QualityFactor > 1.0)
            {
                try
                {
                    qualityEligibleIds = new GameDataPools(_monitor)
                        .Build(_config.PoolTuning, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.HasUpgrade, difficulty.Steps.ItemRarity))
                        .QualityEligibleIds;
                }
                catch (Exception ex)
                {
                    // Without the derived set the built-in never-quality list still guards the
                    // known-impossible items, so degrade rather than abandon the reset.
                    _monitor.Log(
                        $"Reset: could not derive quality eligibility for the Vanilla difficulty pass ({ex.Message}); " +
                        "falling back to the built-in ineligible list only.",
                        LogLevel.Warn);
                }
            }

            int seed = BundleEngineSeed.For(
                unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);

            IDictionary<string, string> adjusted = TheLongestYear.Core.VanillaBoardDifficultyPass.Apply(
                new Dictionary<string, string>(live), difficulty, _config.PoolTuning, seed, qualityEligibleIds);

            Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string>(adjusted));

            _monitor.Log(
                $"Reset: Vanilla board adjusted for difficulty ({adjusted.Count} bundles; " +
                $"stacks {difficulty.Steps.StackSize}, quality {difficulty.Steps.QualityAsks}, " +
                $"required slots {difficulty.Steps.RequiredSlots}; seed {seed}). " +
                "Item ids are unchanged.",
                LogLevel.Info);
        }

        /// <summary>Randomizer "Random bundle rewards" on a freshly built Vanilla or Remixed board.
        /// Runs after the difficulty pass and never on a held board (the held snapshot already
        /// carries its rewards). Only field 1 of each bundle changes. Same seed basis as
        /// <see cref="ApplyVanillaBoardDifficulty"/> and the same reward pool the Engine draws from.</summary>
        private void ApplyVanillaBoardRewardShuffle()
        {
            if (!_meta.RandomBundleRewardsBoard)
                return;

            Dictionary<string, string> live = Game1.netWorldState.Value.BundleData;
            if (live == null || live.Count == 0)
            {
                _monitor.Log("Randomizer: bundle reward shuffle skipped, no bundle data on the board.", LogLevel.Warn);
                return;
            }

            IReadOnlyList<string> pool = BundleEngine.RewardPool(new VanillaBundlePool(_monitor).BuildRoomPools());
            int seed = BundleEngineSeed.For(
                unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);
            IDictionary<string, string> shuffled = TheLongestYear.Core.BundleRewardShuffle.ApplyToData(
                new Dictionary<string, string>(live), seed, pool, BundleEngine.IsRewardShuffleSkippedRoom);
            Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string>(shuffled));

            int bundles = shuffled.Keys.Count(k => !BundleEngine.IsRewardShuffleSkippedRoom(k.Split('/')[0]));
            _monitor.Log($"Randomizer: bundle rewards shuffled ({bundles} bundles, pool {pool.Count}).", LogLevel.Info);
        }

        /// <summary>The all-Normal half of <see cref="ApplyVanillaBoardDifficulty"/>: writes only
        /// the bundles holding a capped ask above one, lowered to one. On a vanilla board the stack
        /// clamp is the whole capped rule; the per-board count is held on engine boards only, since
        /// this path never changes an item or removes a bundle.</summary>
        private void ClampVanillaCappedAsks()
        {
            Dictionary<string, string> live = Game1.netWorldState.Value.BundleData;
            if (live == null || live.Count == 0)
                return;

            var updates = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in live.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                string clamped = TheLongestYear.Core.CappedAsks.RepairBundleValue(live[key]);
                if (clamped != null)
                    updates[key] = clamped;
            }
            if (updates.Count == 0)
                return;

            Game1.netWorldState.Value.SetBundleData(updates);
            _monitor.Log(
                $"Reset: Vanilla board asks for one of each capped item per slot; lowered {updates.Count} bundle(s): " +
                string.Join(", ", updates.Keys) + ".",
                LogLevel.Info);
        }
    }
}
