using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The source domain for authored bundle slots. Later tasks (composer, engine wiring)
/// consume this enum to match sources to item pools.</summary>
public enum AuthoredSlotSource
{
    FixedList, Artifacts, Books, Saplings, GeodeMinerals, Cooking, TapperGoods, Trash, ArtisanGoods, Forage, Fish,
    /// <summary>The geode pool with everything that is not a mineral (the artifacts geodes also drop) left out.</summary>
    Minerals
}

/// <summary>A Community Center bundle definition authored for the Longest Year engine (Plan 3).
/// Bundle names are STABLE, UNIQUE, and SLASH-FREE — downstream systems match bundles by name,
/// and the writer sanitizes '/' AFTER uniqueness checks per the spec
/// (docs/superpowers/specs/2026-07-20-tly-0.12.0-engine-3-authored-bundles-design.md).</summary>
public sealed record AuthoredBundleDef(
    string Name,
    string Room,
    string RewardField,
    int Color,
    AuthoredSlotSource Source,
    int SlotCount,
    int NumberOfSlots,
    IReadOnlyList<string> FixedItemIds,
    int QualityAsk = 0,
    bool SeasonSpread = false);

/// <summary>The catalog of 11 hand-authored Community Center bundles for Plan 3 (v0.11.93).
/// The All list is ordinal-stable; trophy lists feed Gil's Trophies bundle composition.</summary>
public static class AuthoredBundleCatalog
{
    // Year-1-feasible trophies only (user ruling 2026-08-21). Dropped: (H)50 Knight's Helmet
    // (50 Pepper Rex), (H)60 Arcane Hat (100 Mummies), (O)520 Slime Charmer (1,000 slimes),
    // (O)811 Napalm Ring (250 Serpents) — all Skull Cavern / multi-year kill counts that made
    // ~9% of loops roll an uncompletable Gil's Trophies.
    private static readonly string[] _gilTrophies = new[]
    {
        "(H)27", "(H)8", "(O)522", "(O)523", "(O)526", "(O)810", "(W)13"
    };

    private static readonly string[] _gilTrophyRingsOnly = new[]
    {
        "(O)522", "(O)523", "(O)526", "(O)810"
    };

    private static readonly AuthoredBundleDef[] _all = new[]
    {
        new AuthoredBundleDef(
            Name: "Artifact",
            Room: "Bulletin Board",
            RewardField: "O 749 15",
            Color: 2,
            Source: AuthoredSlotSource.Artifacts,
            SlotCount: 6,
            NumberOfSlots: 4,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Mineral",
            Room: "Boiler Room",
            RewardField: "BO 21 1",
            Color: 1,
            Source: AuthoredSlotSource.Minerals,   // rocks only: the geode pool also holds the artifacts and oddments geodes drop
            SlotCount: 6,
            NumberOfSlots: 4,
            FixedItemIds: new List<string>()),

        // Jeff, 2026-09-04: "I want to see minerals much higher ... a Jeweler's bundle for just stuff
        // you'd put in fancy jewelry, a Rockhound's bundle." Both sit in the Boiler Room next to
        // Mineral, so the room's three positions draw from five mineral-minded candidates.
        new AuthoredBundleDef(
            Name: "Jeweler's",
            Room: "Boiler Room",
            RewardField: "O 72 1",
            Color: 5,
            Source: AuthoredSlotSource.FixedList,
            SlotCount: 6,
            NumberOfSlots: 4,
            FixedItemIds: new[] { "(O)72", "(O)64", "(O)60", "(O)70", "(O)62", "(O)66", "(O)68" }),   // Diamond, Ruby, Emerald, Jade, Aquamarine, Amethyst, Topaz

        new AuthoredBundleDef(
            Name: "Rockhound's",
            Room: "Boiler Room",
            RewardField: "BO 182 1",
            Color: 1,
            Source: AuthoredSlotSource.Minerals,
            SlotCount: 8,
            NumberOfSlots: 5,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Book",
            Room: "Bulletin Board",
            RewardField: "O PurpleBook 1",
            Color: 5,
            Source: AuthoredSlotSource.Books,
            SlotCount: 5,
            NumberOfSlots: 3,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Tapper's",
            Room: "Crafts Room",
            RewardField: "BO 105 3",
            Color: 0,
            Source: AuthoredSlotSource.TapperGoods,
            SlotCount: 5,
            NumberOfSlots: 4,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Four Seasons Sampler",
            Room: "Crafts Room",
            RewardField: "O 251 2",
            Color: 0,
            Source: AuthoredSlotSource.Forage,
            SlotCount: 6,
            NumberOfSlots: 5,
            FixedItemIds: new List<string>(),
            QualityAsk: 0,
            SeasonSpread: true),

        new AuthoredBundleDef(
            Name: "Orchard",
            Room: "Pantry",
            RewardField: "BO 15 1",
            Color: 4,
            Source: AuthoredSlotSource.Saplings,
            SlotCount: 5,
            NumberOfSlots: 5,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Preserver's",
            Room: "Pantry",
            RewardField: "BO 12 1",
            Color: 4,
            Source: AuthoredSlotSource.ArtisanGoods,
            SlotCount: 6,
            NumberOfSlots: 4,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Home Cook's Feast",
            Room: "Pantry",
            RewardField: "O 926 1",
            Color: 2,
            Source: AuthoredSlotSource.Cooking,
            SlotCount: 6,
            NumberOfSlots: 4,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Weatherman's",
            Room: "Fish Tank",
            RewardField: "O 681 2",
            Color: 6,
            Source: AuthoredSlotSource.Fish,
            SlotCount: 5,
            NumberOfSlots: 4,
            FixedItemIds: new List<string>()),

        new AuthoredBundleDef(
            Name: "Gil's Trophies",
            Room: "Boiler Room",
            RewardField: "O 879 5",
            Color: 4,
            Source: AuthoredSlotSource.FixedList,
            SlotCount: 4,
            NumberOfSlots: 2,
            FixedItemIds: _gilTrophies),

        new AuthoredBundleDef(
            Name: "Recycler's",
            Room: "Bulletin Board",
            RewardField: "BO 20 1",
            Color: 3,
            Source: AuthoredSlotSource.FixedList,
            SlotCount: 6,
            NumberOfSlots: 4,
            FixedItemIds: new[] { "(O)168", "(O)169", "(O)170", "(O)171", "(O)172", "(O)338", "(O)428" })
    };

    public static IReadOnlyList<AuthoredBundleDef> All => _all;
    public static IReadOnlyList<string> GilTrophies => _gilTrophies;
    public static IReadOnlyList<string> GilTrophyRingsOnly => _gilTrophyRingsOnly;
}
