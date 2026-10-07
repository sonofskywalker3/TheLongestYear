using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The dark aura (spec 2026-09-21): every item of a tampered-away type is drawn
    /// with a pulsing dim purple glow under the sprite wherever an item is drawn in a menu or
    /// held overhead. Both prefixes only draw the glow; the original draw then runs. Patched
    /// manually from ModEntry so a signature mismatch fails loudly at startup.</summary>
    internal static class TaintedAuraPatch
    {
        private static readonly Color AuraColor = new(90, 0, 130);
        private const float MenuAuraScale = 4.2f;
        private const float HeldAuraScale = 3.2f;
        private const float CenterOffset = 32f;
        private const float DepthStep = 0.0001f;
        private const float HeldDepthDivisor = 10000f;
        private const int HeldDepthPixels = 3;

        /// <summary>Set by ModEntry: the qualified item ids tampered away this loop.</summary>
        internal static Func<ISet<string>> Tainted;

        private static readonly Type[] DrawInMenuArgs =
        {
            typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(float), typeof(float),
            typeof(StackDrawType), typeof(Color), typeof(bool),
        };

        private static readonly Type[] DrawWhenHeldArgs =
        {
            typeof(SpriteBatch), typeof(Vector2), typeof(Farmer),
        };

        internal static void Apply(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.drawInMenu), DrawInMenuArgs)
                    ?? throw new MissingMethodException("Object.drawInMenu"),
                prefix: new HarmonyMethod(typeof(TaintedAuraPatch), nameof(MenuPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.drawWhenHeld), DrawWhenHeldArgs)
                    ?? throw new MissingMethodException("Object.drawWhenHeld"),
                prefix: new HarmonyMethod(typeof(TaintedAuraPatch), nameof(HeldPrefix)));
        }

        private static bool IsTainted(Item item)
        {
            ISet<string> ids = Tainted?.Invoke();
            return ids != null && ids.Count > 0 && ids.Contains(item.QualifiedItemId);
        }

        private static void DrawAura(SpriteBatch b, Vector2 center, float scale, float depth)
        {
            Texture2D tex = Game1.shadowTexture;
            if (tex == null || Game1.currentGameTime == null) return;
            float pulse = TaintedItems.Pulse(Game1.currentGameTime.TotalGameTime.TotalMilliseconds);
            b.Draw(tex, center, tex.Bounds, AuraColor * pulse, 0f,
                new Vector2(tex.Bounds.Center.X, tex.Bounds.Center.Y), scale, SpriteEffects.None,
                Math.Max(0f, depth - DepthStep));
        }

        private static void MenuPrefix(StardewValley.Object __instance, SpriteBatch spriteBatch, Vector2 location, float scaleSize, float layerDepth)
        {
            if (!IsTainted(__instance)) return;
            DrawAura(spriteBatch, location + new Vector2(CenterOffset, CenterOffset) * scaleSize,
                MenuAuraScale * scaleSize, layerDepth);
        }

        private static void HeldPrefix(StardewValley.Object __instance, SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
        {
            if (!IsTainted(__instance)) return;
            // objectPosition is the sprite's top-left; a 16px sprite at 4x is a 64px square.
            float depth = Math.Max(0f, (f.StandingPixel.Y + HeldDepthPixels) / HeldDepthDivisor);
            DrawAura(spriteBatch, objectPosition + new Vector2(CenterOffset, CenterOffset), HeldAuraScale, depth);
        }
    }
}
