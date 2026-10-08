using System;
using StardewModdingAPI;
using StardewValley.Events;
using TheLongestYear.Core.Sabotage;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>Which strike gets which scene (spec 2026-09-21), and whether tonight's strike has
    /// anything for its scene to play against. Asked at pick time: a strike whose scene is due but
    /// cannot stage or cannot show its pick lands now without it (Jeff, 2026-10-08).</summary>
    internal static class StrikeSceneFactory
    {
        /// <summary>Can tonight's strike be filmed at all? Asked at pick time, before the slot.</summary>
        public static bool CanPlay(PendingStrike strike)
        {
            if (strike == null || SceneBreakSwitch.BreaksAtPick(strike.Event)) return false;
            return strike.Event switch
            {
                DarknessEvent.CropBlight => strike.CropTiles.Count > 0,
                DarknessEvent.ChestBlight => ThiefScene.SceneTargetOnFarm(strike) != null,
                DarknessEvent.Reversion => true,
                DarknessEvent.Tampering => true,
                _ => false,
            };
        }

        /// <summary>Can this kind's scene stage tonight at all? Asked at the pick while the kind's
        /// scene is due: false lands the strike bare. It never stops the kind from acting. The
        /// thief's own check is per chest, inside the draw (SpoilagePass).</summary>
        public static bool CanStage(DarknessEvent e) => !SceneBreakSwitch.BreaksAtPick(e) && e switch
        {
            DarknessEvent.CropBlight => CrowsScene.CanStage(),
            DarknessEvent.ChestBlight => true,
            DarknessEvent.Reversion => HallScene.CanStage(),
            DarknessEvent.Tampering => CloudScene.CanStage(),
            _ => false,
        };

        /// <summary>Tonight's scene, or null when the kind has none.</summary>
        public static FarmEvent Create(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
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
