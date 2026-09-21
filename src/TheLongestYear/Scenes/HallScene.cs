using System;
using StardewModdingAPI;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The reversion scene (spec 2026-09-21). A stub until Task 9 writes the Community
    /// Center hall: a short hold under the night's black, with the slot opening at its own beat.</summary>
    internal sealed class HallScene : StrikeSceneBase
    {
        private const int StrikeAtMs = 1500;
        private const int SceneEndMs = 2500;

        public HallScene(PendingStrike strike, bool skippable, IMonitor monitor, Action onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        protected override bool Stage() => true;

        protected override void Build(Timeline t)
        {
            t.At(StrikeAtMs, ApplyStrike);
            t.EndAt(SceneEndMs);
        }
    }
}
