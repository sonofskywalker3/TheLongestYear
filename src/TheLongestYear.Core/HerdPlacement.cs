using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>An animal house on the fresh farm: its building type and the room left in it
/// (animalLimit minus animalsThatLiveHere). The Herd Book is restored before the Start-with
/// animals (option C, 2026-09-25), so on a rewind this is the building's whole room.</summary>
public sealed record HerdHouse(string Blueprint, int FreeRoom);

/// <summary>Why a Herd Book entry did not come back this loop.</summary>
public enum HerdSkip
{
    None,
    SlotNotOwned,
    NoBuilding,
    NoRoom,
}

/// <summary>Where one entry goes: an index into the house list, or -1 with the reason.</summary>
public readonly record struct HerdAssignment(HerdEntry Entry, int HouseIndex, HerdSkip Skip);

/// <summary>Decides which house each Herd Book entry comes back into (spec 2026-09-25, section 4).
/// Entries go in slot order, so when room runs out the lowest slot wins. A house qualifies when it is
/// the slot kind's family at the required tier or higher and still has room. A skipped entry is never
/// removed from the book; it waits for the next loop.</summary>
public static class HerdPlacement
{
    public static List<HerdAssignment> Assign(IReadOnlyList<HerdEntry> book, int slotCount, IReadOnlyList<HerdHouse> houses)
    {
        int[] room = houses.Select(h => Math.Max(0, h.FreeRoom)).ToArray();
        var result = new List<HerdAssignment>();
        foreach (HerdEntry entry in book.OrderBy(e => e.SlotIndex))
        {
            if (entry.SlotIndex < 0 || entry.SlotIndex >= slotCount)
            {
                result.Add(new HerdAssignment(entry, -1, HerdSkip.SlotNotOwned));
                continue;
            }
            (string family, int tier) need = AnimalHousing.Chain(HerdSlotRules.RequiredHousing(HerdSlotRules.KindAt(entry.SlotIndex)));
            int fit = -1;
            bool anyBuilding = false;
            for (int i = 0; i < houses.Count; i++)
            {
                (string family, int tier) have = AnimalHousing.Chain(houses[i].Blueprint);
                if (have.family != need.family || have.tier < need.tier)
                    continue;
                anyBuilding = true;
                if (room[i] > 0) { fit = i; break; }
            }
            if (fit >= 0)
            {
                room[fit]--;
                result.Add(new HerdAssignment(entry, fit, HerdSkip.None));
            }
            else
            {
                result.Add(new HerdAssignment(entry, -1, anyBuilding ? HerdSkip.NoRoom : HerdSkip.NoBuilding));
            }
        }
        return result;
    }
}
