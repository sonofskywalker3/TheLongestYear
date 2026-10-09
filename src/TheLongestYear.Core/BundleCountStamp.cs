namespace TheLongestYear.Core;

/// <summary>A reset re-resolves the whole difficulty profile from config. The bundle-count rule
/// is the exception for a held board (Fail night "keep"): it is rebuilt from its pinned seed and
/// must come back the same shape, so it keeps the rule it was built with (Jeff, 2026-10-09).
/// Spec 2026-10-09-bundle-count-dial.</summary>
public static class BundleCountStamp
{
    public static void ForReset(DifficultyProfile fresh, BundleCountRule? previous, bool holdingBoard)
    {
        if (fresh != null && holdingBoard)
            fresh.BundleCount = previous;
    }
}
