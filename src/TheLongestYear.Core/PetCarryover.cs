using System;

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

    public static int ClampFriendship(int value) => Math.Clamp(value, 0, MaxFriendship);
}
