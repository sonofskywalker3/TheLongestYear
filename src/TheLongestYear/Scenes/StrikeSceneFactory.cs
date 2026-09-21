using System;
using StardewModdingAPI;
using StardewValley.Events;
using TheLongestYear.Core.Sabotage;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>Which strike gets which scene (spec 2026-09-21), and whether tonight's strike has
    /// anything for its scene to play against. Asked twice: once at pick time, so a strike with no
    /// scene lands at once, and once when the overnight slot comes round.</summary>
    internal static class StrikeSceneFactory
    {
        /// <summary>Can tonight's strike be filmed at all? Asked at pick time, before the slot.</summary>
        public static bool CanPlay(PendingStrike strike)
        {
            if (strike == null) return false;
            return strike.Event switch
            {
                DarknessEvent.CropBlight => strike.CropTiles.Count > 0,
                DarknessEvent.ChestBlight => ThiefScene.SceneTargetOnFarm(strike) != null,
                DarknessEvent.Reversion => true,
                DarknessEvent.Tampering => true,
                _ => false,
            };
        }

        /// <summary>Tonight's scene, or null when the kind has none.</summary>
        public static FarmEvent Create(PendingStrike strike, bool skippable, IMonitor monitor, Action onFinished)
        {
            if (strike == null) return null;
            return strike.Event switch
            {
                DarknessEvent.CropBlight => new CrowsScene(strike, skippable, monitor, onFinished),
                DarknessEvent.ChestBlight => new ThiefScene(strike, skippable, monitor, onFinished),
                DarknessEvent.Reversion => new HallScene(strike, skippable, monitor, onFinished),
                DarknessEvent.Tampering => new CloudScene(strike, skippable, monitor, onFinished),
                _ => null,
            };
        }
    }
}
