using System;
using StardewModdingAPI;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The crop blight scene (spec 2026-09-21). A stub until Task 7 writes the crows: a
    /// short hold under the night's black, with the blight landing at its own beat.</summary>
    internal sealed class CrowsScene : StrikeSceneBase
    {
        private const int StrikeAtMs = 1500;
        private const int SceneEndMs = 2500;

        public CrowsScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        protected override bool Stage() => true;

        protected override void Build(Timeline t)
        {
            t.At(StrikeAtMs, ApplyStrike);
            t.EndAt(SceneEndMs);
        }
    }
}
