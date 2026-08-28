using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core;

/// <summary>
/// Classifies a parsed vanilla bundle into one of the three <see cref="BundleKind"/>s and
/// returns a populated <see cref="BundleRequirement"/>. Pure (no Game1/SMAPI deps) so it can
/// be unit-tested with synthetic <see cref="ParsedBundle"/>s.
///
/// Decision order (the first match wins):
///   1. Name matches "(Spring|Summer|Fall|Winter) (Foraging|Crops)" → <see cref="BundleKind.Seasonal"/>.
///   2. Name has an entry in <paramref name="bundleQuotas"/> AND X &lt; Y after dedup
///      → <see cref="BundleKind.Percentage"/>. (X &gt;= Y is structurally impossible for a
///      Percentage quota — fall through to PerItem.)
///   3. X &gt;= Y after dedup (every distinct ingredient must be donated)
///      → <see cref="BundleKind.PerItem"/>. Vanilla Construction lists Wood twice (X=4, Y=3
///      deduped); the set-based donation ledger doesn't differentiate stack counts, so
///      donating Wood once satisfies all wood slots.
///   4. Otherwise (X &lt; Y, no quota) → <see cref="BundleKind.Percentage"/> with a DERIVED
///      quota (<see cref="DerivedDefaultQuota"/>). Remixed saves — the RECOMMENDED config —
///      generate pick-X-of-Y bundles with names outside the quota table (Rare Crops,
///      Brewer's, Wild Medicine, Treasure Hunter's, Children's, Winter Star, ...); before
///      2026-07-09 these returned null and were silently dropped from season checkpoints,
///      the win gate, and weekly-theme pools (khauser13's premature win + blank themes).
///      Only category-only bundles return null now.
///
/// Ingredients are normalized via <see cref="BundleParsing.NormalizeItemId"/> and de-duplicated
/// to match the donation-ledger model (one set entry per qualified id).
/// </summary>
public static class BundleClassifier
{
    private static readonly Regex SeasonalNamePattern = new Regex(
        @"^(?<season>Spring|Summer|Fall|Winter)\s+(?<kind>Foraging|Crops)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Moves a curated quota ramp so its endpoint matches how many slots the bundle now
    /// requires, then clamps every entry into [0, X] and restores monotonicity.
    ///
    /// Why: the required-slots difficulty modifier changes X, and a ramp authored against the old
    /// X no longer says what it meant. Animal is curated [1,3,5,5] at X=5; at X=6 the same intent
    /// is [2,4,6,6] (Jeff's own example, 2026-08-27). Leaving the ramp alone would have made a
    /// HARDER bundle demand a SMALLER fraction of itself at every checkpoint.
    ///
    /// The shift is derived from the ramp itself rather than from the difficulty profile, so it is
    /// self-correcting for any reason X differs from what the table assumed, including SVE-edited
    /// save data.
    ///
    /// A ramp whose endpoint is zero is left alone: those are the deliberately never-gated
    /// bundles, and shifting them would invent a Spring demand out of nothing.</summary>
    public static int[] ShiftRampToSlotCount(int[] ramp, int numberOfSlots)
    {
        int last = ramp.Length - 1;
        int shift = ramp[last] > 0 ? numberOfSlots - ramp[last] : 0;

        var result = new int[ramp.Length];
        for (int i = 0; i < ramp.Length; i++)
            result[i] = Math.Clamp(ramp[i] + shift, 0, numberOfSlots);

        for (int i = 1; i < result.Length; i++)
            result[i] = Math.Max(result[i], result[i - 1]);
        return result;
    }

    /// <summary>Classify one bundle. Returns null if the bundle name doesn't match any rule
    /// (i.e. an SVE-added or otherwise unknown bundle the caller should log and skip).</summary>
    /// <param name="parsed">Parsed vanilla bundle data (Data/Bundles entry).</param>
    /// <param name="theme">Bundle's room theme (from <see cref="RoomThemeMap"/>).</param>
    /// <param name="itemSeasonPins">Per-item season pins for KIND 2 bundles (merged
    /// defaults + user). Keyed by qualified item id.</param>
    /// <param name="bundleQuotas">Per-bundle cumulative quotas for KIND 3 bundles (merged
    /// defaults + user). Keyed by bundle name.</param>
    /// <param name="availability">Derived item model. When supplied, the PerItem branch computes
    /// deadlines with <see cref="BundleDeadlines"/> instead of looking ingredients up in
    /// <paramref name="itemSeasonPins"/>. Null keeps the legacy pin-table behaviour, which exists
    /// only until every caller passes a model (Phase 4 of the availability spec).</param>
    public static BundleRequirement? Classify(
        ParsedBundle parsed, Theme theme,
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas,
        ItemAvailabilityModel? availability = null)
    {
        if (parsed == null) throw new ArgumentNullException(nameof(parsed));
        if (itemSeasonPins == null) throw new ArgumentNullException(nameof(itemSeasonPins));
        if (bundleQuotas == null) throw new ArgumentNullException(nameof(bundleQuotas));

        string name = parsed.Name ?? "";

        // Skip category-only bundles — no concrete ids to track.
        List<string> ingredients = CollectQualifiedIngredients(parsed);
        if (ingredients.Count == 0)
            return null;

        // Per-ingredient display data (stack required + minimum quality) — passed to the
        // BundleRequirement so the season-goals UI can render the correct quantity badge +
        // quality star on each icon. Uses MAX across duplicate entries (Construction lists
        // Wood twice with stack 99; max of 99 is the safe "what fits any slot" reading).
        Dictionary<string, int> ingredientStacks = new();
        Dictionary<string, int> ingredientQualities = new();
        foreach (BundleIngredient ing in parsed.Ingredients)
        {
            if (BundleParsing.IsCategoryRef(ing.ItemRef)) continue;
            string id = BundleParsing.NormalizeItemId(ing.ItemRef);
            int stack = ing.Stack > 0 ? ing.Stack : 1;
            if (!ingredientStacks.TryGetValue(id, out int existingStack) || stack > existingStack)
                ingredientStacks[id] = stack;
            if (!ingredientQualities.TryGetValue(id, out int existingQ) || ing.Quality > existingQ)
                ingredientQualities[id] = ing.Quality;
        }

        // KIND 1: Seasonal — bundle name like "Spring Foraging" / "Fall Crops".
        Match seasonalMatch = SeasonalNamePattern.Match(name);
        if (seasonalMatch.Success)
        {
            Season season = ParseSeason(seasonalMatch.Groups["season"].Value);
            return BundleRequirement.CreateSeasonal(name, theme, ingredients, season,
                ingredientStacks, ingredientQualities);
        }

        // KIND 3: Percentage — has a named quota override, BUT only when X < Y after dedup.
        // SVE-edited save data can inflate the bundle's slot count so X >= Y even when the
        // bundle is in the quota table (e.g. Chef's with the SVE Candy entry baked in:
        // X=Y=7 instead of vanilla X=10, Y=6). In that case the Percentage model doesn't
        // apply — fall through to PerItem.
        if (parsed.NumberOfSlots <= ingredients.Count
            && bundleQuotas.TryGetValue(name, out int[]? quota) && quota != null)
        {
            // numberOfSlots = X (the parsed bundle's slot count), ingredients = Y (deduped list).
            // CreatePercentage validates X < Y and Y entries within [0..X] -- it THROWS on a quota
            // entry above X, which would take the whole reset down with it. A configured quota can
            // legitimately exceed this board's X in two ways: the difficulty "required slots"
            // modifier at Easy lowers X by one (spec 2026-08-26), and SVE-edited save data can
            // reshape a bundle. Either way an unsatisfiable quota bricks the run, so clamp rather
            // than trust the table.
            int[] clampedQuota = ShiftRampToSlotCount(quota, parsed.NumberOfSlots);
            return BundleRequirement.CreatePercentage(
                name, theme, ingredients,
                numberOfSlots: parsed.NumberOfSlots,
                cumulativeRequiredBySeason: clampedQuota,
                ingredientStacks: ingredientStacks,
                ingredientQualities: ingredientQualities);
        }

        // KIND 2: PerItem — every distinct ingredient must be donated. The structural rule is
        // X >= Y (the slot list covers each ingredient at least once). Vanilla Construction
        // lists Wood twice (X=4, Y=3 deduped); the set-based donation ledger satisfies the
        // duplicate slot implicitly when wood is donated once.
        if (parsed.NumberOfSlots >= ingredients.Count)
        {
            Dictionary<string, Season> pins = new();
            if (availability != null)
            {
                // Derived model: every ingredient gets a deadline, spread by effort and clamped
                // up to the season it can first exist in. No ingredient can fall through
                // ungated, which is the whole point of the change.
                foreach (KeyValuePair<string, Season> deadline
                         in BundleDeadlines.For(ingredients, availability))
                    pins[deadline.Key] = deadline.Value;
            }
            else
            {
                // Legacy path: only ingredients named in the hand written table gate anything.
                // Unpinned items don't gate any season but still count toward IsFullyComplete.
                foreach (string id in ingredients)
                    if (itemSeasonPins.TryGetValue(id, out Season s))
                        pins[id] = s;
            }
            return BundleRequirement.CreatePerItem(name, theme, ingredients, pins,
                ingredientStacks, ingredientQualities);
        }

        // X < Y with no named quota — an unknown pick-X-of-Y bundle (remixed / SVE / custom
        // bundle mod). Classify as Percentage with a derived default ramp so it still gates
        // seasons, counts toward the win, and feeds the weekly-theme pools. A named
        // BundleQuotas/DefaultBundleQuotas entry (checked above) overrides this.
        return BundleRequirement.CreatePercentage(
            name, theme, ingredients,
            numberOfSlots: parsed.NumberOfSlots,
            cumulativeRequiredBySeason: DerivedDefaultQuota(parsed.NumberOfSlots),
            ingredientStacks: ingredientStacks,
            ingredientQualities: ingredientQualities);
    }

    /// <summary>
    /// Default cumulative [Spring, Summer, Fall, Winter] quota for a pick-X-of-Y bundle with
    /// no curated entry: floor(X * [0.25, 0.35, 0.60, 1.0]) (spec 2026-08-28-theme-week-budget:
    /// Summer and Fall lean late so Winter keeps lines to ask for; Spring stays where it was,
    /// Jeff 2026-08-28: a 9-line Spring gate is too light). Monotone, each value in
    /// [0..X], Winter always demands the full X so the bundle must be completed to win. Curated
    /// entries stay authoritative where present.
    /// </summary>
    public static int[] DerivedDefaultQuota(int numberOfSlots)
    {
        if (numberOfSlots < 1)
            throw new ArgumentOutOfRangeException(nameof(numberOfSlots),
                $"numberOfSlots must be >= 1; got {numberOfSlots}.");
        return new[]
        {
            numberOfSlots / 4,
            numberOfSlots * 35 / 100,
            numberOfSlots * 3 / 5,
            numberOfSlots
        };
    }

    /// <summary>Distinct, qualified-id ingredient list (drops category refs).</summary>
    private static List<string> CollectQualifiedIngredients(ParsedBundle parsed)
    {
        List<string> result = new();
        HashSet<string> seen = new();
        foreach (BundleIngredient ing in parsed.Ingredients)
        {
            if (BundleParsing.IsCategoryRef(ing.ItemRef))
                continue;
            string id = BundleParsing.NormalizeItemId(ing.ItemRef);
            if (seen.Add(id))
                result.Add(id);
        }
        return result;
    }

    private static Season ParseSeason(string text) => text.ToLowerInvariant() switch
    {
        "spring" => Season.Spring,
        "summer" => Season.Summer,
        "fall"   => Season.Fall,
        "winter" => Season.Winter,
        _ => throw new ArgumentException($"Unknown season name '{text}'.", nameof(text))
    };
}
