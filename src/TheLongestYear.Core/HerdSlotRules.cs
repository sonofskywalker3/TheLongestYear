using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The Herd Book's fixed slot ladder and what each slot accepts (spec 2026-09-25).
/// Slot 0 is the free Chicken slot every save has; herdbook_N adds slot N.</summary>
public static class HerdSlotRules
{
    private static readonly HerdSlotKind[] Ladder =
    {
        HerdSlotKind.Chicken, HerdSlotKind.Chicken,
        HerdSlotKind.Cow, HerdSlotKind.Cow,
        HerdSlotKind.Duck, HerdSlotKind.Duck,
        HerdSlotKind.Goat, HerdSlotKind.Goat,
        HerdSlotKind.Rabbit, HerdSlotKind.Rabbit,
        HerdSlotKind.Sheep, HerdSlotKind.Sheep,
        HerdSlotKind.Pig, HerdSlotKind.Pig,
        HerdSlotKind.VoidChicken, HerdSlotKind.GoldenChicken, HerdSlotKind.Dinosaur, HerdSlotKind.Ostrich,
    };

    private static readonly IReadOnlyDictionary<HerdSlotKind, string[]> AcceptedTypes =
        new Dictionary<HerdSlotKind, string[]>
        {
            [HerdSlotKind.Chicken] = new[] { "White Chicken", "Brown Chicken", "Blue Chicken" },
            [HerdSlotKind.Cow] = new[] { "White Cow", "Brown Cow" },
            [HerdSlotKind.Duck] = new[] { "Duck" },
            [HerdSlotKind.Goat] = new[] { "Goat" },
            [HerdSlotKind.Rabbit] = new[] { "Rabbit" },
            [HerdSlotKind.Sheep] = new[] { "Sheep" },
            [HerdSlotKind.Pig] = new[] { "Pig" },
            [HerdSlotKind.VoidChicken] = new[] { "Void Chicken" },
            [HerdSlotKind.GoldenChicken] = new[] { "Golden Chicken" },
            [HerdSlotKind.Dinosaur] = new[] { "Dinosaur" },
            [HerdSlotKind.Ostrich] = new[] { "Ostrich" },
        };

    /// <summary>The building each kind needs, matching the Start-with keeps (a higher tier of the
    /// same family also counts). Ostrich lives in a Barn (Data/FarmAnimals House).</summary>
    private static readonly IReadOnlyDictionary<HerdSlotKind, string> Housing =
        new Dictionary<HerdSlotKind, string>
        {
            [HerdSlotKind.Chicken] = "Coop",
            [HerdSlotKind.VoidChicken] = "Coop",
            [HerdSlotKind.GoldenChicken] = "Coop",
            [HerdSlotKind.Duck] = "Big Coop",
            [HerdSlotKind.Dinosaur] = "Big Coop",
            [HerdSlotKind.Rabbit] = "Deluxe Coop",
            [HerdSlotKind.Cow] = "Barn",
            [HerdSlotKind.Ostrich] = "Barn",
            [HerdSlotKind.Goat] = "Big Barn",
            [HerdSlotKind.Sheep] = "Deluxe Barn",
            [HerdSlotKind.Pig] = "Deluxe Barn",
        };

    private static readonly IReadOnlyDictionary<string, string> KeepForHousing =
        new Dictionary<string, string>
        {
            ["Coop"] = "keep_coop",
            ["Big Coop"] = "keep_big_coop",
            ["Deluxe Coop"] = "keep_deluxe_coop",
            ["Barn"] = "keep_barn",
            ["Big Barn"] = "keep_big_barn",
            ["Deluxe Barn"] = "keep_deluxe_barn",
        };

    /// <summary>Total slots the full ladder has (18).</summary>
    public static int LadderLength => Ladder.Length;

    /// <summary>The slots a save has with <paramref name="purchasedTier"/> herdbook tiers owned:
    /// the free first slot plus one per tier, clamped to the ladder.</summary>
    public static IReadOnlyList<HerdSlotKind> SlotsFor(int purchasedTier)
        => Ladder.Take(1 + Math.Clamp(purchasedTier, 0, Ladder.Length - 1)).ToArray();

    /// <summary>The kind of slot <paramref name="slotIndex"/> on the ladder.</summary>
    public static HerdSlotKind KindAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= Ladder.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Not a Herd Book slot.");
        return Ladder[slotIndex];
    }

    /// <summary>True when an animal of this vanilla type may go in a slot of this kind (exact,
    /// case-sensitive vanilla type names).</summary>
    public static bool Accepts(HerdSlotKind kind, string? vanillaType)
        => vanillaType != null && AcceptedTypes[kind].Contains(vanillaType, StringComparer.Ordinal);

    /// <summary>The lowest building this kind can come back into ("Coop", "Big Barn"...).</summary>
    public static string RequiredHousing(HerdSlotKind kind) => Housing[kind];

    /// <summary>The keep upgrade that rebuilds <see cref="RequiredHousing"/> each loop.</summary>
    public static string RequiredKeepId(HerdSlotKind kind) => KeepForHousing[Housing[kind]];

    /// <summary>The slot kind's player-facing name.</summary>
    public static string DisplayName(HerdSlotKind kind) => kind switch
    {
        HerdSlotKind.Chicken => Strings.Get("herd-slot.chicken"),
        HerdSlotKind.Cow => Strings.Get("herd-slot.cow"),
        HerdSlotKind.Duck => Strings.Get("herd-slot.duck"),
        HerdSlotKind.Goat => Strings.Get("herd-slot.goat"),
        HerdSlotKind.Rabbit => Strings.Get("herd-slot.rabbit"),
        HerdSlotKind.Sheep => Strings.Get("herd-slot.sheep"),
        HerdSlotKind.Pig => Strings.Get("herd-slot.pig"),
        HerdSlotKind.VoidChicken => Strings.Get("herd-slot.void-chicken"),
        HerdSlotKind.GoldenChicken => Strings.Get("herd-slot.golden-chicken"),
        HerdSlotKind.Dinosaur => Strings.Get("herd-slot.dinosaur"),
        HerdSlotKind.Ostrich => Strings.Get("herd-slot.ostrich"),
        _ => kind.ToString(),
    };
}
