using StardewValley;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Maps a game item to the <see cref="GearPowers.ToolKind"/> the Gear chains key on.</summary>
    internal static class GearToolKind
    {
        internal static ToolKind KindOf(Item item)
        {
            switch (item)
            {
                case Hoe: return ToolKind.Hoe;
                case Pickaxe: return ToolKind.Pickaxe;
                case Axe: return ToolKind.Axe;
                case WateringCan: return ToolKind.WateringCan;
                case FishingRod: return ToolKind.FishingRod;
                case MeleeWeapon weapon: return weapon.isScythe() ? ToolKind.Scythe : ToolKind.MeleeWeapon;
                default: return ToolKind.Other;
            }
        }
    }
}
