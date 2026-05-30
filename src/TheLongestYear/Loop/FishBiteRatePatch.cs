using HarmonyLib;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Fishing bonus (fish_bite_up): "Fish bite 30% sooner" — multiplier 0.70 applied to
    /// <c>calculateTimeUntilFishingBite</c> so a normal 10-second wait becomes 7 seconds.
    /// Farming liability (fish_bite_down): "Fish bite 30% slower" — multiplier 1.30 so a 10s
    /// wait becomes 13s.
    ///
    /// Prior values 0.77 / 1.43 (an inverse pair around 1.30) were off-spec; 2026-05-28 audit
    /// caught that 0.77 is only ~23% faster, not 30%. Literal "X% sooner/slower" reading is
    /// (1 - X/100) for sooner, (1 + X/100) for slower.
    ///
    /// The fortune_rare_fish upgrade was previously wired here as an additional 25% faster
    /// bite — that doesn't match its display ("Rare fish catch chance increased by 25%").
    /// The rewire lives in <see cref="FishRareLurePatch"/> (postfixes HasCuriosityLure so
    /// the upgrade owner gets the same rare-fish-boost path as a player holding the lure).
    /// </summary>
    [HarmonyPatch(typeof(FishingRod), "calculateTimeUntilFishingBite")]
    internal static class FishBiteRatePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(ref float __result)
        {
            if (ActiveEffectsProvider.ActiveBonus("fish_bite_up"))
                __result *= 0.70f;  // 30% sooner

            if (ActiveEffectsProvider.ActiveLiability("fish_bite_down"))
                __result *= 1.30f;  // 30% slower (Farming liability)

            // Quick Bite passive accelerator (quick_bite_1..5): 5% faster per tier, max 25%.
            // Stacks multiplicatively with the theme bonus — Quick Bite V on a Fishing week
            // = 0.70 × 0.75 = ~47.5% sooner total. Intentional: the whole point of the
            // accelerator chains is to compound with the matching theme week.
            int tier = TheLongestYear.Loop.UpgradeChecker.GetTier("quick_bite", 5);
            if (tier > 0)
                __result *= (float)(1.0 - 0.05 * tier);
        }
    }

    /// <summary>
    /// Rewire of <c>fortune_rare_fish</c> upgrade from "25% faster bite" (off-spec — that's a
    /// bite-rate change, not a rarity change) to "rare fish catch chance increased" by piggy-
    /// backing on vanilla's Curiosity Lure mechanic. Postfixes <see cref="FishingRod.HasCuriosityLure"/>
    /// to return true when the upgrade is owned, so every downstream check (the +0.4 baitPotency
    /// boost on <c>GameLocation.getFish</c> and any other curiosity-lure-gated path) fires as
    /// if the player had the lure equipped.
    ///
    /// 2026-05-28 user audit: "Rare fish catch chance increased by 25%" but the wiring was 25%
    /// faster bite — confused rarity with timing. The Curiosity Lure boost is closer to a flat
    /// +40% potency than a precise 25% rarity bump, but it's the cleanest mechanism that
    /// actually targets rare fish.
    ///
    /// 2026-05-29 design decision (closes the TODO "fortune_rare_fish exact rarity rewire"
    /// item): Stardew has no abstract "rare fish" concept — rarity lives inside per-spawn
    /// <c>SpawnFishData.GetChance(hasCuriosityLure, …)</c> thresholds (GameLocation.cs:13797).
    /// The Curiosity Lure piggyback IS vanilla's canonical "increase rare fish odds" pathway;
    /// any "exact 25%" rewire would require reimplementing the entire spawn table from scratch
    /// (the spawn data ships as game-content JSON keyed off precedence + chance modifiers).
    /// Treating the Curiosity Lure piggyback as the canonical implementation, not an
    /// approximation pending later replacement.
    /// </summary>
    [HarmonyPatch(typeof(FishingRod), nameof(FishingRod.HasCuriosityLure))]
    internal static class FishRareLurePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(ref bool __result)
        {
            if (__result) return;  // already true — vanilla path is satisfied
            if (UpgradeChecker.HasUpgrade != null && UpgradeChecker.HasUpgrade("fortune_rare_fish"))
                __result = true;
        }
    }
}
