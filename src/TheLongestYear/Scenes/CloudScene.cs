using System;
using StardewModdingAPI;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The tampering scene (spec 2026-09-21). A stub until Task 10 writes the cloud over
    /// the board: a short hold under the night's black, with the rewrite landing at its own beat.</summary>
    internal sealed class CloudScene : StrikeSceneBase
    {
        private const int StrikeAtMs = 1500;
        private const int SceneEndMs = 2500;

        public CloudScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        protected override bool Stage() => true;

        protected override void Build(Timeline t)
        {
            t.At(StrikeAtMs, ApplyStrike);
            t.EndAt(SceneEndMs);
        }
    }
}
