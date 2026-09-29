using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Pure rules for the keep_pet carryover (the game-side work is in
/// Loop/PetCarryoverService).</summary>
public static class PetCarryover
{
    private const int RestoreTileX = 54;
    private const int RestoreTileY = 8;
    private const int ColumnsPerPet = 2;
    private const int MaxFriendship = 1000;

    /// <summary>Moves a pre-0.13.0 single snapshot into <see cref="MetaState.PetStates"/>
    /// when the list is empty; always clears the legacy field. True when a snapshot moved.</summary>
    public static bool MigrateLegacy(MetaState state)
    {
        state.PetStates ??= new();
        bool moved = false;
        if (state.PetState != null && state.PetStates.Count == 0)
        {
            state.PetStates.Add(state.PetState);
            moved = true;
        }
        state.PetState = null;
        return moved;
    }

    /// <summary>Where pet number <paramref name="index"/> lands on the Farm: the porch tile,
    /// staggered two columns further WEST per pet so they do not stack. West, not east:
    /// x59-67 above the farmhouse is a no-go footprint per WorldResetService, so marching
    /// east would walk pets into the house.</summary>
    public static (int X, int Y) RestoreTile(int index)
        => (RestoreTileX - ColumnsPerPet * Math.Max(0, index), RestoreTileY);

    /// <summary>Where a pet bowl for pet number <paramref name="index"/> goes when the farm has
    /// no free bowl left for it: one tile up-left of that pet's restore tile, so the bowl's pet
    /// spot (bowl tile + 1 row, vanilla PetBowl.GetPetSpot) is the row the pets stand on. Index 0
    /// lands on vanilla's own default bowl tile (53,7) and later indexes stagger west with the pets.
    /// The farm ships ONE bowl and vanilla lets one pet own a bowl; every extra pet was warped
    /// indoors each morning and docked friendship (Nexus bug 1122901, second report).</summary>
    public static (int X, int Y) BowlTile(int index)
        => (RestoreTileX - 1 - ColumnsPerPet * Math.Max(0, index), RestoreTileY - 1);

    public static int ClampFriendship(int value) => Math.Clamp(value, 0, MaxFriendship);

    /// <summary>Marnie's pet visit, cat (1590166) and dog (897405). Data/Events/Farm keys them on
    /// money earned, weekday, weather, pet preference and host only.</summary>
    public static readonly IReadOnlyList<string> ArrivalSceneIds = new[] { "1590166", "897405" };

    /// <summary>After a rewind leaves the farm petless, un-mark Marnie's pet visit so it plays again
    /// (Jeff, 2026-09-29: a streamer read the missing visit as "no pet this loop"). The events-seen
    /// reseed marks every watched scene seen, so without this the visit never came back. Returns how
    /// many ids were cleared.</summary>
    public static int ReopenArrivalScenes(ISet<string> eventsSeen, bool farmHasPet)
    {
        if (eventsSeen == null || farmHasPet) return 0;
        int cleared = 0;
        foreach (string id in ArrivalSceneIds)
            if (eventsSeen.Remove(id)) cleared++;
        return cleared;
    }
}
