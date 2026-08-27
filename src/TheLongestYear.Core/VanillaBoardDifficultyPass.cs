using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Gives a vanilla-generated Community Center board the stack, quality, and required-slot
/// modifiers, so those three mean the same thing whether the board came from TLY's own engine or
/// from the game's Standard / Remixed generator (or another bundle mod's).
///
/// THE ONE INVARIANT: this pass never changes which item a slot asks for. Vanilla authored the
/// bundle; the pass only adjusts how much, what quality, and how many of the shown slots count.
/// That is why the item-rarity modifier has no vanilla equivalent, and why it is excluded from
/// <see cref="DifficultySettings.AsksAllNormal"/>.
///
/// The pass is a pure string-dictionary transform so it is testable without the game. It rebuilds
/// each value FIELD BY FIELD rather than round-tripping through <see cref="BundleSpec"/>, because
/// a round trip would discard the sprite field (index 5) that
/// <see cref="BundleDataWriter"/> deliberately writes empty.
///
/// Determinism: keys are walked in ordinal order and each bundle gets its own RNG stream salted on
/// its key, so the result depends only on the seed, never on dictionary iteration order. A replayed
/// reset therefore reproduces the same board, which is what the anti-save-scum guarantee rests on.
///
/// Spec 2026-08-26 difficulty-modifiers, section 4.</summary>
public static class VanillaBoardDifficultyPass
{
    private const string MoneySlotId = "-1";
    private const string VaultRoom = "Vault";

    private const int QualityNone = 0;
    private const int QualitySilver = 1;
    private const int QualityGold = 2;

    /// <summary>Per-bundle RNG salt, mirroring BundleEngine's own SlotSaltPrime idiom.</summary>
    private const int BundleSaltPrime = 6151;

    /// <summary>Field indices of the slash-delimited Data/Bundles value. See
    /// <see cref="BundleParsing"/>: name / reward / ingredients / color / numberOfSlots / sprite /
    /// displayName.</summary>
    private const int IngredientsField = 2;
    private const int NumberOfSlotsField = 4;
    private const int MinimumFieldCount = 5;

    /// <summary>Applies the three vanilla-capable ask-side modifiers. Returns the SAME reference
    /// when every one of them is Normal, so the default Vanilla path keeps its current
    /// zero-write behaviour exactly.</summary>
    /// <param name="bundleData">Live Data/Bundles entries, keyed "Room/index".</param>
    /// <param name="profile">The resolved difficulty profile for this loop.</param>
    /// <param name="tuning">Supplies the base silver/gold chances and the config-extensible
    /// quality-ineligible list.</param>
    /// <param name="seed">The loop's bundle seed, so a replayed reset is reproducible.</param>
    /// <param name="qualityEligibleIds">Ids the game itself gives quality stars to. Null means no
    /// eligibility data is available, in which case only the built-in never-quality set and the
    /// config list guard the roll, matching <see cref="BundleSlotFiller"/>'s own fallback.</param>
    public static IDictionary<string, string> Apply(
        IDictionary<string, string> bundleData,
        DifficultyProfile profile,
        BundleGenerationTuning tuning,
        int seed,
        IReadOnlySet<string>? qualityEligibleIds = null)
    {
        if (bundleData == null) throw new ArgumentNullException(nameof(bundleData));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (tuning == null) throw new ArgumentNullException(nameof(tuning));

        if (profile.Steps.AsksAllNormal())
            return bundleData;

        // Ordinal key order, not dictionary order: the RNG stream must not depend on how the
        // caller's dictionary happens to enumerate.
        var result = new Dictionary<string, string>(bundleData.Count, StringComparer.Ordinal);
        foreach (string key in bundleData.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var rng = new Random(unchecked(seed + StableHash(key) * BundleSaltPrime));
            result[key] = ApplyToBundle(key, bundleData[key], profile, tuning, rng, qualityEligibleIds);
        }
        return result;
    }

    private static string ApplyToBundle(
        string key, string value, DifficultyProfile profile,
        BundleGenerationTuning tuning, Random rng, IReadOnlySet<string>? qualityEligibleIds)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        string[] fields = value.Split('/');
        if (fields.Length < MinimumFieldCount)
            return value;   // not a shape we understand; leave it exactly as found

        IReadOnlyList<BundleIngredient> ingredients =
            BundleParsing.ParseIngredients(fields[IngredientsField]);
        if (ingredients.Count == 0)
            return value;

        bool isMoneyBundle = key.StartsWith(VaultRoom + "/", StringComparison.OrdinalIgnoreCase)
                             || ingredients.Any(i => i.ItemRef == MoneySlotId);
        if (isMoneyBundle)
            return value;   // a money ask is a sum, not a difficulty dial

        fields[IngredientsField] = string.Join(" ", ingredients.Select(ing =>
        {
            int stack = StackScaling.ScaleStack(ing.Stack, profile.StackFactor);
            int quality = RollQuality(ing, profile, tuning, rng, qualityEligibleIds);
            return $"{ing.ItemRef} {stack} {quality}";
        }));

        if (int.TryParse(fields[NumberOfSlotsField], out int required))
            fields[NumberOfSlotsField] =
                AdjustRequired(required, ingredients.Count, profile).ToString();

        return string.Join("/", fields);
    }

    /// <summary>Relative to what vanilla authored, so Normal is a genuine no-op: Hard and Extreme
    /// ADD stars to currently-plain slots at the excess above 1.0, and Easy STRIPS existing stars.
    /// Eligibility is never overridden at any step.</summary>
    private static int RollQuality(
        BundleIngredient ingredient, DifficultyProfile profile,
        BundleGenerationTuning tuning, Random rng, IReadOnlySet<string>? qualityEligibleIds)
    {
        double factor = profile.QualityFactor;
        if (factor == 1.0)
            return ingredient.Quality;

        if (factor < 1.0)
        {
            // Stripping a star is always legal: a plainer ask can never be impossible.
            if (ingredient.Quality == QualityNone)
                return QualityNone;
            return rng.NextDouble() < 1.0 - factor ? QualityNone : ingredient.Quality;
        }

        if (ingredient.Quality != QualityNone)
            return ingredient.Quality;   // already starred; the modifier only adds where there is none
        if (!CanCarryQuality(ingredient, tuning, qualityEligibleIds))
            return QualityNone;

        double excess = factor - 1.0;
        if (rng.NextDouble() < tuning.GoldQualityChance * excess) return QualityGold;
        if (rng.NextDouble() < tuning.SilverQualityChance * excess) return QualitySilver;
        return QualityNone;
    }

    /// <summary>Mirrors <see cref="BundleSlotFiller.RollQuality"/>'s vetting exactly. A category
    /// reference ("-5" = any animal product) can never carry a star, because the donated item is
    /// not known until the player picks it.</summary>
    private static bool CanCarryQuality(
        BundleIngredient ingredient, BundleGenerationTuning tuning, IReadOnlySet<string>? qualityEligibleIds)
    {
        if (BundleParsing.IsCategoryRef(ingredient.ItemRef))
            return false;

        string id = BundleParsing.NormalizeItemId(ingredient.ItemRef);
        if (BundleSlotFiller.BuiltInQualityIneligibleItemIds.Contains(id)
            || tuning.QualityIneligibleItemIds.Contains(id))
            return false;

        return qualityEligibleIds == null || qualityEligibleIds.Contains(id);
    }

    /// <summary>Clamped to <c>[1, max(shown, current)]</c>. The upper bound keeps the existing
    /// value when odd save data already requires more slots than it shows, so Hard can never
    /// silently REDUCE a requirement.</summary>
    private static int AdjustRequired(int current, int shown, DifficultyProfile profile)
    {
        if (profile.RequireAllSlots)
            return Math.Max(1, shown);
        if (profile.RequiredSlotsDelta == 0)
            return current;

        int upper = Math.Max(shown, current);
        return Math.Clamp(current + profile.RequiredSlotsDelta, 1, upper);
    }

    /// <summary>Ordinal char-walk hash. <see cref="string.GetHashCode()"/> is randomised per
    /// process in .NET Core, which would make a replayed reset produce a different board.</summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in text)
                hash = hash * 31 + c;
            return hash;
        }
    }
}
