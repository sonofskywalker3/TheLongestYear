using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What a blight night goes after: the crops in the ground, or what is stored in chests.</summary>
public enum BlightTarget { Crops, Chests }

/// <summary>One thing a chest blight may take from, as the planner sees it: which chest it sits in
/// (<see cref="OwnerId"/>, negative for a machine standing on a map), how many of the night's units
/// it is worth, and whether it is a big craftable (which costs more per copy).</summary>
public readonly struct TakeCandidate
{
    public readonly int OwnerId;
    public readonly int Units;
    public readonly bool BigCraftable;

    public TakeCandidate(int ownerId, int units, bool bigCraftable)
    {
        OwnerId = ownerId;
        Units = units;
        BigCraftable = bigCraftable;
    }

    /// <summary>A placed machine belongs to no chest.</summary>
    public bool Machine => OwnerId < 0;
}

/// <summary>How many crops blight takes on a night it strikes: a share of the live crops,
/// clamped so a tiny patch loses one and a big field loses a handful, never the run.</summary>
public static partial class BlightRule
{
    /// <summary>Is this item in the chest-blight pool (spec 2026-09-15 Part B, section 2.5)? The one
    /// place that answers it, so the glue's walk over the maps carries no rules of its own.
    ///
    /// A warded item is never in: a chest or a machine on a Circle of Warding's tiles is safe, and so
    /// is the Junimo Stash (the caller excludes the stash before asking). A STORED item (in a chest,
    /// <paramref name="placedOnMap"/> false) is in the pool on Extreme whatever it is, and below
    /// Extreme only when it is a plain object stack: tools, weapons, rings, boots, hats and big
    /// craftables in storage are safe. A PLACED item (standing on a map) joins the pool only on
    /// Extreme, only as a big craftable, and only on the farm; other maps are exempt, matching crop
    /// blight's farm-only rule.</summary>
    public static bool InStoragePool(bool isPlainObject, bool isBigCraftable, bool placedOnMap, bool onFarm, bool warded, bool everything)
    {
        if (warded) return false;
        if (!placedOnMap) return everything || (isPlainObject && !isBigCraftable);
        return everything && isBigCraftable && onFarm;
    }

    /// <summary>How many crops die on a strike: the level's share of the live crops, at least one,
    /// capped by season and level (spec 2026-09-15 Part B, section 2.4).</summary>
    public static int Count(int liveCrops, Season season, DifficultyStep level)
        => Take(liveCrops, season, level);

    /// <summary>How many stored units go on a chest strike, from the total in unwarded chests (the
    /// Junimo Stash excluded by the caller). Same share and caps as crops.</summary>
    public static int SpoilCount(int storedUnits, Season season, DifficultyStep level)
        => Take(storedUnits, season, level);

    /// <summary>How many of the night's take one item costs: a stack counts its size, a machine
    /// (placed or stored) counts <see cref="DarknessLevels.BigCraftableUnits"/> per copy (Jeff, 2026-09-15).</summary>
    public static int UnitsOf(int stack, bool bigCraftable)
        => Math.Max(0, stack) * (bigCraftable ? DarknessLevels.BigCraftableUnits : 1);

    /// <summary>Decimal digits kept before rounding up: enough to tell 100 * 0.07 = 7 apart from a
    /// real fraction, without a double's binary rounding (7.000000000000001) forcing an extra
    /// unit.</summary>
    private const int SharePrecision = 6;

    private static int Take(int have, Season season, DifficultyStep level)
    {
        if (have <= 0) return 0;
        double raw = Math.Round(have * DarknessLevels.BlightShare(level), SharePrecision);
        int n = (int)Math.Ceiling(raw);
        n = Math.Max(SabotageTuning.BlightMinPerNight, Math.Min(DarknessLevels.BlightCap(level, season), n));
        return Math.Min(n, have);
    }

    /// <summary>Vanilla object categories that are food and so "spoil": vegetables, fruit,
    /// flowers, forage greens, fish, eggs, milk and other animal products, cooked dishes,
    /// artisan goods. Anything else taken from a chest "goes missing" instead.</summary>
    public static bool IsPerishableCategory(int category) => category switch
    {
        -75 => true,   // vegetable
        -79 => true,   // fruit
        -80 => true,   // flower
        -81 => true,   // greens / forage
        -4 => true,    // fish
        -5 => true,    // egg
        -6 => true,    // milk
        -18 => true,   // animal product
        -7 => true,    // cooking
        -26 => true,   // artisan goods
        _ => false,
    };

    /// <summary>Which things a chest blight takes a unit from tonight, as indexes into
    /// <paramref name="pool"/> in the order they were taken (an entry repeats once per unit).
    /// Units are drawn one at a time, each off an entry picked by <paramref name="rng"/> weighted by
    /// the units it has left, until the night's <paramref name="count"/> is spent or nothing is left
    /// to take. A machine costs <see cref="DarknessLevels.BigCraftableUnits"/> of the count per copy.
    ///
    /// The one constraint (Jeff, 2026-09-21): the night has ONE chest. The first time a roll lands on
    /// a chest entry, every entry belonging to any other chest leaves the pool. Machines stay in it
    /// throughout, so a night's take is units from at most one chest plus any number of machines.
    /// One roll per unit either way, so a re-slept night rolls the same outcome.</summary>
    public static IReadOnlyList<int> PlanTake(IReadOnlyList<TakeCandidate> pool, int count, Random rng)
    {
        if (pool is null) throw new ArgumentNullException(nameof(pool));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        var result = new List<int>();
        if (count <= 0) return result;

        var left = new int[pool.Count];
        var live = new List<int>();
        for (int i = 0; i < pool.Count; i++)
        {
            left[i] = Math.Max(0, pool[i].Units);
            if (left[i] > 0) live.Add(i);
        }

        int chest = NoChestYet;
        int taken = 0;
        while (taken < count && live.Count > 0)
        {
            int total = 0;
            foreach (int i in live) total += left[i];
            if (total <= 0) break;
            int roll = rng.Next(total);
            int hit = live[0];
            foreach (int i in live)
            {
                roll -= left[i];
                if (roll < 0) { hit = i; break; }
            }
            int cost = UnitsOf(1, pool[hit].BigCraftable);
            taken += cost;
            result.Add(hit);
            left[hit] -= cost;
            if (left[hit] <= 0) live.Remove(hit);
            if (chest == NoChestYet && !pool[hit].Machine)
            {
                chest = pool[hit].OwnerId;
                live.RemoveAll(i => !pool[i].Machine && pool[i].OwnerId != chest);
            }
        }
        return result;
    }

    /// <summary>The night has not landed on a chest yet. Not a valid owner id: owners are the
    /// caller's chest indexes, which start at zero, and a machine's is negative.</summary>
    private const int NoChestYet = int.MinValue;

    /// <summary>Which of the live crops die: <paramref name="count"/> distinct positions, uniform.</summary>
    public static IReadOnlyList<int> PickIndexes(int liveCrops, int count, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        count = Math.Max(0, Math.Min(count, liveCrops));
        var all = Enumerable.Range(0, liveCrops).ToList();
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        return all.Take(count).ToList();
    }
}

/// <summary>A filled slot the darkness may empty: in an item room, in a bundle that is not yet
/// complete (a complete bundle keeps its reward and its room restoration intact). No ward: the
/// Junimos protect crops, not the hall (Jeff, 2026-09-09).</summary>
public static class ReversionRule
{
    /// <summary>Room themes (see RoomThemeMap): the five item rooms, never the Vault or Joja.</summary>
    public static bool IsItemRoomTheme(Theme theme) => theme is Theme.Farming or Theme.Foraging or Theme.Fishing or Theme.Mining or Theme.Mixed;

    public static IReadOnlyList<DonatedSlot> Candidates(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        var result = new List<DonatedSlot>();
        foreach (BundleRequirement req in requirements)
        {
            if (req.BundleIndex < 0) continue;
            if (!IsItemRoomTheme(req.Theme)) continue;
            if (req.IsFullyComplete(ledger)) continue;
            foreach (DonatedSlot slot in ledger.Entries)
                if (slot.BundleIndex == req.BundleIndex)
                    result.Add(slot);
        }
        return result;
    }

    public static DonatedSlot? Pick(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Random rng)
        => Pick(ledger, requirements, _ => true, rng);

    /// <summary>Uniform among the candidates whose item passes <paramref name="fair"/> (the
    /// FairnessRule, spec 2026-09-15 Part B, 1.6). Null when none passes.</summary>
    public static DonatedSlot? Pick(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Func<string, bool> fair, Random rng)
    {
        if (fair is null) throw new ArgumentNullException(nameof(fair));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        List<DonatedSlot> candidates = Candidates(ledger, requirements).Where(s => fair(s.ItemId)).ToList();
        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }
}
