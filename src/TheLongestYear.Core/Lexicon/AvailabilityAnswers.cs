using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>What <see cref="ItemAvailabilityBuilder"/> computes for one difficulty step and kitchen
/// state: the rule-placed items, the effort-only items, the dish table and the year-two dish units.
/// <see cref="ItemAvailabilityBuilder.FromAnswers"/> lays the live overrides over them to give the
/// same model <see cref="ItemAvailabilityBuilder.Build"/> would.
/// <para><see cref="Inputs"/> is <see cref="InputsHash"/> of everything those answers were computed
/// from; an entry is only served when it equals the live hash. It is kept here rather than as a row
/// of <see cref="RowHashes"/>, because the availability reads inputs the obtainability rows do not
/// (the config's season and week pins, the live world's wild trees), and a mismatch there must not
/// cost the obtainability answers too.</para></summary>
public sealed record AvailabilityAnswers(
    IReadOnlyDictionary<string, ItemAvailability> Derived,
    IReadOnlyDictionary<string, ItemEffort> EffortDerived,
    IReadOnlyDictionary<string, double[]> DishBases,
    IReadOnlyDictionary<string, int> YearTwoCropDishUnits)
{
    private const int HashChars = 16;
    private const string Kitchen = "kitchen";
    private const string NoKitchen = "no_kitchen";

    /// <summary><see cref="InputsHash"/> of the inputs these answers were computed from; empty when unknown.</summary>
    public string Inputs { get; init; } = "";

    /// <summary>The <see cref="AnswerVariant.Availability"/> key: <c>"{step}|kitchen"</c> or <c>"{step}|no_kitchen"</c>.
    /// The week mode follows from the step (<see cref="WeekModes.For"/>).</summary>
    public static string Key(DifficultyStep step, bool hasKitchen) => $"{step}|{(hasKitchen ? Kitchen : NoKitchen)}";

    /// <summary>A hash of every input <see cref="ItemAvailabilityBuilder"/> reads besides the step and
    /// the kitchen (which are the key): the pool lists and tables it reads, the effort data whole, the
    /// season, effort and week overrides (the dish table reads the overridden model), and whether the
    /// year-two crops are capped (the one thing <c>hasUpgrade</c> decides there). Serialised under
    /// <see cref="LexiconJson.Options"/> in data order; the trap fish set is sorted.</summary>
    public static string InputsHash(
        ItemPools pools, EffortData? effortData,
        IReadOnlyDictionary<string, Season>? seasonOverrides, IReadOnlyDictionary<string, int>? effortOverrides,
        IReadOnlyDictionary<string, int>? weekOverrides, bool yearTwoCapped)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        var inputs = new
        {
            pools.Fish, pools.CrabPot, pools.UnpooledFish, pools.Metals,
            TrapFishIds = pools.TrapFishIds?.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            pools.FishRows,
            LocationWeeks = pools.LocationWeeks?.Reached,
            ShopWeeks = pools.ShopWeeks?.Weeks,
            pools.Saplings, pools.Artifacts, pools.Books,
            EffortData = effortData,
            SeasonOverrides = seasonOverrides, EffortOverrides = effortOverrides, WeekOverrides = weekOverrides,
            YearTwoCapped = yearTwoCapped,
        };
        byte[] digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(inputs, LexiconJson.Options));
        return Convert.ToHexString(digest)[..HashChars].ToLowerInvariant();
    }
}
