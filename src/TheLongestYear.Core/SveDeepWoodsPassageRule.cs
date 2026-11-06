using System;

namespace TheLongestYear.Core;

/// <summary>When The Longest Year applies Stardew Valley Expanded's Deep Woods map patch itself: both
/// mods are installed and SVE's own condition for the patch (the mod <see cref="OldDeepWoodsId"/>)
/// does not hold.</summary>
public static class SveDeepWoodsPassageRule
{
    public const string DeepWoodsId = "maxmakesmods.deepwoodsmod";
    /// <summary>Deep Woods' id under its first author, the one SVE's patch still asks for.</summary>
    public const string OldDeepWoodsId = "maxvollmer.deepwoodsmod";

    public static bool Applies(Func<string, bool> isLoaded)
    {
        if (isLoaded is null) throw new ArgumentNullException(nameof(isLoaded));
        return isLoaded(SveUnlocks.ModId) && isLoaded(DeepWoodsId) && !isLoaded(OldDeepWoodsId);
    }
}
