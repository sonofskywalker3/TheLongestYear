using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Pure Herd Book rules (spec 2026-09-25): slot count, hearts, registration, which
/// animals the picker offers, whether the rewind night opens the book, and the pre-reset refresh.
/// The game-side work is in Loop/HerdBookService.</summary>
public static class HerdBookRules
{
    private const int FriendshipPerHeart = 200;
    private const int MaxFriendship = 1000;
    private const int MaxHappiness = 255;

    /// <summary>The slots <paramref name="meta"/> owns: the free Chicken slot plus one per herdbook tier.</summary>
    public static IReadOnlyList<HerdSlotKind> SlotsFor(MetaState meta)
        => UpgradeCatalog.HerdBookSlots(meta.HighestKeptTier(UpgradeCatalog.HerdBookPrefix, UpgradeCatalog.HerdBookMaxTier));

    public static int Hearts(int friendship) => ClampFriendship(friendship) / FriendshipPerHeart;

    public static int ClampFriendship(int value) => Math.Clamp(value, 0, MaxFriendship);

    public static int ClampHappiness(int value) => Math.Clamp(value, 0, MaxHappiness);

    /// <summary>A kept animal always comes back grown: its own age if it was already an adult,
    /// else exactly the age it matures at.</summary>
    public static int AdultAge(int savedAge, int daysToMature) => Math.Max(savedAge, daysToMature);

    public static HerdEntry? EntryAt(IReadOnlyList<HerdEntry> book, int slotIndex)
        => book.FirstOrDefault(e => e.SlotIndex == slotIndex);

    /// <summary>Registered entries inside the owned slots.</summary>
    public static int Used(IReadOnlyList<HerdEntry> book, int slotCount)
        => book.Count(e => e.SlotIndex >= 0 && e.SlotIndex < slotCount);

    /// <summary>Live animals a slot of <paramref name="kind"/> can take: the right type and not
    /// registered anywhere in the book. Sorted by name, then id, so the picker is stable.</summary>
    public static List<HerdAnimal> Candidates(HerdSlotKind kind, IEnumerable<HerdAnimal> live, IReadOnlyList<HerdEntry> book)
    {
        var registered = new HashSet<long>(book.Select(e => e.AnimalId));
        return live
            .Where(a => HerdSlotRules.Accepts(kind, a.Type) && !registered.Contains(a.Id))
            .OrderBy(a => a.Name, StringComparer.Ordinal)
            .ThenBy(a => a.Id)
            .ToList();
    }

    /// <summary>Open the book on a rewind night only when an owned slot is empty and some animal
    /// on the farm fits it. Otherwise the night is not interrupted.</summary>
    public static bool ShouldOfferAtReset(IReadOnlyList<HerdSlotKind> slots, IReadOnlyList<HerdEntry> book, IReadOnlyList<HerdAnimal> live)
    {
        for (int i = 0; i < slots.Count; i++)
            if (EntryAt(book, i) == null && Candidates(slots[i], live, book).Count > 0)
                return true;
        return false;
    }

    /// <summary>Put <paramref name="entry"/> in its slot. Refuses a slot the save does not own, a
    /// type the slot does not take, and an animal already in another slot. Replaces whatever the
    /// slot held; the book stays sorted by slot.</summary>
    public static HerdRegisterResult Register(List<HerdEntry> book, IReadOnlyList<HerdSlotKind> slots, HerdEntry entry)
    {
        if (entry.SlotIndex < 0 || entry.SlotIndex >= slots.Count)
            return HerdRegisterResult.SlotNotOwned;
        if (!HerdSlotRules.Accepts(slots[entry.SlotIndex], entry.Type))
            return HerdRegisterResult.WrongKind;
        if (book.Any(e => e.AnimalId == entry.AnimalId && e.SlotIndex != entry.SlotIndex))
            return HerdRegisterResult.AlreadyRegistered;
        book.RemoveAll(e => e.SlotIndex == entry.SlotIndex);
        book.Add(entry);
        book.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
        return HerdRegisterResult.Registered;
    }

    /// <summary>Empty a slot. True when it held something.</summary>
    public static bool Remove(List<HerdEntry> book, int slotIndex)
        => book.RemoveAll(e => e.SlotIndex == slotIndex) > 0;

    /// <summary>Before the reset, every entry whose animal is alive takes that animal's current
    /// snapshot (keeping its slot), so it comes back with this loop's hearts. An entry whose animal
    /// is gone keeps its last snapshot (spec 2026-09-25, section 4).</summary>
    public static List<HerdEntry> Refresh(IReadOnlyList<HerdEntry> book, IReadOnlyDictionary<long, HerdEntry> liveById)
        => book
            .Select(e => liveById.TryGetValue(e.AnimalId, out HerdEntry? live) ? live with { SlotIndex = e.SlotIndex } : e)
            .ToList();
}
