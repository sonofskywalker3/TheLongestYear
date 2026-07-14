namespace TheLongestYear.Core;

/// <summary>Which source serves bundle requirements at SaveLoaded (spec 2026-07-14 engine).</summary>
public enum RequirementsSource { EngineManifest, GenerateFreshRun, LegacyReadAndClassify }

public static class EngineModeDecider
{
    /// <summary>marker = MetaState.BundlesGeneratedForReset; ccTouched = any live CC slot
    /// already donated. Engine mode when the live set was generated for THIS loop; fresh
    /// generation only on a pristine first run; everything else (pre-engine saves mid-loop)
    /// keeps the legacy path until their next reset (spec migration rule).</summary>
    public static RequirementsSource Decide(int marker, int completedResets, bool ccTouched)
    {
        if (marker == completedResets) return RequirementsSource.EngineManifest;
        if (completedResets == 0 && marker == -1 && !ccTouched) return RequirementsSource.GenerateFreshRun;
        return RequirementsSource.LegacyReadAndClassify;
    }
}
