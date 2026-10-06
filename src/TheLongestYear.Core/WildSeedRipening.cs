namespace TheLongestYear.Core
{
    /// <summary>Vanilla turns a wild seed crop into forage only inside Crop.newDay, the night it
    /// reaches its last phase (Crop.cs:869). A growCompletely from outside that pass (the crop
    /// fairy, FairyEvent.cs:202) skips it and leaves a ripe crop that still draws a mid-growth
    /// sprite and harvests the placeholder item. sigyn2002, Nexus bug 2026-10-03.</summary>
    public static class WildSeedRipening
    {
        public static bool ShouldBecomeForage(bool isWildSeedCrop, bool dead, int currentPhase, int phaseCount)
            => isWildSeedCrop && !dead && phaseCount > 0 && currentPhase >= phaseCount - 1;
    }
}
