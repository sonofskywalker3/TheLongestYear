using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Everything <see cref="ItemAvailabilityBuilder"/> reads besides the step and the kitchen
/// state, as plain data: the <see cref="ItemPools"/> members it reads (the map and shop weeks as their
/// dictionaries, the placed shops as <see cref="ShopPlacements"/>), the effort data whole, the season, effort and week overrides, and whether the
/// year-two crops are capped (the one thing the save's upgrades decide there).
/// <para><see cref="AvailabilityAnswers.InputsHash"/> is the hash of this record's
/// <see cref="LexiconJson.Options"/> JSON, so the members, their names, their order and their types
/// are part of every stored <see cref="AvailabilityAnswers.Inputs"/> stamp: changing any of them makes
/// every shipped and cached availability answer read as stale. <c>tly_lexicon dumpinputs</c> writes
/// one as a test fixture, and <see cref="Answers"/> rebuilds the answers from it offline.</para></summary>
public sealed record AvailabilityInputs(
    IReadOnlyList<PoolItem>? Fish, IReadOnlyList<PoolItem>? CrabPot, IReadOnlyList<PoolItem>? UnpooledFish,
    IReadOnlyList<PoolItem>? Metals, IReadOnlyList<string>? TrapFishIds,
    IReadOnlyDictionary<string, RawFishEntry>? FishRows,
    IReadOnlyDictionary<string, PlaceWeek>? LocationWeeks, IReadOnlyDictionary<string, PlaceWeek>? ShopWeeks,
    IReadOnlyList<string>? UnplacedShopItems,
    IReadOnlyList<PoolItem>? Saplings, IReadOnlyList<PoolItem>? Artifacts, IReadOnlyList<PoolItem>? Books,
    EffortData? EffortData,
    IReadOnlyDictionary<string, Season>? SeasonOverrides, IReadOnlyDictionary<string, int>? EffortOverrides,
    IReadOnlyDictionary<string, int>? WeekOverrides,
    bool YearTwoCapped,
    IReadOnlyDictionary<string, CatchRate>? CatchRates = null,
    IReadOnlyDictionary<string, PlaceWeek>? ShopPlacements = null)
{
    private const int HashChars = 16;

    /// <summary>The projection of the live inputs; the trap fish set and the items sold only
    /// outside year 1 (<see cref="ShopWeeks.UnplacedItems"/>, null when there are none) are sorted
    /// ordinally, and so is every dictionary, the effort data's included, so the stamp is a function
    /// of the content alone: Stardew Valley Expanded's location walk visits its maps in a different
    /// order on a loaded save than on a fresh farm, and the same inputs hashed two ways.</summary>
    public static AvailabilityInputs From(
        ItemPools pools, EffortData? effortData,
        IReadOnlyDictionary<string, Season>? seasonOverrides, IReadOnlyDictionary<string, int>? effortOverrides,
        IReadOnlyDictionary<string, int>? weekOverrides, bool yearTwoCapped)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        return new AvailabilityInputs(
            pools.Fish, pools.CrabPot, pools.UnpooledFish, pools.Metals,
            pools.TrapFishIds?.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            OrdinalOrder.Of(pools.FishRows),
            OrdinalOrder.Of(pools.LocationWeeks?.Reached), OrdinalOrder.Of(pools.ShopWeeks?.Weeks),
            pools.ShopWeeks?.UnplacedItems is { Count: > 0 } unplaced
                ? unplaced.OrderBy(id => id, StringComparer.Ordinal).ToList()
                : null,
            pools.Saplings, pools.Artifacts, pools.Books,
            effortData?.Ordered(), OrdinalOrder.Of(seasonOverrides), OrdinalOrder.Of(effortOverrides),
            OrdinalOrder.Of(weekOverrides), yearTwoCapped,
            OrdinalOrder.Of(pools.CatchRates),
            OrdinalOrder.Of(pools.ShopWeeks?.Shops));
    }

    /// <summary>The first <see cref="HashChars"/> lower-case hex digits of the SHA-256 of this record's
    /// <see cref="LexiconJson.Options"/> JSON.</summary>
    public string Hash()
    {
        byte[] digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this, LexiconJson.Options));
        return Convert.ToHexString(digest)[..HashChars].ToLowerInvariant();
    }

    /// <summary>The pools these inputs were taken from, as far as the availability builder reads them.</summary>
    public ItemPools Pools() => new()
    {
        Fish = Fish ?? new List<PoolItem>(),
        CrabPot = CrabPot ?? new List<PoolItem>(),
        UnpooledFish = UnpooledFish ?? new List<PoolItem>(),
        Metals = Metals ?? new List<PoolItem>(),
        TrapFishIds = new HashSet<string>(TrapFishIds ?? Array.Empty<string>(), StringComparer.Ordinal),
        FishRows = FishRows ?? new Dictionary<string, RawFishEntry>(),
        LocationWeeks = LocationWeeks == null ? null : Availability.LocationWeeks.FromReached(LocationWeeks),
        ShopWeeks = ShopWeeks == null ? null : Availability.ShopWeeks.FromWeeks(ShopWeeks, UnplacedShopItems, ShopPlacements),
        Saplings = Saplings ?? new List<PoolItem>(),
        Artifacts = Artifacts ?? new List<PoolItem>(),
        Books = Books ?? new List<PoolItem>(),
        CatchRates = CatchRates ?? new Dictionary<string, CatchRate>(StringComparer.Ordinal),
    };

    /// <summary>The upgrade test the game hands the builder, as far as it reads it: only
    /// <see cref="YearTwoCrops.IsCapped"/>, which is true for a save without the Pierre upgrade.</summary>
    private Func<string, bool> HasUpgrade() => YearTwoCapped ? _ => false : id => id == YearTwoCrops.PierreUpgrade;

    /// <summary>The model the game builds from these inputs for one step and kitchen state (the
    /// arguments <c>ModEntry.AvailabilityBuild</c> passes).</summary>
    public static ItemAvailabilityModel BuildFrom(AvailabilityInputs inputs, DifficultyStep step, bool kitchen)
        => Build(inputs, step, kitchen).Model;

    /// <summary>The answers the game stores for these inputs, one step and kitchen state, stamped with
    /// <see cref="Hash"/>.</summary>
    public static AvailabilityAnswers Answers(AvailabilityInputs inputs, DifficultyStep step, bool kitchen)
        => Build(inputs, step, kitchen).Answers with { Inputs = inputs.Hash() };

    private static (ItemAvailabilityModel Model, AvailabilityAnswers Answers) Build(AvailabilityInputs inputs, DifficultyStep step, bool kitchen)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        return ItemAvailabilityBuilder.BuildWithAnswers(
            inputs.Pools(), inputs.SeasonOverrides, inputs.EffortOverrides, inputs.EffortData, kitchen,
            inputs.WeekOverrides, WeekModes.For(step), step, inputs.HasUpgrade());
    }
}
