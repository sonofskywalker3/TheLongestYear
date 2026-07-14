using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One concrete ingredient slot of a generated bundle: qualified-or-bare item id
/// exactly as it will appear in the BundleData string ("24", "(O)128", or "-1" for money),
/// required stack, and minimum quality (0 normal / 1 silver / 2 gold / 4 iridium).</summary>
public sealed record BundleSlotSpec(string ItemId, int Stack, int Quality);

/// <summary>A complete generated bundle, ready to be written into BundleData by
/// <see cref="BundleDataWriter"/>. Field meanings mirror vanilla's slash-delimited format
/// (see BundleParsing): RewardField is the raw reward text (e.g. "O 495 30"), Color is the
/// bundle's sprite tint index, NumberOfSlots is the pick-X count (how many of the Slots
/// must be donated). Names must stay STABLE and UNIQUE per save — downstream systems
/// (SlotPoolBuilder, quota tables) match bundles by name.</summary>
public sealed record BundleSpec(
    string Room,
    int Index,
    string Name,
    string DisplayName,
    string RewardField,
    int Color,
    int NumberOfSlots,
    IReadOnlyList<BundleSlotSpec> Slots);
