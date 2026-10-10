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
        /// <summary>Reset phase (step 11a): the new loop's board, engine-written or vanilla, held or new.</summary>
        private void WriteBoard(ResetContext ctx)
        {
            // 11a. Regenerate the owned-bundle set for the new loop (Task 6: engine wiring) and
            // expose its requirement manifest via LastGeneratedRequirements so FinalizeReset can
            // re-inject it (RunController.ReplaceRequirements) before the hub samples goal slots.
            //
            // Seed basis: NOT Game1.uniqueIDForThisGame. Decompile-verified
            // (Utility.NewUniqueIdForThisGame, StardewValley/Utility.cs): it's
            // `(ulong)(DateTime.UtcNow - epoch).TotalSeconds` -- wall-clock, not deterministic --
            // and step 0 above just reseeded it to a fresh value for THIS loop. Two problems rule
            // it out entirely, not just the obvious one:
            //   (a) using the value captured HERE (post-reseed) would make a replayed reset
            //       reroll: ForceFullSave (called by FinalizeReset right after this method
            //       returns) explicitly documents a skip-and-defer-to-next-sleep path when an
            //       event/minigame is active, so "generate+write happened but the save hasn't
            //       landed on disk yet" is a real, reachable window, not just a theoretical
            //       process-kill race. A reload from that window followed by re-triggering the
            //       reset would draw a NEW wall-clock reseed and a DIFFERENT bundle set --
            //       exactly the reroll the engine exists to prevent.
            //   (b) using a PRE-reset capture (the id from BEFORE step 0's reseed) fixes (a) but
            //       breaks the far more common case: this id is only ever this loop's OLD id --
            //       every ordinary subsequent reload of the NEW save (the whole rest of this
            //       loop's playtime) sees the POST-reseed id instead, so ModEntry's SaveLoaded
            //       engine-mode branch could never reproduce this exact generation and would
            //       permanently fall back to the legacy read path for the entire loop.
            // Game1.player.UniqueMultiplayerID has neither problem: it's assigned once at farmer
            // creation, is part of the Farmer's own persisted save data, and loadForNewGame
            // above only resets the EXISTING persistent Farmer's stats/inventory -- it never
            // reassigns this id (decompile-verified, Game1.loadForNewGame). So it is IDENTICAL
            // across a replayed reset (satisfies the anti-scum guarantee) AND identical on every
            // later reload of this loop's save (satisfies SaveLoaded's manifest-first re-derivation) --
            // the one value that is simultaneously stable across both.
            // A reset that skipped the Fail-night hold choice (console tly_reset, post-win new loop)
            // must behave like a reshuffle; BundleHold.ConsumeChoiceAtReset owns that rule.
            TheLongestYear.Core.BundleHold.ConsumeChoiceAtReset(_meta);

            // Randomizer "Random bundle rewards" is board-level: stamp it only when this reset builds
            // a NEW board. A held board (vanilla snapshot restored, or the Engine re-deriving off the
            // pinned seed loop; ConsecutiveHolds > 0 only after a Kept choice) keeps the stamp it was
            // built under, so toggling the option never changes a board the player paid to keep.
            bool holdingBoard = ctx.VanillaBoard ? ctx.HeldVanillaBoard != null : _meta.ConsecutiveHolds > 0;
            if (!holdingBoard)
                _meta.RandomBundleRewardsBoard = _config.Randomizer?.RandomBundleRewards ?? false;
            // Bundle-count dial (Jeff, 2026-10-09): a held board keeps its count.
            TheLongestYear.Core.BundleCountStamp.ForReset(_meta.Difficulty, ctx.PreviousBundleCount, holdingBoard);

            if (ctx.VanillaBoard)
            {
                // Vanilla mode: the board loadForNewGame just wrote IS the board. No engine write,
                // no manifest marker; the post-reset reload classifies it (read-and-classify).
                _meta.BundlesGeneratedForReset = -1;
                _meta.WrittenBoard = null;
                _meta.WrittenBoardSeasonPins = null;
                _meta.WrittenBoardFlavors = null;
                LastGeneratedRequirements = null;
                // Normal or Remixed with Tech's Cross-Mod Bundles: roll a fresh Tech board in place
                // of the old one, before the difficulty and reward passes below run over it (spec
                // 2026-10-08 addendum 2). Skipped for a held board and TLY Custom; a failure logs
                // one warning and keeps the game's own board. The post-reset
                // reload classifies and fingerprints whatever is live, so this board is the
                // expected one for the loop.
                TheLongestYear.Core.TechBundlesReroll.Run(
                    TechBundles, ctx.BoardSource, restoringHeldBoard: ctx.HeldVanillaBoard != null,
                    message => _monitor.Log(message, LogLevel.Info),
                    message => _monitor.Log(message, LogLevel.Warn));
                if (ctx.HeldVanillaBoard != null)
                {
                    // The held board already carries whatever difficulty adjustments it was built
                    // with, so the ask-side pass must NOT run over it again and compound them.
                    Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string>(ctx.HeldVanillaBoard));
                    _monitor.Log(
                        $"Reset: restored the held vanilla board ({ctx.HeldVanillaBoard.Count} bundles); no re-roll, no difficulty re-pass.",
                        LogLevel.Info);
                }
                else
                {
                    _monitor.Log("Reset: vanilla board — keeping the game's own board (no engine write).", LogLevel.Info);
                    ApplyVanillaBoardDifficulty();
                    ApplyVanillaBoardRewardShuffle();
                }
                // With Tech's Cross-Mod Bundles loaded, store the board this reset just wrote: Tech's
                // mod writes its own saved board over it on every load, and the load puts this one
                // back (spec 2026-10-08 addendum 3). Without Tech it stays null, as before.
                _meta.WrittenBoard = TheLongestYear.Core.TechBoardOfRecord.VanillaBoardToStore(
                    TechBundles?.IsLoaded ?? false, Game1.netWorldState.Value.BundleData);
                if (_meta.WrittenBoard != null)
                    _monitor.Log(
                        $"Reset: stored this loop's board ({_meta.WrittenBoard.Count} bundles) as the board of record, since Tech's Cross-Mod Bundles rewrites the board on every load.",
                        LogLevel.Info);
            }
            else
            {
                // Stack size and quality asks arrive as a SCALED TUNING BLOCK rather than as
                // engine changes: BundleSlotFiller already reads every stack number and quality
                // chance off this object, so scaling it applies both modifiers with no edit to
                // generation. Scale returns the same instance at Normal.
                var difficultyTuning = TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, _meta.Difficulty);
                var engine = new BundleEngine(_monitor, difficultyTuning, _config.EnableNonObjectDonations, _config.RarityThresholds,
                    TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.HasUpgrade, _meta.Difficulty.Steps.ItemRarity), _meta.Difficulty);
                engine.Availability = AvailabilityModel;
                // "Allow mod items in custom bundles" (spec 2026-10-08 addendum 1): a new board takes
                // the save's choice, a held board keeps the value it was built under. Stamped with the
                // board so the load-time check re-derives it the same way after a mid-loop toggle.
                _meta.BoardAllowsModItems = TheLongestYear.Core.CustomBoardModItems.ForReset(
                    holdingBoard, _meta.BoardAllowsModItems, _meta.AllowModItemsInCustomBundles);
                engine.AllowModItems = _meta.BoardAllowsModItems.Value;
                // Keep-bundles hold (spec 2026-08-24): the seed loop is EffectiveBundleSeedLoop, which
                // RunController's Fail-night choice already pinned (hold) or advanced to this loop
                // (reshuffle) before we got here. Legacy saves resolve to CompletedResets.
                int seed = BundleEngineSeed.For(unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);
                GeneratedBundleSet generatedSet = engine.Generate(seed, _meta.RandomBundleRewardsBoard);
                engine.WriteToWorld(generatedSet, _monitor);
                // Persist exactly what was written (and the derived pins it was classified under)
                // so later loads verify the live board against this instead of re-deriving from
                // the seed; see MetaState.WrittenBoard.
                _meta.WrittenBoard = new Dictionary<string, string>(generatedSet.ToBundleData());
                _meta.WrittenBoardSeasonPins = TheLongestYear.Core.BoardRequirements.PinsToStored(engine.LastDerivedSeasonPins);
                // The fruit/mushroom/fish each flavored slot names. Stamped with the board it
                // belongs to, so a board written before 0.18.33 keeps a null map and no flavors.
                _meta.WrittenBoardFlavors = new Dictionary<string, string>(generatedSet.Flavors);
                _monitor.Log(
                    $"Reset: bundle seed loop {_meta.EffectiveBundleSeedLoop} (CompletedResets {_meta.CompletedResets}, consecutive holds {_meta.ConsecutiveHolds}).",
                    LogLevel.Info);
                _meta.BundlesGeneratedForReset = _meta.CompletedResets;
                LastGeneratedRequirements = engine.BuildRequirements(
                    generatedSet, _itemSeasonPins, _bundleQuotas, AvailabilityModel);
            }
        }
    }
}
