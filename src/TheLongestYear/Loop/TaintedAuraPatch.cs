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
        private static readonly Color AuraColor = new(120, 20, 180);
        private const int GlowTextureSize = 64;
        private const float MenuAuraScale = 1.6f;
        private const float HeldAuraScale = 1.5f;
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

        private static Texture2D _glow;

        /// <summary>A white soft-edged disc, built once on the first draw. Game1.shadowTexture
        /// cannot carry the colour: its pixels are black, so tinting it only darkens.</summary>
        private static Texture2D Glow(GraphicsDevice device)
        {
            if (_glow != null && !_glow.IsDisposed) return _glow;
            var pixels = new Color[GlowTextureSize * GlowTextureSize];
            float half = GlowTextureSize / 2f;
            for (int y = 0; y < GlowTextureSize; y++)
            for (int x = 0; x < GlowTextureSize; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float a = TaintedItems.Falloff((float)Math.Sqrt(dx * dx + dy * dy));
                pixels[y * GlowTextureSize + x] = Color.White * a; // premultiplied
            }
            _glow = new Texture2D(device, GlowTextureSize, GlowTextureSize);
            _glow.SetData(pixels);
            return _glow;
        }

        private static bool IsTainted(Item item)
        {
            ISet<string> ids = Tainted?.Invoke();
            return ids != null && ids.Count > 0 && ids.Contains(item.QualifiedItemId);
        }

        private static void DrawAura(SpriteBatch b, Vector2 center, float scale, float depth)
        {
            if (Game1.currentGameTime == null || b.GraphicsDevice == null) return;
            Texture2D tex = Glow(b.GraphicsDevice);
            float pulse = TaintedItems.Pulse(Game1.currentGameTime.TotalGameTime.TotalMilliseconds);
            b.Draw(tex, center, tex.Bounds, AuraColor * pulse, 0f,
                new Vector2(tex.Width / 2f, tex.Height / 2f), scale, SpriteEffects.None,
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
