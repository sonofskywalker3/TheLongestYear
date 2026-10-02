using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Serializable snapshot of a single item in the Junimo Stash. Round-trips through
/// System.Text.Json via <see cref="MetaState.StashItems"/> and is recreated on restore via
/// ItemRegistry.Create (+ the preserve fields below re-applied for flavored goods).
///
/// <para>
/// The preserve fields keep a flavored/preserved Object's identity and baked value —
/// the data that does NOT survive recreation by base <see cref="ItemId"/> alone. A Smoked Fish,
/// Wine, Juice, Jelly, Pickles, Aged Roe, Honey, Targeted Bait, etc. all store their source and
/// their copied sale price in <c>preservedParentSheetIndex</c> / <c>preserve</c> / <c>price.Value</c>
/// (see the game's <c>Object.GetOneCopyFrom</c>, which copies exactly these). Without them a
/// Smoked Legend round-trips back as a blank 57g smoked fish instead of ~21,000g. They are null
/// for plain items (ore, wood, sprinklers) so those are recreated untouched. All three are
/// nullable + default null so saves written before this field existed still deserialize.
/// </para>
///
/// <para>
/// <see cref="Attachments"/> does the same job for a stashed tool's slots. A fishing rod's bait and
/// tackle live on the tool INSTANCE (<c>Tool.attachments</c>), so a rod recreated by id alone comes
/// back empty (Nexus posts, CausticOptimist 18 Aug + Bumblewyn 27 Aug: "the fishing rod that I had
/// saved ... did not bring the bait back"). One entry per slot, in slot order, null for an empty
/// slot; the entries are ordinary item records because bait is itself an Object (targeted bait
/// carries preserve fields). Null for anything that is not a tool with slots, and for records
/// written before the field existed.
/// </para>
///
/// <para>
/// Contents, HeldObject, Clothing, Boots, InnerRings and TrinketSeed (spec 2026-10-01,
/// sarahwinchester97's dresser) carry what lives on the instance: a container's contents, an item
/// set on a table, dye, tailoring, a Combined Ring's two rings and a trinket's rolled stats. All
/// null by default so 0.18.118 saves load.
/// </para>
/// </summary>
/// <param name="PreservedParentSheetIndex">Source item id of a flavored good (e.g. the fish a
/// Smoked Fish was made from); null for non-preserved items.</param>
/// <param name="Preserve">The <c>PreserveType</c> enum value as an int (e.g. SmokedFish); null
/// for non-preserved items.</param>
/// <param name="Price">The Object's stored <c>price.Value</c> (the smoker/keg/etc. bakes the
/// source's price in here); null for items whose price is purely data-driven.</param>
/// <param name="Attachments">A tool's attachment slots in order (bait, tackle...), null entries
/// for empty slots; null when the item has no slots.</param>
/// <param name="Enchantments">A tool's enchantments/forges in order; null when it has none or the
/// record predates the field.</param>
/// <param name="Contents">A container's held items (StorageFurniture <c>heldItems</c>, a Chest
/// item's <c>Items</c>, a mod bag's item list), non-null entries only.</param>
/// <param name="HeldObject">The item's <c>Object.heldObject</c> (an item or lamp on a table).</param>
/// <param name="Clothing">A shirt's or pants' dye colour and dyeable flag.</param>
/// <param name="Boots">A pair of boots' tailoring (applied sheet index, colour index, bonuses).</param>
/// <param name="InnerRings">A Combined Ring's <c>combinedRings</c>. Deliberately not
/// <see cref="Contents"/>: inner rings are part of the ring, not something it holds, and must not
/// trip the nesting rule.</param>
/// <param name="TrinketSeed">A trinket's <c>generationSeed</c>; recreating with the same seed
/// re-rolls identical stats.</param>
public sealed record StashItemRecord(
    string ItemId,
    int Quantity,
    int Quality,
    string? PreservedParentSheetIndex = null,
    int? Preserve = null,
    int? Price = null,
    List<StashItemRecord?>? Attachments = null,
    List<StashEnchantmentRecord>? Enchantments = null,
    List<StashItemRecord>? Contents = null,
    StashItemRecord? HeldObject = null,
    StashClothingRecord? Clothing = null,
    StashBootsRecord? Boots = null,
    List<StashItemRecord>? InnerRings = null,
    int? TrinketSeed = null);
