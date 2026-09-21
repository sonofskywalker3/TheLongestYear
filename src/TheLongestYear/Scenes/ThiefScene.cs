using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The chest blight scene (spec 2026-09-21). A stub until Task 8 writes the thief: a
    /// short hold under the night's black, with the take landing at its own beat.</summary>
    internal sealed class ThiefScene : StrikeSceneBase
    {
        private const int StrikeAtMs = 1500;
        private const int SceneEndMs = 2500;

        public ThiefScene(PendingStrike strike, bool skippable, IMonitor monitor, Action onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        /// <summary>Where the thief is staged: the night's chest, else the first machine it takes,
        /// but only on a map the scene can actually show. Null when nothing tonight can be filmed,
        /// and then the take lands with no scene.</summary>
        internal static SpoilagePass.Hit SceneTargetOnFarm(PendingStrike strike)
        {
            SpoilagePass.Hit hit = strike?.SceneTarget;
            if (hit?.Location == null) return null;
            bool onFarm = hit.Location is Farm
                       || hit.Location is FarmHouse
                       || hit.Location is Cellar
                       || hit.Location is Shed;
            return onFarm ? hit : null;
        }

        protected override bool Stage() => SceneTargetOnFarm(Strike) != null;

        protected override void Build(Timeline t)
        {
            t.At(StrikeAtMs, ApplyStrike);
            t.EndAt(SceneEndMs);
        }
    }
}
