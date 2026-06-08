namespace TheLongestYear.Core;

/// <summary>
/// Serializable snapshot of a single item in the Junimo Stash. Round-trips through
/// System.Text.Json via <see cref="MetaState.StashItems"/> and is recreated on restore via
/// ItemRegistry.Create (+ the preserve fields below re-applied for flavored goods).
///
/// <para>
/// The last three fields preserve a flavored/preserved Object's identity and baked value —
/// the data that does NOT survive recreation by base <see cref="ItemId"/> alone. A Smoked Fish,
/// Wine, Juice, Jelly, Pickles, Aged Roe, Honey, Targeted Bait, etc. all store their source and
/// their copied sale price in <c>preservedParentSheetIndex</c> / <c>preserve</c> / <c>price.Value</c>
/// (see the game's <c>Object.GetOneCopyFrom</c>, which copies exactly these). Without them a
/// Smoked Legend round-trips back as a blank 57g smoked fish instead of ~21,000g. They are null
/// for plain items (ore, wood, sprinklers) so those are recreated untouched. All three are
/// nullable + default null so saves written before this field existed still deserialize.
/// </para>
/// </summary>
/// <param name="PreservedParentSheetIndex">Source item id of a flavored good (e.g. the fish a
/// Smoked Fish was made from); null for non-preserved items.</param>
/// <param name="Preserve">The <c>PreserveType</c> enum value as an int (e.g. SmokedFish); null
/// for non-preserved items.</param>
/// <param name="Price">The Object's stored <c>price.Value</c> (the smoker/keg/etc. bakes the
/// source's price in here); null for items whose price is purely data-driven.</param>
public sealed record StashItemRecord(
    string ItemId,
    int Quantity,
    int Quality,
    string? PreservedParentSheetIndex = null,
    int? Preserve = null,
    int? Price = null);
