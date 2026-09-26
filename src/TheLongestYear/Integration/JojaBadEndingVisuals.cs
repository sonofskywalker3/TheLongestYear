using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's draw-side pieces: the animals' left-walk frames, the planks on
    /// Pierre's door, and the Harmony patches that hide the farmhouse and its mail flag.</summary>
    internal static partial class JojaBadEndingCommands
    {
        /// <summary>A farm animal's sheet has no left-facing row (FarmAnimal draws its right-facing
        /// frames flipped); an NPC walking left shows row 3, which is the eating frames. Swap them
        /// to the right-walk frames, mirrored, after the game's update and before the draw.</summary>
        private static void FlipLeftWalkers(Event ev)
        {
            foreach (NPC actor in ev.actors)
            {
                AnimatedSprite sprite = actor?.Sprite;
                if (sprite?.textureName.Value == null || !sprite.textureName.Value.StartsWith(AnimalTexturePrefix, StringComparison.Ordinal))
                    continue;
                if (actor.FacingDirection != FaceLeft)
                {
                    actor.flip = false;
                    continue;
                }
                if (sprite.currentFrame >= LeftFramesStart && sprite.currentFrame < LeftFramesStart + FramesPerRow)
                    sprite.CurrentFrame = RightFramesStart + sprite.currentFrame - LeftFramesStart;
                actor.flip = true;
            }
        }

        private static void DrawPlanks(SpriteBatch b)
        {
            if (Planks.Count == 0 || !IsBadEnding(Game1.CurrentEvent)) return;
            foreach (var (loc, door) in Planks)
            {
                if (loc != Game1.currentLocation) continue;
                Vector2 centre = Game1.GlobalToLocal(Game1.viewport, new Vector2(door.Center.X, door.Center.Y));
                DrawPlank(b, centre, PlankAngle);
                DrawPlank(b, centre, -PlankAngle);
            }
        }

        private static void DrawPlank(SpriteBatch b, Vector2 centre, float angle)
        {
            var origin = new Vector2(0.5f, 0.5f);
            b.Draw(Game1.staminaRect, centre, null, PlankEdgeColour, angle, origin,
                new Vector2(PlankLength + PlankEdge, PlankThickness + PlankEdge), SpriteEffects.None, 1f);
            b.Draw(Game1.staminaRect, centre, null, PlankColour, angle, origin,
                new Vector2(PlankLength, PlankThickness), SpriteEffects.None, 1f);
        }

        private static bool IsHiddenFarmhouse(Building building)
            => _farmhouseHidden && building != null && building == Game1.getFarm()?.GetMainFarmHouse();

        [HarmonyPatch(typeof(Building), nameof(Building.draw))]
        internal static class HideFarmhouseDraw
        {
            private static bool Prefix(Building __instance) => !IsHiddenFarmhouse(__instance);
        }

        /// <summary>The farm's new-mail flag floats over the mailbox; with the house gone it would
        /// hang in the air. While hidden, the mailbox is off the map.</summary>
        [HarmonyPatch(typeof(Farmer), nameof(Farmer.getMailboxPosition))]
        internal static class HideMailFlag
        {
            private static readonly Point OffMap = new(-100, -100);

            private static void Postfix(ref Point __result)
            {
                if (_farmhouseHidden) __result = OffMap;
            }
        }
    }
}
